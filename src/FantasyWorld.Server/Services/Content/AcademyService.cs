// FantasyWorld.Server/Services/Content/AcademyService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Content;

public interface IAcademyService
{
    Task<List<AcademyDto>>          GetAllAsync(long charId);
    Task<(bool Ok, string Msg)>     EnrollAsync(long charId, int academyId, string lang);
    Task<(bool Ok, string Msg)>     LeaveAsync(long charId, string lang);
    Task<List<AcademyExamDto>>      GetExamsAsync(long charId);
    Task<List<ExamQuestionDto>>     GetExamQuestionsAsync(long charId, int examId, string lang);
    Task<ExamResultDto?>            SubmitExamAsync(long charId, SubmitExamRequest req, string lang);
    Task<List<AcademyRankDto>>      GetRankingAsync(int academyId, int top);
    Task<List<AcademyTournament>>   GetTournamentsAsync();
    Task                            ProcessTournamentsAsync(); // cron
}

public class AcademyService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<AcademyService> logger) : IAcademyService
{
    // ─── Get all academies ───────────────────────────────────
    public async Task<List<AcademyDto>> GetAllAsync(long charId)
    {
        var enrolled = await db.CharacterAcademies
            .FirstOrDefaultAsync(ca => ca.CharacterId == charId);

        var academies = await db.Academies
            .Include(a => a.Enrollments)
            .ToListAsync();

        return academies.Select(a => new AcademyDto(
            a.Id, a.Name, a.Element.ToString(), a.Description ?? "",
            a.Enrollments.Count,
            enrolled?.AcademyId == a.Id,
            enrolled?.AcademyId == a.Id ? enrolled.Grade : 0,
            enrolled?.AcademyId == a.Id ? enrolled.RankPoints : 0
        )).ToList();
    }

    // ─── Enroll ──────────────────────────────────────────────
    public async Task<(bool, string)> EnrollAsync(
        long charId, int academyId, string lang)
    {
        var already = await db.CharacterAcademies
            .AnyAsync(ca => ca.CharacterId == charId);
        if (already)
            return (false, loc.Get("error.bad_request", lang));

        var academy = await db.Academies.FindAsync(academyId);
        if (academy is null)
            return (false, loc.Get("error.not_found", lang));

        db.CharacterAcademies.Add(new CharacterAcademy
        {
            CharacterId = charId,
            AcademyId   = academyId,
        });
        await db.SaveChangesAsync();

        // Apply academy bonuses to character
        if (academy.BonusJson is not null)
        {
            var bonuses = JsonSerializer.Deserialize<Dictionary<string, int>>(academy.BonusJson) ?? [];
            var char_   = await db.Characters.FindAsync(charId)!;
            if (char_ is not null)
            {
                if (bonuses.TryGetValue("atk", out var atk)) char_.Atk += atk;
                if (bonuses.TryGetValue("def", out var def)) char_.Def += def;
                if (bonuses.TryGetValue("spd", out var spd)) char_.Spd += spd;
                if (bonuses.TryGetValue("hp",  out var hp))  { char_.HpMax += hp; char_.Hp += hp; }
                if (bonuses.TryGetValue("mp",  out var mp))  { char_.MpMax += mp; char_.Mp += mp; }
                await db.SaveChangesAsync();
            }
        }

        logger.LogInformation("Enrolled: charId={Char} academyId={Aca}", charId, academyId);
        return (true, loc.Get("academy.enrolled", lang,
            new { name = academy.Name }));
    }

