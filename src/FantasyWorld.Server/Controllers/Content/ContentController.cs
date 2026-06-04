// FantasyWorld.Server/Controllers/Content/ContentController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FantasyWorld.Server.Controllers.Economy;
using FantasyWorld.Server.Services.Content;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Controllers.Content;

// ─── Quest Controller ────────────────────────────────────────

[ApiController, Route("api/quests"), Authorize]
public class QuestController(IQuestService questSvc) : GameControllerBase
{
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable() =>
        Ok(new { success = true, data = await questSvc.GetAvailableAsync(CharId, Lang) });

    [HttpGet("active")]
    public async Task<IActionResult> GetActive() =>
        Ok(new { success = true, data = await questSvc.GetActiveAsync(CharId, Lang) });

    [HttpGet("completed")]
    public async Task<IActionResult> GetCompleted() =>
        Ok(new { success = true, data = await questSvc.GetCompletedAsync(CharId, Lang) });

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptQuestRequest req)
    {
        var (ok, msg) = await questSvc.AcceptAsync(CharId, req.QuestId, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{questId}/abandon")]
    public async Task<IActionResult> Abandon(int questId)
    {
        var (ok, msg) = await questSvc.AbandonAsync(CharId, questId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("{questId}/complete")]
    public async Task<IActionResult> Complete(int questId)
    {
        var (ok, msg, reward) = await questSvc.CompleteAsync(CharId, questId, Lang);
        return ok ? Ok(new { success = true, message = msg, reward })
                  : BadRequest(new { success = false, message = msg });
    }
}

// ─── Academy Controller ──────────────────────────────────────

[ApiController, Route("api/academies"), Authorize]
public class AcademyController(IAcademyService academySvc) : GameControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, data = await academySvc.GetAllAsync(CharId) });

    [HttpPost("{academyId}/enroll")]
    public async Task<IActionResult> Enroll(int academyId)
    {
        var (ok, msg) = await academySvc.EnrollAsync(CharId, academyId, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave()
    {
        var (ok, msg) = await academySvc.LeaveAsync(CharId, Lang);
        return ok ? Ok(new { success = true })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("exams")]
    public async Task<IActionResult> GetExams() =>
        Ok(new { success = true, data = await academySvc.GetExamsAsync(CharId) });

    [HttpGet("exams/{examId}/questions")]
    public async Task<IActionResult> GetQuestions(int examId) =>
        Ok(new { success = true,
            data = await academySvc.GetExamQuestionsAsync(CharId, examId, Lang) });

    [HttpPost("exams/submit")]
    public async Task<IActionResult> SubmitExam([FromBody] SubmitExamRequest req)
    {
        var result = await academySvc.SubmitExamAsync(CharId, req, Lang);
        return result is null
            ? BadRequest(new { success = false })
            : Ok(new { success = true, data = result });
    }

    [HttpGet("{academyId}/ranking")]
    public async Task<IActionResult> GetRanking(int academyId,
        [FromQuery] int top = 50) =>
        Ok(new { success = true,
            data = await academySvc.GetRankingAsync(academyId, top) });

    [HttpGet("tournaments")]
    public async Task<IActionResult> GetTournaments() =>
        Ok(new { success = true,
            data = await academySvc.GetTournamentsAsync() });
}

// ─── Dungeon Controller ──────────────────────────────────────

[ApiController, Route("api/dungeons"), Authorize]
public class DungeonController(IDungeonService dungeonSvc) : GameControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAvailable() =>
        Ok(new { success = true, data = await dungeonSvc.GetAvailableAsync(CharId) });

    [HttpPost("enter")]
    public async Task<IActionResult> Enter([FromBody] DungeonEnterRequest req)
    {
        var (ok, msg, runId) = await dungeonSvc.EnterAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true, runId })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("runs/{runId}/floor")]
    public async Task<IActionResult> GetFloor(long runId)
    {
        var floor = await dungeonSvc.GetCurrentFloorAsync(runId);
        return floor is null ? NotFound()
                             : Ok(new { success = true, data = floor });
    }

    [HttpPost("combat")]
    public async Task<IActionResult> Combat([FromBody] CombatActionRequest req)
    {
        var (ok, result) = await dungeonSvc.CombatActionAsync(CharId, req, Lang);
        return ok ? Ok(new { success = true, data = result })
                  : BadRequest(new { success = false });
    }

    [HttpPost("runs/{runId}/advance")]
    public async Task<IActionResult> Advance(long runId)
    {
        var (ok, msg) = await dungeonSvc.AdvanceFloorAsync(CharId, runId, Lang);
        return ok ? Ok(new { success = true, message = msg })
                  : BadRequest(new { success = false, message = msg });
    }

    [HttpGet("runs/{runId}/result")]
    public async Task<IActionResult> GetResult(long runId)
    {
        var result = await dungeonSvc.GetRunResultAsync(runId);
        return result is null ? NotFound()
                              : Ok(new { success = true, data = result });
    }
}

