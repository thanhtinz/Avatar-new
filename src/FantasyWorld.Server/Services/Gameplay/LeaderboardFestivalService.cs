// FantasyWorld.Server/Services/Gameplay/LeaderboardFestivalService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Server.BackgroundServices;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Services.Gameplay;

// ─── Leaderboard ─────────────────────────────────────────────

public interface ILeaderboardService
{
    Task<List<LeaderboardEntry>> GetAsync(string category, int top);
    Task                         SnapshotAsync(); // cron daily
}

public class LeaderboardService(
    GameDbContext db, ILogger<LeaderboardService> logger) : ILeaderboardService
{
    // 10 categories (no PvP)
    public static readonly string[] Categories =
    [
        "richest",        // by gold
        "best_house",     // by house score
        "top_fisher",     // by fish caught
        "fashionista",    // by fashion score
        "world_traveler", // by landmark stamps
        "master_farmer",  // by harvests
        "pet_collector",  // by unique species
        "academy_scholar",// by rank points
        "benefactor",     // by museum donations
        "top_performer",  // by performance tips earned
    ];

    public async Task<List<LeaderboardEntry>> GetAsync(string category, int top)
    {
        // Return from latest snapshot
        return await db.LeaderboardSnapshots
            .Where(s => s.Category == category
                && s.SnapshotDate == db.LeaderboardSnapshots
                    .Where(x => x.Category == category)
                    .Max(x => x.SnapshotDate))
            .Include(s => s.Character)
            .OrderBy(s => s.Rank)
            .Take(top)
            .Select(s => new LeaderboardEntry(
                s.Rank, s.CharacterId, s.Character.Name,
                s.Character.Level, s.Score, s.Category))
            .ToListAsync();
    }

    public async Task SnapshotAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var category in Categories)
        {
            IQueryable<(long CharId, long Score)> query = category switch
            {
                "richest"         => db.Characters.Select(c => new { c.Id, Score = c.Gold })
                                        .Select(x => ValueTuple.Create(x.Id, x.Score)),
                "best_house"      => db.CharacterFameStats.Select(f => ValueTuple.Create(f.CharacterId, (long)f.HouseScore)),
                "top_fisher"      => db.CharacterFameStats.Select(f => ValueTuple.Create(f.CharacterId, (long)f.FishCaught)),
                "fashionista"     => db.CharacterFameStats.Select(f => ValueTuple.Create(f.CharacterId, (long)f.FashionScore)),
                "world_traveler"  => db.TravelPassports.Select(p => ValueTuple.Create(p.CharacterId, (long)p.StampCount)),
                "master_farmer"   => db.CharacterFameStats.Select(f => ValueTuple.Create(f.CharacterId, (long)f.TotalHarvests)),
                "pet_collector"   => db.Pets.GroupBy(p => p.OwnerId)
                                        .Select(g => ValueTuple.Create(g.Key, (long)g.Count())),
                "academy_scholar" => db.CharacterAcademies.Select(a => ValueTuple.Create(a.CharacterId, (long)a.RankPoints)),
                "benefactor"      => db.MuseumDonations.GroupBy(d => d.CharacterId)
                                        .Select(g => ValueTuple.Create(g.Key, (long)g.Count())),
                "top_performer"   => db.Performances.Where(p => p.Status == "ended")
                                        .GroupBy(p => p.PerformerId)
                                        .Select(g => ValueTuple.Create(g.Key, g.Sum(p => p.TotalTips))),
                _ => throw new ArgumentException($"Unknown category: {category}"),
            };

            var top50 = await ((IQueryable<(long CharId, long Score)>)query)
                .OrderByDescending(x => x.Item2)
                .Take(50)
                .ToListAsync();

            // Delete old snapshots for this category/date
            await db.LeaderboardSnapshots
                .Where(s => s.Category == category && s.SnapshotDate == today)
                .ExecuteDeleteAsync();

            for (int i = 0; i < top50.Count; i++)
            {
                db.LeaderboardSnapshots.Add(new LeaderboardSnapshot
                {
                    Category     = category,
                    Rank         = i + 1,
                    CharacterId  = top50[i].CharId,
                    Score        = top50[i].Score,
                    SnapshotDate = today,
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Leaderboard snapshot complete for {Date}", today);
    }
}

public record LeaderboardEntry(int Rank, long CharId, string Name, int Level, long Score, string Category);

// ─── Endgame Realm ───────────────────────────────────────────

public interface IEndgameRealmService
{
    Task<List<EndgameRealm>>        GetAllAsync();
    Task<List<EndgameRealm>>        GetUnlockedAsync(long charId);
    Task<(bool Ok, string Msg)>     UnlockAsync(long charId, int realmId, string lang);
    Task<(bool Ok, string Msg)>     EnterAsync(long charId, int realmId, string lang);
}

public class EndgameRealmService(
    GameDbContext db, ILocalizationService loc,
    ILogger<EndgameRealmService> logger) : IEndgameRealmService
{
    public async Task<List<EndgameRealm>> GetAllAsync() =>
        await db.EndgameRealms.OrderBy(r => r.RequiredLevel).ToListAsync();

    public async Task<List<EndgameRealm>> GetUnlockedAsync(long charId) =>
        await db.CharacterEndgameRealms
            .Where(cr => cr.CharacterId == charId)
            .Include(cr => cr.Realm)
            .Select(cr => cr.Realm)
            .ToListAsync();

    public async Task<(bool, string)> UnlockAsync(long charId, int realmId, string lang)
    {
        var realm  = await db.EndgameRealms.FindAsync(realmId);
        if (realm is null) return (false, loc.Get("error.not_found", lang));

        var char_  = await db.Characters.FindAsync(charId)!;
        if (char_!.Level < realm.RequiredLevel)
            return (false, loc.Get("error.bad_request", lang));

        // Check extra conditions (e.g. story ending required)
        if (!string.IsNullOrEmpty(realm.RequiredConditionJson))
        {
            var cond = JsonSerializer.Deserialize<Dictionary<string, object>>(realm.RequiredConditionJson) ?? [];
            if (cond.TryGetValue("story_ending", out var endingObj))
            {
                var endingId = Convert.ToInt32(endingObj);
                var hasEnding = await db.CharacterStoryEndings
                    .AnyAsync(e => e.CharacterId == charId && e.EndingId == endingId);
                if (!hasEnding) return (false, loc.Get("error.forbidden", lang));
            }
        }

        var alreadyUnlocked = await db.CharacterEndgameRealms
            .AnyAsync(cr => cr.CharacterId == charId && cr.RealmId == realmId);
        if (alreadyUnlocked) return (false, loc.Get("error.bad_request", lang));

        db.CharacterEndgameRealms.Add(new CharacterEndgameRealm
        {
            CharacterId = charId,
            RealmId     = realmId,
            UnlockedAt  = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        logger.LogInformation("Realm unlocked: charId={C} realm={R}", charId, realm.Name);
        return (true, loc.Get("realm.unlocked", lang, new { name = realm.Name }));
    }

    public async Task<(bool, string)> EnterAsync(long charId, int realmId, string lang)
    {
        var unlocked = await db.CharacterEndgameRealms
            .AnyAsync(cr => cr.CharacterId == charId && cr.RealmId == realmId);
        if (!unlocked) return (false, loc.Get("error.forbidden", lang));

        var realm = await db.EndgameRealms.FindAsync(realmId);
        if (realm is null) return (false, loc.Get("error.not_found", lang));

        // Teleport character to realm entry map
        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.MapId, realm.EntryMapId)
                .SetProperty(c => c.PosX,  realm.EntryX)
                .SetProperty(c => c.PosY,  realm.EntryY));

        await db.SaveChangesAsync();
        return (true, loc.Get("realm.entered", lang, new { name = realm.Name }));
    }
}

// ─── Season Service ──────────────────────────────────────────

public interface ISeasonService
{
    Task<SeasonInfo>    GetCurrentAsync();
    Task<List<SeasonInfo>> GetAllAsync();
    Task               TickAsync(); // cron checks transition
}

public record SeasonInfo(SeasonCode Code, string NameVi, string NameEn,
    DateTime StartAt, DateTime EndAt, string[] PriceModItems);

public class SeasonService(
    GameDbContext db, ILogger<SeasonService> logger) : ISeasonService
{
    public Task<SeasonInfo> GetCurrentAsync()
    {
        var wt = WorldTimeService.Current;
        return Task.FromResult(BuildInfo(wt.Season));
    }

    public Task<List<SeasonInfo>> GetAllAsync() =>
        Task.FromResult(Enum.GetValues<SeasonCode>().Select(s => BuildInfo(s)).ToList());

    public async Task TickAsync()
    {
        // WorldTimeService manages season transitions already.
        // This cron syncs price modifiers when season changes.
        var wt = WorldTimeService.Current;
        var expectedSeason = (int)wt.Season + 1;

        // Activate / deactivate seasonal modifiers
        await db.SeasonalPriceModifiers
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.SeasonId, m => m.SeasonId)); // no-op, just touch

        logger.LogDebug("Season tick: {Season}", wt.Season);
    }

    private static SeasonInfo BuildInfo(SeasonCode season) => season switch
    {
        SeasonCode.Spring => new(season, "Mùa Xuân", "Spring",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(90),
            new[] { "cherry_blossom", "spring_herb", "seed_box" }),
        SeasonCode.Summer => new(season, "Mùa Hè", "Summer",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(90),
            new[] { "sunfish", "tropical_fruit", "ice_cream" }),
        SeasonCode.Autumn => new(season, "Mùa Thu", "Autumn",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(90),
            new[] { "mushroom", "pumpkin", "maple_leaf" }),
        SeasonCode.Winter => new(season, "Mùa Đông", "Winter",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(90),
            new[] { "snowflake", "hot_cocoa", "pine_wood" }),
        _ => new(season, "Không rõ", "Unknown",
            DateTime.UtcNow, DateTime.UtcNow, Array.Empty<string>()),
    };
}

