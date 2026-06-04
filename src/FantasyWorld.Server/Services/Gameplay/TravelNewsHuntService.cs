// FantasyWorld.Server/Services/Gameplay/TravelService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;

namespace FantasyWorld.Server.Services.Gameplay;

// ─── Travel ──────────────────────────────────────────────────

public interface ITravelService
{
    Task<List<Landmark>>             GetLandmarksAsync(int? mapId);
    Task<(bool Ok, string Msg)>      VisitLandmarkAsync(long charId, int landmarkId, string lang);
    Task<List<TravelPassportStamp>>  GetMyStampsAsync(long charId);
    Task<List<RegionalSpecialty>>    GetSpecialtiesAsync(int regionId);
    Task<TravelPassport?>            GetPassportAsync(long charId);
}

public class TravelService(
    GameDbContext db, ILocalizationService loc) : ITravelService
{
    public async Task<List<Landmark>> GetLandmarksAsync(int? mapId)
    {
        var q = db.Landmarks.AsQueryable();
        if (mapId.HasValue) q = q.Where(l => l.MapId == mapId.Value);
        return await q.ToListAsync();
    }

    public async Task<(bool, string)> VisitLandmarkAsync(long charId, int landmarkId, string lang)
    {
        var landmark = await db.Landmarks.FindAsync(landmarkId);
        if (landmark is null) return (false, loc.Get("error.not_found", lang));

        var alreadyVisited = await db.TravelPassportStamps
            .AnyAsync(s => s.CharacterId == charId && s.LandmarkId == landmarkId);
        if (alreadyVisited) return (false, loc.Get("error.bad_request", lang));

        // Ensure passport exists
        var passport = await db.TravelPassports.FindAsync(charId);
        if (passport is null)
        {
            passport = new TravelPassport { CharacterId = charId, StampCount = 0 };
            db.TravelPassports.Add(passport);
        }
        passport.StampCount++;

        db.TravelPassportStamps.Add(new TravelPassportStamp
        {
            CharacterId  = charId,
            LandmarkId   = landmarkId,
            RegionName   = landmark.RegionName,
            VisitedAt    = DateTime.UtcNow,
        });

        // Reward: EXP + gold
        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Exp,  c => c.Exp  + landmark.ExpReward)
                .SetProperty(c => c.Gold, c => c.Gold + landmark.GoldReward));

        await db.SaveChangesAsync();
        return (true, loc.Get("travel.stamp_collected", lang,
            new { name = landmark.Name, region = landmark.RegionName }));
    }

    public async Task<List<TravelPassportStamp>> GetMyStampsAsync(long charId) =>
        await db.TravelPassportStamps
            .Where(s => s.CharacterId == charId)
            .Include(s => s.Landmark)
            .OrderBy(s => s.VisitedAt)
            .ToListAsync();

    public async Task<List<RegionalSpecialty>> GetSpecialtiesAsync(int regionId) =>
        await db.RegionalSpecialties
            .Where(s => s.RegionId == regionId)
            .Include(s => s.Item)
            .ToListAsync();

    public async Task<TravelPassport?> GetPassportAsync(long charId) =>
        await db.TravelPassports.FindAsync(charId);
}

// ─── News / Bulletin Board ───────────────────────────────────

public interface INewsService
{
    Task<List<NewsArticle>>         GetBulletinAsync(int page);
    Task<List<NewsArticle>>         GetMyArticlesAsync(long charId);
    Task<(bool Ok, string Msg, long Id)> PublishAsync(long charId, string title, string content, string lang);
    Task<(bool Ok, string Msg)>     LikeAsync(long charId, long articleId, string lang);
    Task<(bool Ok, string Msg)>     PinAsync(long gmId, long articleId, bool pin, string lang);
    Task<(bool Ok, string Msg)>     DeleteAsync(long charId, long articleId, string lang);
}