// ─── Story Controller ────────────────────────────────────────

[ApiController, Route("api/story"), Authorize]
public class StoryController(IStoryService storySvc) : GameControllerBase
{
    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress() =>
        Ok(new { success = true,
            data = await storySvc.GetProgressAsync(CharId, Lang) });

    [HttpPost("chapters/{chapterId}/start")]
    public async Task<IActionResult> Start(int chapterId)
    {
        var node = await storySvc.StartChapterAsync(CharId, chapterId, Lang);
        return node is null ? BadRequest(new { success = false })
                            : Ok(new { success = true, data = node });
    }

    [HttpGet("chapters/{chapterId}/current")]
    public async Task<IActionResult> GetCurrent(int chapterId)
    {
        var node = await storySvc.GetCurrentNodeAsync(CharId, chapterId, Lang);
        return node is null ? NotFound()
                            : Ok(new { success = true, data = node });
    }

    [HttpPost("choice")]
    public async Task<IActionResult> MakeChoice([FromBody] MakeChoiceRequest req)
    {
        var node = await storySvc.MakeChoiceAsync(CharId, req, Lang);
        return node is null ? BadRequest(new { success = false })
                            : Ok(new { success = true, data = node });
    }

    [HttpPost("chapters/{chapterId}/advance")]
    public async Task<IActionResult> Advance(int chapterId)
    {
        var node = await storySvc.AdvanceAsync(CharId, chapterId, Lang);
        return node is null ? NotFound()
                            : Ok(new { success = true, data = node });
    }

    [HttpGet("endings")]
    public async Task<IActionResult> GetEndings() =>
        Ok(new { success = true,
            data = await storySvc.GetEndingsAsync(CharId) });
}

// ─── Achievement Controller ──────────────────────────────────

[ApiController, Route("api/achievements"), Authorize]
public class AchievementController(
    FantasyWorld.Server.Data.GameDbContext db) : GameControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var achvs = await db.CharacterAchievements
            .Where(ca => ca.CharacterId == CharId)
            .Include(ca => ca.Achievement)
            .Select(ca => new AchievementDto(
                ca.AchievementId,
                ca.Achievement.Name,
                ca.Achievement.Category,
                ca.Progress, ca.Target,
                ca.IsCompleted,
                ca.Achievement.Points,
                ca.Achievement.BadgeIcon))
            .ToListAsync();
        return Ok(new { success = true, data = achvs });
    }

    [HttpGet("titles")]
    public async Task<IActionResult> GetTitles()
    {
        var titles = await db.CharacterTitles
            .Where(ct => ct.CharacterId == CharId)
            .Include(ct => ct.Title)
            .Select(ct => new TitleDto(
                ct.TitleId, ct.Title.Name, ct.Title.Description ?? "",
                ct.IsActive,
                new System.DateTimeOffset(ct.EarnedAt).ToUnixTimeMilliseconds()))
            .ToListAsync();
        return Ok(new { success = true, data = titles });
    }

    [HttpPatch("titles/{titleId}/activate")]
    public async Task<IActionResult> ActivateTitle(int titleId)
    {
        // Tắt title hiện tại
        await db.CharacterTitles
            .Where(ct => ct.CharacterId == CharId && ct.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(ct => ct.IsActive, false));

        // Bật title mới
        var title = await db.CharacterTitles
            .FirstOrDefaultAsync(ct => ct.CharacterId == CharId && ct.TitleId == titleId);
        if (title is null) return NotFound();

        title.IsActive = true;
        await db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}