// ─── Festival ────────────────────────────────────────────────

public interface IFestivalService
{
    Task<List<Festival>>            GetAllAsync();
    Task<Festival?>                 GetActiveAsync();
    Task<List<FestivalTask>>        GetTasksAsync(int festivalId, long charId);
    Task<(bool Ok, string Msg)>     ParticipateAsync(long charId, int festivalId, string lang);
    Task<(bool Ok, string Msg)>     CompleteTaskAsync(long charId, int festivalId, int taskId, string lang);
    Task<(bool Ok, string Msg)>     ClaimRewardAsync(long charId, int festivalId, string lang);
    Task                            TickAsync(); // cron activates/expires festivals
}

public class FestivalService(
    GameDbContext db, ILocalizationService loc,
    ILogger<FestivalService> logger) : IFestivalService
{
    // 12 festivals per year with fixed calendar dates
    private static readonly (int Month, int Day, string NameVi, string NameEn, int DurationDays)[]
        FestivalCalendar =
        [
            (1,  1,  "Tết Nguyên Đán",    "Lunar New Year",    7),
            (2,  14, "Lễ Tình Nhân",      "Valentine's Day",   3),
            (3,  20, "Lễ Xuân Phân",      "Spring Equinox",    3),
            (4,  15, "Lễ Hoa Anh Đào",   "Cherry Blossom",    5),
            (5,  1,  "Ngày Lao Động",     "Labor Day",         2),
            (6,  21, "Lễ Hạ Chí",        "Summer Solstice",   3),
            (7,  7,  "Thất Tịch",        "Star Festival",      2),
            (8,  15, "Trung Thu",        "Mid-Autumn",         3),
            (9,  22, "Lễ Thu Phân",      "Autumn Equinox",    3),
            (10, 31, "Halloween",        "Halloween",          2),
            (11, 15, "Lễ Thu Hoạch",     "Harvest Festival",  5),
            (12, 25, "Lễ Giáng Sinh",    "Winter Festival",   7),
        ];

    public async Task<List<Festival>> GetAllAsync() =>
        await db.Festivals.OrderBy(f => f.Month).ThenBy(f => f.Day).ToListAsync();

    public async Task<Festival?> GetActiveAsync() =>
        await db.Festivals.FirstOrDefaultAsync(f => f.IsActive);

    public async Task<List<FestivalTask>> GetTasksAsync(int festivalId, long charId)
    {
        var tasks = await db.FestivalTasks.Where(t => t.FestivalId == festivalId).ToListAsync();
        var completed = await db.CharacterFestivalTasks
            .Where(ct => ct.CharacterId == charId && ct.FestivalId == festivalId)
            .Select(ct => ct.TaskId)
            .ToListAsync();

        foreach (var t in tasks) t.IsCompletedByPlayer = completed.Contains(t.Id);
        return tasks;
    }

    public async Task<(bool, string)> ParticipateAsync(long charId, int festivalId, string lang)
    {
        var festival = await db.Festivals.FindAsync(festivalId);
        if (festival?.IsActive != true) return (false, loc.Get("error.not_found", lang));

        var already = await db.CharacterFestivalParticipants
            .AnyAsync(p => p.CharacterId == charId && p.FestivalId == festivalId);
        if (already) return (false, loc.Get("error.bad_request", lang));

        db.CharacterFestivalParticipants.Add(new CharacterFestivalParticipant
        {
            CharacterId = charId,
            FestivalId  = festivalId,
            JoinedAt    = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return (true, loc.Get("festival.joined", lang, new { name = festival.NameVi }));
    }

    public async Task<(bool, string)> CompleteTaskAsync(
        long charId, int festivalId, int taskId, string lang)
    {
        var isParticipant = await db.CharacterFestivalParticipants
            .AnyAsync(p => p.CharacterId == charId && p.FestivalId == festivalId);
        if (!isParticipant) return (false, loc.Get("error.forbidden", lang));

        var task = await db.FestivalTasks.FindAsync(taskId);
        if (task?.FestivalId != festivalId) return (false, loc.Get("error.not_found", lang));

        var alreadyDone = await db.CharacterFestivalTasks
            .AnyAsync(ct => ct.CharacterId == charId && ct.TaskId == taskId);
        if (alreadyDone) return (false, loc.Get("error.bad_request", lang));

        db.CharacterFestivalTasks.Add(new CharacterFestivalTask
        {
            CharacterId  = charId,
            FestivalId   = festivalId,
            TaskId       = taskId,
            CompletedAt  = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return (true, "OK");
    }

    public async Task<(bool, string)> ClaimRewardAsync(
        long charId, int festivalId, string lang)
    {
        var festival = await db.Festivals.FindAsync(festivalId);
        if (festival is null) return (false, loc.Get("error.not_found", lang));

        var participant = await db.CharacterFestivalParticipants
            .FirstOrDefaultAsync(p => p.CharacterId == charId
                && p.FestivalId == festivalId && !p.RewardClaimed);
        if (participant is null) return (false, loc.Get("error.not_found", lang));

        // Count completed tasks
        var tasksTotal = await db.FestivalTasks.CountAsync(t => t.FestivalId == festivalId);
        var tasksDone  = await db.CharacterFestivalTasks
            .CountAsync(ct => ct.CharacterId == charId && ct.FestivalId == festivalId);

        if (tasksDone < tasksTotal / 2)
            return (false, loc.Get("error.bad_request", lang));

        participant.RewardClaimed = true;

        // Scale reward by completion
        var ratio      = (double)tasksDone / Math.Max(1, tasksTotal);
        var baseGold   = festival.BaseRewardGold;
        var earnedGold = (long)(baseGold * ratio);

        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + earnedGold)
                .SetProperty(c => c.Exp,  c => c.Exp  + festival.BaseRewardExp));

        await db.SaveChangesAsync();
        return (true, loc.Get("festival.reward_claimed", lang, new { gold = earnedGold }));
    }

    public async Task TickAsync()
    {
        var now = DateTime.UtcNow;

        // Deactivate expired festivals
        await db.Festivals
            .Where(f => f.IsActive && f.EndAt < now)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false));

        // Activate festivals whose time has come
        var month = now.Month;
        var day   = now.Day;

        foreach (var (fMonth, fDay, nameVi, nameEn, dur) in FestivalCalendar)
        {
            var festivalStart = new DateTime(now.Year, fMonth, fDay, 0, 0, 0, DateTimeKind.Utc);
            var festivalEnd   = festivalStart.AddDays(dur);

            if (now >= festivalStart && now <= festivalEnd)
            {
                var exists = await db.Festivals.AnyAsync(f =>
                    f.Month == fMonth && f.Day == fDay && f.Year == now.Year);

                if (!exists)
                {
                    db.Festivals.Add(new Festival
                    {
                        Year           = now.Year,
                        Month          = fMonth,
                        Day            = fDay,
                        NameVi         = nameVi,
                        NameEn         = nameEn,
                        StartAt        = festivalStart,
                        EndAt          = festivalEnd,
                        IsActive       = true,
                        BaseRewardGold = 500 * dur,
                        BaseRewardExp  = 200 * dur,
                    });

                    logger.LogInformation("Festival activated: {Name}", nameVi);
                }
            }
        }

        await db.SaveChangesAsync();
    }
}