public class NewsService(
    GameDbContext db, ILocalizationService loc,
    ILogger<NewsService> logger) : INewsService
{
    public async Task<List<NewsArticle>> GetBulletinAsync(int page) =>
        await db.NewsArticles
            .Include(a => a.Author)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishedAt)
            .Skip((page - 1) * 20).Take(20)
            .ToListAsync();

    public async Task<List<NewsArticle>> GetMyArticlesAsync(long charId) =>
        await db.NewsArticles.Where(a => a.AuthorId == charId)
            .OrderByDescending(a => a.PublishedAt)
            .ToListAsync();

    public async Task<(bool, string, long)> PublishAsync(
        long charId, string title, string content, string lang)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 100)
            return (false, loc.Get("error.bad_request", lang), 0);

        if (string.IsNullOrWhiteSpace(content) || content.Length > 5000)
            return (false, loc.Get("error.bad_request", lang), 0);

        var article = new NewsArticle
        {
            AuthorId    = charId,
            Title       = title.Trim(),
            Content     = content.Trim(),
            PublishedAt = DateTime.UtcNow,
        };
        db.NewsArticles.Add(article);
        await db.SaveChangesAsync();

        logger.LogInformation("News published: charId={C} title={T}", charId, title);
        return (true, "OK", article.Id);
    }

    public async Task<(bool, string)> LikeAsync(long charId, long articleId, string lang)
    {
        var art = await db.NewsArticles.FindAsync(articleId);
        if (art is null) return (false, loc.Get("error.not_found", lang));
        art.Likes++;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> PinAsync(long gmId, long articleId, bool pin, string lang)
    {
        var art = await db.NewsArticles.FindAsync(articleId);
        if (art is null) return (false, loc.Get("error.not_found", lang));
        art.IsPinned = pin;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> DeleteAsync(long charId, long articleId, string lang)
    {
        var art = await db.NewsArticles
            .FirstOrDefaultAsync(a => a.Id == articleId && a.AuthorId == charId);
        if (art is null) return (false, loc.Get("error.not_found", lang));
        db.NewsArticles.Remove(art);
        await db.SaveChangesAsync();
        return (true, "OK");
    }
}

// ─── Server Treasure Hunt ────────────────────────────────────

public interface IServerTreasureHuntService
{
    Task<List<TreasureHunt>>            GetActiveAsync();
    Task<TreasureHunt?>                 GetByIdAsync(int huntId);
    Task<(bool Ok, string Msg, int HuntId)> CreateHuntAsync(long gmId, string title, string clue1, string clue2, string clue3, int mapId, float x, float y, int goldReward, string lang);
    Task<(bool Ok, string Msg)>         SubmitLocationAsync(long charId, int huntId, float x, float y, string lang);
    Task<(bool Ok, string Msg)>         ClaimTreasureAsync(long charId, int huntId, string lang);
}

public class ServerTreasureHuntService(
    GameDbContext db, ILocalizationService loc,
    ILogger<ServerTreasureHuntService> logger) : IServerTreasureHuntService
{
    private const float ClaimRadius = 3.0f;   // studs

    public async Task<List<TreasureHunt>> GetActiveAsync() =>
        await db.TreasureHunts
            .Where(h => h.Status == "active")
            .Include(h => h.Creator)
            .ToListAsync();

    public async Task<TreasureHunt?> GetByIdAsync(int huntId) =>
        await db.TreasureHunts.Include(h => h.Creator)
            .FirstOrDefaultAsync(h => h.Id == huntId);

    public async Task<(bool, string, int)> CreateHuntAsync(
        long gmId, string title, string clue1, string clue2, string clue3,
        int mapId, float x, float y, int goldReward, string lang)
    {
        var hunt = new TreasureHunt
        {
            CreatorId   = gmId,
            Title       = title.Trim(),
            Clue1       = clue1, Clue2 = clue2, Clue3 = clue3,
            MapId       = mapId, TreasureX = x, TreasureY = y,
            GoldReward  = goldReward,
            Status      = "active",
            CreatedAt   = DateTime.UtcNow,
        };
        db.TreasureHunts.Add(hunt);
        await db.SaveChangesAsync();

        logger.LogInformation("Treasure hunt created: id={Id} by gm={Gm}", hunt.Id, gmId);
        return (true, "OK", hunt.Id);
    }

    public async Task<(bool, string)> SubmitLocationAsync(
        long charId, int huntId, float x, float y, string lang)
    {
        var hunt = await db.TreasureHunts.FindAsync(huntId);
        if (hunt?.Status != "active") return (false, loc.Get("error.not_found", lang));

        var dist = MathF.Sqrt(MathF.Pow(x - hunt.TreasureX, 2) + MathF.Pow(y - hunt.TreasureY, 2));

        string hint;
        if (dist <= ClaimRadius)
        {
            return await ClaimTreasureAsync(charId, huntId, lang);
        }
        else if (dist <= 20) hint = loc.Get("treasure.very_close", lang);
        else if (dist <= 50) hint = loc.Get("treasure.close", lang);
        else if (dist <= 100) hint = loc.Get("treasure.warm", lang);
        else hint = loc.Get("treasure.far", lang);

        return (false, hint);
    }

    public async Task<(bool, string)> ClaimTreasureAsync(long charId, int huntId, string lang)
    {
        var hunt = await db.TreasureHunts.FindAsync(huntId);
        if (hunt?.Status != "active") return (false, loc.Get("error.not_found", lang));

        hunt.Status    = "found";
        hunt.FinderId  = charId;
        hunt.FoundAt   = DateTime.UtcNow;

        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + hunt.GoldReward));

        await db.SaveChangesAsync();

        var char_ = await db.Characters.FindAsync(charId);
        logger.LogInformation("Treasure found: huntId={H} by charId={C} ({Name})",
            huntId, charId, char_?.Name);

        return (true, loc.Get("treasure.found", lang, new { reward = hunt.GoldReward }));
    }
}