    // ─── Leave ───────────────────────────────────────────────
    public async Task<(bool, string)> LeaveAsync(long charId, string lang)
    {
        var ca = await db.CharacterAcademies
            .FirstOrDefaultAsync(c => c.CharacterId == charId);
        if (ca is null) return (false, loc.Get("error.not_found", lang));

        db.CharacterAcademies.Remove(ca);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Get exams ───────────────────────────────────────────
    public async Task<List<AcademyExamDto>> GetExamsAsync(long charId)
    {
        var ca = await db.CharacterAcademies
            .FirstOrDefaultAsync(c => c.CharacterId == charId);
        if (ca is null) return [];

        var exams = await db.AcademyExams
            .Where(e => e.AcademyId == ca.AcademyId && e.MinGrade <= ca.Grade)
            .ToListAsync();

        var results = new List<AcademyExamDto>();
        foreach (var exam in exams)
        {
            var lastResult = await db.AcademyExamResults
                .Where(r => r.CharacterId == charId && r.ExamId == exam.Id)
                .OrderByDescending(r => r.TakenAt)
                .FirstOrDefaultAsync();

            long? cooldownUntil = null;
            bool canTake = true;
            if (lastResult is not null)
            {
                var cooldownEnd = lastResult.TakenAt.AddHours(exam.CooldownHours);
                if (cooldownEnd > DateTime.UtcNow)
                {
                    canTake = false;
                    cooldownUntil = new DateTimeOffset(cooldownEnd).ToUnixTimeMilliseconds();
                }
            }

            results.Add(new AcademyExamDto(
                exam.Id, exam.Name, exam.Difficulty,
                exam.MinGrade, canTake, cooldownUntil));
        }
        return results;
    }

    // ─── Get questions ───────────────────────────────────────
    public async Task<List<ExamQuestionDto>> GetExamQuestionsAsync(
        long charId, int examId, string lang)
    {
        var exam = await db.AcademyExams.FindAsync(examId);
        if (exam?.QuestionsJson is null) return [];

        var questions = JsonSerializer.Deserialize<List<ExamQuestion>>(exam.QuestionsJson) ?? [];
        var isVi = lang == "vi";

        return questions.Select((q, i) => new ExamQuestionDto(
            i,
            isVi ? q.QuestionVi : q.QuestionEn,
            isVi ? q.OptionsVi : q.OptionsEn)).ToList();
    }

    // ─── Submit exam ─────────────────────────────────────────
    public async Task<ExamResultDto?> SubmitExamAsync(
        long charId, SubmitExamRequest req, string lang)
    {
        var ca = await db.CharacterAcademies
            .FirstOrDefaultAsync(c => c.CharacterId == charId);
        if (ca is null) return null;

        var exam = await db.AcademyExams.FindAsync(req.ExamId);
        if (exam?.QuestionsJson is null) return null;

        var questions = JsonSerializer.Deserialize<List<ExamQuestion>>(exam.QuestionsJson) ?? [];

        // Tính điểm
        int correct = 0;
        for (int i = 0; i < Math.Min(req.Answers.Count, questions.Count); i++)
        {
            if (req.Answers[i] == questions[i].AnswerIndex)
                correct++;
        }

        int score  = questions.Count == 0 ? 0
            : (int)Math.Round((double)correct / questions.Count * 100);
        bool passed = score >= 60;

        // Ghi kết quả
        db.AcademyExamResults.Add(new AcademyExamResult
        {
            CharacterId = charId,
            ExamId      = req.ExamId,
            Score       = score,
            Passed      = passed,
        });

        int rankPointsEarned = 0;
        int newGrade = ca.Grade;

        if (passed)
        {
            ca.ExamPasses++;
            rankPointsEarned  = score * exam.Difficulty * 10;
            ca.RankPoints    += rankPointsEarned;

            // Tăng grade mỗi 3 kỳ thi đậu
            if (ca.ExamPasses % 3 == 0 && ca.Grade < 10)
            {
                ca.Grade++;
                newGrade = ca.Grade;
                logger.LogInformation("Academy grade up: charId={Char} grade={Grade}",
                    charId, ca.Grade);
            }
        }

        await db.SaveChangesAsync();

        return new ExamResultDto(score, passed, correct,
            questions.Count, newGrade, rankPointsEarned);
    }

    // ─── Get ranking ─────────────────────────────────────────
    public async Task<List<AcademyRankDto>> GetRankingAsync(int academyId, int top)
    {
        return await db.CharacterAcademies
            .Where(ca => ca.AcademyId == academyId)
            .Include(ca => ca.Character)
            .OrderByDescending(ca => ca.RankPoints)
            .Take(top)
            .Select((ca, idx) => new AcademyRankDto(
                idx + 1,
                ca.CharacterId,
                ca.Character.Name,
                ca.Grade,
                ca.RankPoints,
                ca.ExamPasses))
            .ToListAsync();
    }

    // ─── Get tournaments ─────────────────────────────────────
    public async Task<List<AcademyTournament>> GetTournamentsAsync()
    {
        return await db.AcademyTournaments
            .Include(t => t.AcademyA)
            .Include(t => t.AcademyB)
            .OrderByDescending(t => t.HeldAt)
            .Take(10)
            .ToListAsync();
    }

    // ─── Process tournaments (cron) ──────────────────────────
    public async Task ProcessTournamentsAsync()
    {
        var ongoing = await db.AcademyTournaments
            .Where(t => t.Status == "ongoing" && t.HeldAt.AddHours(1) < DateTime.UtcNow)
            .Include(t => t.AcademyA)
            .Include(t => t.AcademyB)
            .ToListAsync();

        foreach (var t in ongoing)
        {
            // Tính điểm từ participants
            var participants = await db.AcademyTournamentParticipants
                .Where(p => p.TournamentId == t.Id)
                .Include(p => p.Character).ThenInclude(c => c.AcademyEnrollments)
                .ToListAsync();

            var scoreA = participants
                .Where(p => p.Character.AcademyEnrollments.Any(e => e.AcademyId == t.AcademyAId))
                .Sum(p => p.Score);
            var scoreB = participants
                .Where(p => p.Character.AcademyEnrollments.Any(e => e.AcademyId == t.AcademyBId))
                .Sum(p => p.Score);

            t.WinnerId   = scoreA >= scoreB ? t.AcademyAId : t.AcademyBId;
            t.Status     = "finished";
            t.ScoresJson = JsonSerializer.Serialize(new
            {
                a = scoreA, b = scoreB,
                academyA = t.AcademyA.Name,
                academyB = t.AcademyB.Name,
            });

            logger.LogInformation("Tournament finished: id={Id} winner={Winner}",
                t.Id, t.WinnerId);
        }

        if (ongoing.Count > 0) await db.SaveChangesAsync();
    }

    // ─── Private records for deserialization ─────────────────
    private record ExamQuestion(
        string QuestionVi, string QuestionEn,
        List<string> OptionsVi, List<string> OptionsEn,
        int AnswerIndex);
}