// ─── UGC (User Generated Content) ────────────────────────────

public interface IUgcService
{
    Task<List<UgcContent>>           GetTopAsync(string contentType, int page);
    Task<UgcContent?>                GetByIdAsync(long contentId);
    Task<List<UgcContent>>           GetMineAsync(long charId);
    Task<(bool Ok, string Msg, long Id)> CreateAsync(long charId, string contentType, string title, string dataJson, string lang);
    Task<(bool Ok, string Msg)>      PublishAsync(long charId, long contentId, string lang);
    Task<(bool Ok, string Msg)>      VisitAsync(long charId, long contentId, string lang);
    Task<(bool Ok, string Msg)>      RateAsync(long charId, long contentId, int stars, string lang);
    Task<(bool Ok, string Msg)>      DeleteAsync(long charId, long contentId, string lang);
}

public class UgcService(
    GameDbContext db, ILocalizationService loc,
    ILogger<UgcService> logger) : IUgcService
{
    private static readonly string[] AllowedTypes =
        ["house", "garden", "maze", "park", "art_gallery", "museum"];

    public async Task<List<UgcContent>> GetTopAsync(string contentType, int page) =>
        await db.UgcContents
            .Where(c => c.ContentType == contentType && c.IsPublished)
            .OrderByDescending(c => c.Rating)
            .ThenByDescending(c => c.VisitCount)
            .Skip((page - 1) * 20).Take(20)
            .Include(c => c.Creator)
            .ToListAsync();

    public async Task<UgcContent?> GetByIdAsync(long contentId) =>
        await db.UgcContents.Include(c => c.Creator)
            .FirstOrDefaultAsync(c => c.Id == contentId);

    public async Task<List<UgcContent>> GetMineAsync(long charId) =>
        await db.UgcContents.Where(c => c.CreatorId == charId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<(bool, string, long)> CreateAsync(
        long charId, string contentType, string title, string dataJson, string lang)
    {
        if (!AllowedTypes.Contains(contentType))
            return (false, loc.Get("error.bad_request", lang), 0);

        var content = new UgcContent
        {
            CreatorId   = charId,
            ContentType = contentType,
            Title       = title.Trim(),
            DataJson    = dataJson,
            IsPublished = false,
            CreatedAt   = DateTime.UtcNow,
        };
        db.UgcContents.Add(content);
        await db.SaveChangesAsync();

        logger.LogInformation("UGC created: type={T} charId={C}", contentType, charId);
        return (true, "OK", content.Id);
    }

    public async Task<(bool, string)> PublishAsync(long charId, long contentId, string lang)
    {
        var content = await db.UgcContents
            .FirstOrDefaultAsync(c => c.Id == contentId && c.CreatorId == charId);
        if (content is null) return (false, loc.Get("error.not_found", lang));

        content.IsPublished = true;
        content.PublishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> VisitAsync(long charId, long contentId, string lang)
    {
        var content = await db.UgcContents
            .FirstOrDefaultAsync(c => c.Id == contentId && c.IsPublished);
        if (content is null) return (false, loc.Get("error.not_found", lang));

        content.VisitCount++;

        // Reward creator per visit (tiny amount)
        if (content.CreatorId != charId)
            await db.Characters.Where(c => c.Id == content.CreatorId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Gold, c => c.Gold + 1));

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> RateAsync(
        long charId, long contentId, int stars, string lang)
    {
        stars = Math.Clamp(stars, 1, 5);
        var content = await db.UgcContents
            .FirstOrDefaultAsync(c => c.Id == contentId && c.IsPublished);
        if (content is null) return (false, loc.Get("error.not_found", lang));
        if (content.CreatorId == charId) return (false, loc.Get("error.bad_request", lang));

        var existing = await db.UgcRatings
            .FirstOrDefaultAsync(r => r.ContentId == contentId && r.RaterId == charId);
        if (existing is not null) { existing.Stars = stars; }
        else db.UgcRatings.Add(new UgcRating
            { ContentId = contentId, RaterId = charId, Stars = stars });

        // Recalculate average
        var avg = await db.UgcRatings
            .Where(r => r.ContentId == contentId)
            .AverageAsync(r => (double)r.Stars);
        content.Rating     = (float)avg;
        content.RatingCount = await db.UgcRatings.CountAsync(r => r.ContentId == contentId);

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> DeleteAsync(long charId, long contentId, string lang)
    {
        var content = await db.UgcContents
            .FirstOrDefaultAsync(c => c.Id == contentId && c.CreatorId == charId);
        if (content is null) return (false, loc.Get("error.not_found", lang));

        db.UgcContents.Remove(content);
        await db.SaveChangesAsync();
        return (true, "OK");
    }
}
