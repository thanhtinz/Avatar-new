// FantasyWorld.Server/Services/Entertainment/MiniGameService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Entertainment;

public interface IMiniGameService
{
    Task<List<MiniGameDto>>          GetAllAsync(long charId);
    Task<(bool Ok, string Msg)>      StartAsync(long charId, int miniGameId, string lang);
    Task<MiniGameResultDto>          SubmitScoreAsync(long charId, SubmitScoreRequest req, string lang);
    Task<List<MiniGameLeaderboardDto>> GetLeaderboardAsync(int miniGameId, int top);
}

public class MiniGameService(
    GameDbContext        db,
    ILocalizationService loc) : IMiniGameService
{
    public async Task<List<MiniGameDto>> GetAllAsync(long charId)
    {
        var scores = await db.MiniGameScores
            .Where(s => s.CharacterId == charId)
            .ToListAsync();

        return await db.MiniGames
            .Where(g => g.IsActive)
            .Select(g => new MiniGameDto(
                g.Id, g.Name, g.GameType, g.Description ?? "",
                g.EntryFee,
                scores.FirstOrDefault(s => s.MiniGameId == g.Id)?.BestScore ?? 0,
                scores.Any(s => s.MiniGameId == g.Id
                    && s.LastPlayed.HasValue
                    && s.LastPlayed.Value.AddMinutes(g.CooldownMin) > DateTime.UtcNow),
                scores.Where(s => s.MiniGameId == g.Id && s.LastPlayed.HasValue
                    && s.LastPlayed.Value.AddMinutes(g.CooldownMin) > DateTime.UtcNow)
                    .Select(s => (long?)new DateTimeOffset(
                        s.LastPlayed!.Value.AddMinutes(g.CooldownMin))
                        .ToUnixTimeMilliseconds())
                    .FirstOrDefault()))
            .ToListAsync();
    }

    public async Task<(bool, string)> StartAsync(long charId, int miniGameId, string lang)
    {
        var game = await db.MiniGames.FindAsync(miniGameId);
        if (game is null || !game.IsActive)
            return (false, loc.Get("error.not_found", lang));

        // Cooldown check
        var score = await db.MiniGameScores
            .FirstOrDefaultAsync(s => s.CharacterId == charId && s.MiniGameId == miniGameId);

        if (score?.LastPlayed.HasValue == true
            && score.LastPlayed.Value.AddMinutes(game.CooldownMin) > DateTime.UtcNow)
            return (false, loc.Get("error.bad_request", lang));

        // Phí vào game
        if (game.EntryFee > 0)
        {
            var char_ = await db.Characters.FindAsync(charId)!;
            if (char_!.Gold < game.EntryFee)
                return (false, loc.Get("character.insufficient_gold", lang,
                    new { need = game.EntryFee, have = char_.Gold }));
            char_.Gold -= game.EntryFee;
            await db.SaveChangesAsync();
        }

        return (true, "OK");
    }

    public async Task<MiniGameResultDto> SubmitScoreAsync(
        long charId, SubmitScoreRequest req, string lang)
    {
        var game = await db.MiniGames.FindAsync(req.MiniGameId);
        if (game is null) throw new InvalidOperationException();

        var score = await db.MiniGameScores
            .FirstOrDefaultAsync(s => s.CharacterId == charId && s.MiniGameId == req.MiniGameId);

        bool isRecord = false;
        if (score is null)
        {
            score = new MiniGameScore
            {
                CharacterId = charId,
                MiniGameId  = req.MiniGameId,
                BestScore   = req.Score,
                PlayCount   = 1,
                LastPlayed  = DateTime.UtcNow,
            };
            db.MiniGameScores.Add(score);
            isRecord = true;
        }
        else
        {
            score.PlayCount++;
            score.LastPlayed = DateTime.UtcNow;
            if (req.Score > score.BestScore)
            {
                score.BestScore = req.Score;
                isRecord = true;
            }
        }
        score.Score = req.Score;

        // Tính thưởng
        int goldEarned = req.Score / 10;
        int expEarned  = req.Score / 5;
        if (isRecord && game.HighScoreRewardJson is not null)
        {
            goldEarned += 500;
            expEarned  += 200;
        }

        // Cộng thưởng
        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + goldEarned)
                .SetProperty(c => c.Exp,  c => c.Exp  + expEarned));

        await db.SaveChangesAsync();

        // Rank
        var rank = await db.MiniGameScores
            .CountAsync(s => s.MiniGameId == req.MiniGameId && s.BestScore > req.Score) + 1;

        return new MiniGameResultDto(req.Score, isRecord, goldEarned, expEarned, rank);
    }

    public async Task<List<MiniGameLeaderboardDto>> GetLeaderboardAsync(int miniGameId, int top) =>
        await db.MiniGameScores
            .Where(s => s.MiniGameId == miniGameId)
            .Include(s => s.Character)
            .OrderByDescending(s => s.BestScore)
            .Take(top)
            .Select((s, i) => new MiniGameLeaderboardDto(
                i + 1, s.CharacterId, s.Character.Name, s.BestScore))
            .ToListAsync();
}

// ─── Mount Race Service ──────────────────────────────────────

public interface IMountRaceService
{
    Task<List<MountRaceDto>>        GetAvailableAsync(int mapId);
    Task<(bool Ok, string Msg, long RaceId)> CreateRaceAsync(long charId, int trackId, string lang);
    Task<(bool Ok, string Msg)>     JoinAsync(long charId, JoinRaceRequest req, string lang);
    Task<(bool Ok, string Msg)>     LeaveAsync(long charId, long raceId, string lang);
    Task<List<RaceUpdateDto>>       GetRaceStateAsync(long raceId);
    Task<List<RaceFinishDto>>       GetResultsAsync(long raceId);
    Task                            ProcessRacesAsync(); // cron
}

public class MountRaceService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc,
    ILogger<MountRaceService> logger) : IMountRaceService
{
    // In-memory race progress: raceId → {charId → progress}
    private static readonly Dictionary<long, Dictionary<long, float>> _raceProgress = new();

    public async Task<List<MountRaceDto>> GetAvailableAsync(int mapId)
    {
        return await db.MountRaces
            .Where(r => r.Status != "finished" && r.Track.MapId == mapId)
            .Include(r => r.Track)
            .Include(r => r.Entries)
            .Select(r => new MountRaceDto(
                r.Id, r.Track.Name, r.Status,
                r.EntryFee, r.Entries.Count, r.MaxRacers,
                r.StartedAt.HasValue
                    ? new DateTimeOffset(r.StartedAt.Value).ToUnixTimeMilliseconds()
                    : null))
            .ToListAsync();
    }

    public async Task<(bool, string, long)> CreateRaceAsync(
        long charId, int trackId, string lang)
    {
        var track = await db.MountRaceTracks.FindAsync(trackId);
        if (track is null) return (false, loc.Get("error.not_found", lang), 0);

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Level < track.MinLevel)
            return (false, loc.Get("error.bad_request", lang), 0);

        var race = new MountRace
        {
            TrackId   = trackId,
            Status    = "waiting",
            EntryFee  = 100,
            MaxRacers = 8,
            RewardJson = """{"1st":500,"2nd":300,"3rd":150}""",
        };
        db.MountRaces.Add(race);
        await db.SaveChangesAsync();

        return (true, "OK", race.Id);
    }

    public async Task<(bool, string)> JoinAsync(
        long charId, JoinRaceRequest req, string lang)
    {
        var race = await db.MountRaces
            .Include(r => r.Entries)
            .FirstOrDefaultAsync(r => r.Id == req.RaceId && r.Status == "waiting");

        if (race is null) return (false, loc.Get("error.not_found", lang));

        if (race.Entries.Any(e => e.CharacterId == charId))
            return (false, loc.Get("error.bad_request", lang));

        if (race.Entries.Count >= race.MaxRacers)
            return (false, loc.Get("error.bad_request", lang));

        // Phí tham gia
        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < race.EntryFee)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = race.EntryFee, have = char_.Gold }));

        char_.Gold -= race.EntryFee;

        db.MountRaceEntries.Add(new MountRaceEntry
        {
            RaceId      = req.RaceId,
            CharacterId = charId,
            MountPetId  = req.MountPetId,
        });

        // Auto-start nếu đủ người
        if (race.Entries.Count + 1 >= race.MaxRacers)
        {
            race.Status    = "countdown";
            race.StartedAt = DateTime.UtcNow.AddSeconds(10);
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> LeaveAsync(
        long charId, long raceId, string lang)
    {
        var entry = await db.MountRaceEntries
            .FirstOrDefaultAsync(e => e.CharacterId == charId && e.RaceId == raceId);

        if (entry is null) return (false, loc.Get("error.not_found", lang));

        var race = await db.MountRaces.FindAsync(raceId);
        if (race?.Status != "waiting")
            return (false, loc.Get("error.bad_request", lang));

        // Hoàn tiền
        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + race.EntryFee));

        db.MountRaceEntries.Remove(entry);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<RaceUpdateDto>> GetRaceStateAsync(long raceId)
    {
        if (!_raceProgress.TryGetValue(raceId, out var progress))
            return [];

        var entries = await db.MountRaceEntries
            .Where(e => e.RaceId == raceId)
            .Include(e => e.Character)
            .ToListAsync();

        return entries
            .OrderByDescending(e => progress.GetValueOrDefault(e.CharacterId, 0))
            .Select((e, i) => new RaceUpdateDto(
                e.CharacterId, e.Character.Name,
                progress.GetValueOrDefault(e.CharacterId, 0), i + 1))
            .ToList();
    }

    public async Task<List<RaceFinishDto>> GetResultsAsync(long raceId) =>
        await db.MountRaceEntries
            .Where(e => e.RaceId == raceId && e.FinishPosition.HasValue)
            .Include(e => e.Character)
            .OrderBy(e => e.FinishPosition)
            .Select(e => new RaceFinishDto(
                e.CharacterId, e.Character.Name,
                e.FinishPosition!.Value, e.FinishTimeMs ?? 0, 0))
            .ToListAsync();

    public async Task ProcessRacesAsync()
    {
        var now = DateTime.UtcNow;

        // Bắt đầu race đã countdown xong
        var starting = await db.MountRaces
            .Include(r => r.Entries)
            .Where(r => r.Status == "countdown"
                && r.StartedAt.HasValue && r.StartedAt <= now)
            .ToListAsync();

        foreach (var race in starting)
        {
            race.Status = "racing";
            _raceProgress[race.Id] = race.Entries
                .ToDictionary(e => e.CharacterId, _ => 0f);
            logger.LogInformation("Race started: id={Id}", race.Id);
        }

        // Simulate racing progress
        var racing = await db.MountRaces
            .Include(r => r.Entries).ThenInclude(e => e.Character)
            .Include(r => r.Entries).ThenInclude(e => e.MountPet).ThenInclude(p => p!.Species)
            .Where(r => r.Status == "racing")
            .ToListAsync();

        var rng = new Random();
        foreach (var race in racing)
        {
            if (!_raceProgress.TryGetValue(race.Id, out var progress)) continue;

            bool anyFinished = false;
            int  finishPos   = race.Entries.Count(e => e.FinishPosition.HasValue) + 1;

            foreach (var entry in race.Entries.Where(e => !e.FinishPosition.HasValue))
            {
                // Tốc độ dựa trên spd của mount pet (hoặc base 5)
                float speed = entry.MountPet?.Spd ?? 5;
                speed = speed / 30f + (float)(rng.NextDouble() * 0.1);

                progress[entry.CharacterId] = Math.Min(1f,
                    progress.GetValueOrDefault(entry.CharacterId, 0) + speed * 0.05f);

                if (progress[entry.CharacterId] >= 1f)
                {
                    entry.FinishPosition = finishPos++;
                    entry.FinishTimeMs   = (int)(now - race.StartedAt!.Value).TotalMilliseconds;
                    anyFinished = true;

                    // Trao thưởng
                    int reward = entry.FinishPosition switch
                    {
                        1 => 500, 2 => 300, 3 => 150, _ => 50
                    };
                    await db.Characters.Where(c => c.Id == entry.CharacterId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(c => c.Gold, c => c.Gold + reward));
                }
            }

            // Tất cả về đích
            if (race.Entries.All(e => e.FinishPosition.HasValue))
            {
                race.Status     = "finished";
                race.FinishedAt = now;
                _raceProgress.Remove(race.Id);
                logger.LogInformation("Race finished: id={Id}", race.Id);
            }

            if (anyFinished) await db.SaveChangesAsync();
        }

        if (starting.Count > 0 || racing.Count > 0)
            await db.SaveChangesAsync();
    }
}
