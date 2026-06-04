// FantasyWorld.Server/Services/Gameplay/ProfessionDeityService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;

namespace FantasyWorld.Server.Services.Gameplay;

// ─── Guild Territory ─────────────────────────────────────────

public interface IGuildTerritoryService
{
    Task<List<GuildTerritory>>           GetAllAsync();
    Task<GuildTerritory?>                GetByClanAsync(int clanId);
    Task<(bool Ok, string Msg)>          FoundTerritoryAsync(long charId, int clanId, string name, int mapId, string lang);
    Task<(bool Ok, string Msg)>          UpgradeAsync(long charId, int clanId, string lang);
    Task<(bool Ok, string Msg)>          BuildAsync(long charId, int clanId, string buildingType, string lang);
    Task<(bool Ok, string Msg)>          CollectTaxAsync(long charId, int clanId, string lang);
}

public class GuildTerritoryService(
    GameDbContext db, ILocalizationService loc,
    ILogger<GuildTerritoryService> logger) : IGuildTerritoryService
{
    // Level tiers
    private static readonly string[] Tiers = ["hamlet", "village", "town", "city", "kingdom"];
    // Upgrade costs per tier
    private static readonly int[] UpgradeCosts = [5_000, 20_000, 100_000, 500_000, 0];
    private static readonly int[] MaxBuildings  = [2, 4, 8, 12, 20];

    public async Task<List<GuildTerritory>> GetAllAsync() =>
        await db.GuildTerritories.Include(t => t.Clan).ToListAsync();

    public async Task<GuildTerritory?> GetByClanAsync(int clanId) =>
        await db.GuildTerritories
            .Include(t => t.Clan)
            .Include(t => t.Buildings)
            .FirstOrDefaultAsync(t => t.ClanId == clanId);

    public async Task<(bool, string)> FoundTerritoryAsync(
        long charId, int clanId, string name, int mapId, string lang)
    {
        var clan = await db.Clans.FindAsync(clanId);
        if (clan?.LeaderId != charId) return (false, loc.Get("error.forbidden", lang));

        if (await db.GuildTerritories.AnyAsync(t => t.ClanId == clanId))
            return (false, loc.Get("error.bad_request", lang));

        const int foundCost = 10_000;
        if (clan.Gold < foundCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = foundCost, have = clan.Gold }));

        clan.Gold -= foundCost;

        db.GuildTerritories.Add(new GuildTerritory
        {
            ClanId   = clanId,
            Name     = name.Trim(),
            MapId    = mapId,
            Tier     = 0,
            TaxPool  = 0,
        });
        await db.SaveChangesAsync();

        logger.LogInformation("Territory founded: clan={C} name={N}", clanId, name);
        return (true, "OK");
    }

    public async Task<(bool, string)> UpgradeAsync(long charId, int clanId, string lang)
    {
        var territory = await db.GuildTerritories
            .Include(t => t.Clan)
            .FirstOrDefaultAsync(t => t.ClanId == clanId);

        if (territory is null) return (false, loc.Get("error.not_found", lang));
        if (territory.Clan.LeaderId != charId) return (false, loc.Get("error.forbidden", lang));
        if (territory.Tier >= 4) return (false, loc.Get("error.bad_request", lang));

        var cost = UpgradeCosts[territory.Tier];
        if (territory.Clan.Gold < cost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = cost, have = territory.Clan.Gold }));

        territory.Clan.Gold -= cost;
        territory.Tier++;
        territory.TierName = Tiers[territory.Tier];

        logger.LogInformation("Territory upgraded: clan={C} tier={T}", clanId, territory.Tier);
        await db.SaveChangesAsync();
        return (true, loc.Get("territory.upgraded", lang, new { tier = territory.TierName }));
    }

    public async Task<(bool, string)> BuildAsync(
        long charId, int clanId, string buildingType, string lang)
    {
        var territory = await db.GuildTerritories
            .Include(t => t.Clan)
            .Include(t => t.Buildings)
            .FirstOrDefaultAsync(t => t.ClanId == clanId);

        if (territory is null) return (false, loc.Get("error.not_found", lang));
        if (territory.Clan.LeaderId != charId) return (false, loc.Get("error.forbidden", lang));

        if (territory.Buildings.Count >= MaxBuildings[territory.Tier])
            return (false, loc.Get("error.bad_request", lang));

        var buildCosts = new Dictionary<string, int>
        {
            ["barracks"] = 3_000, ["market"] = 5_000, ["temple"] = 8_000,
            ["library"]  = 6_000, ["forge"]  = 4_000, ["farm"]   = 2_000,
            ["guild_hall"] = 10_000, ["treasury"] = 15_000,
        };

        if (!buildCosts.TryGetValue(buildingType, out var cost))
            return (false, loc.Get("error.bad_request", lang));

        if (territory.Clan.Gold < cost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = cost, have = territory.Clan.Gold }));

        territory.Clan.Gold -= cost;
        db.TerritoryBuildings.Add(new TerritoryBuilding
        {
            TerritoryId  = territory.Id,
            BuildingType = buildingType,
            Level        = 1,
            BuiltAt      = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> CollectTaxAsync(long charId, int clanId, string lang)
    {
        var territory = await db.GuildTerritories
            .Include(t => t.Clan)
            .FirstOrDefaultAsync(t => t.ClanId == clanId);

        if (territory is null) return (false, loc.Get("error.not_found", lang));
        if (territory.Clan.LeaderId != charId) return (false, loc.Get("error.forbidden", lang));
        if (territory.TaxPool <= 0) return (false, loc.Get("error.bad_request", lang));

        territory.Clan.Gold += territory.TaxPool;
        var collected = territory.TaxPool;
        territory.TaxPool   = 0;
        territory.LastTaxAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (true, loc.Get("territory.tax_collected", lang, new { amount = collected }));
    }
}

// ─── Rare Profession ─────────────────────────────────────────

public interface IRareProfessionService
{
    Task<List<RareProfession>>           GetAllAsync();
    Task<List<CharacterRareProfession>>  GetMyProfessionsAsync(long charId);
    Task<(bool Ok, string Msg)>          ApplyAsync(long charId, int professionId, string lang);
    Task<(bool Ok, string Msg)>          AbandonAsync(long charId, int professionId, string lang);
}

public class RareProfessionService(
    GameDbContext db, ILocalizationService loc) : IRareProfessionService
{
    public async Task<List<RareProfession>> GetAllAsync() =>
        await db.RareProfessions.ToListAsync();

    public async Task<List<CharacterRareProfession>> GetMyProfessionsAsync(long charId) =>
        await db.CharacterRareProfessions
            .Where(p => p.CharacterId == charId)
            .Include(p => p.Profession)
            .ToListAsync();

    public async Task<(bool, string)> ApplyAsync(long charId, int professionId, string lang)
    {
        var prof = await db.RareProfessions.FindAsync(professionId);
        if (prof is null) return (false, loc.Get("error.not_found", lang));

        // Check server slot limit
        var currentCount = await db.CharacterRareProfessions
            .CountAsync(p => p.ProfessionId == professionId && p.IsActive);
        if (currentCount >= prof.ServerSlotLimit)
            return (false, loc.Get("rare_profession.full", lang, new { name = prof.Name }));

        // Check prerequisites
        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Level < prof.RequiredLevel)
            return (false, loc.Get("error.bad_request", lang));

        if (!string.IsNullOrEmpty(prof.RequiredConditionJson))
        {
            var conditions = JsonSerializer.Deserialize<Dictionary<string, int>>(prof.RequiredConditionJson) ?? [];
            // Quest, reputation, etc. checks would go here
        }

        var already = await db.CharacterRareProfessions
            .AnyAsync(p => p.CharacterId == charId && p.ProfessionId == professionId);
        if (already) return (false, loc.Get("error.bad_request", lang));

        db.CharacterRareProfessions.Add(new CharacterRareProfession
        {
            CharacterId  = charId,
            ProfessionId = professionId,
            IsActive     = true,
            ObtainedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return (true, loc.Get("rare_profession.obtained", lang, new { name = prof.Name }));
    }

    public async Task<(bool, string)> AbandonAsync(long charId, int professionId, string lang)
    {
        var cp = await db.CharacterRareProfessions
            .FirstOrDefaultAsync(p => p.CharacterId == charId && p.ProfessionId == professionId);
        if (cp is null) return (false, loc.Get("error.not_found", lang));

        cp.IsActive = false;
        await db.SaveChangesAsync();
        return (true, "OK");
    }
}

// ─── Deity ───────────────────────────────────────────────────

public interface IDeityService
{
    Task<List<Deity>>                GetAllAsync();
    Task<(bool Ok, string Msg)>      ChooseDeityAsync(long charId, int deityId, string lang);
    Task<(bool Ok, string Msg)>      PrayAsync(long charId, string lang);
    Task<CharacterDeity?>            GetMyDeityAsync(long charId);
    Task<List<DeityQuest>>           GetDeityQuestsAsync(int deityId, long charId);
    Task<(bool Ok, string Msg)>      OfferAsync(long charId, int itemId, int qty, string lang);
}

public class DeityService(
    GameDbContext db, ILocalizationService loc,
    ILogger<DeityService> logger) : IDeityService
{
    public async Task<List<Deity>> GetAllAsync() =>
        await db.Deities.ToListAsync();

    public async Task<(bool, string)> ChooseDeityAsync(long charId, int deityId, string lang)
    {
        var deity = await db.Deities.FindAsync(deityId);
        if (deity is null) return (false, loc.Get("error.not_found", lang));

        var already = await db.CharacterDeities.AnyAsync(d => d.CharacterId == charId);
        if (already) return (false, loc.Get("error.bad_request", lang));

        db.CharacterDeities.Add(new CharacterDeity
        {
            CharacterId   = charId,
            DeityId       = deityId,
            Devotion      = 0,
            Tier          = "follower",
            ChosenAt      = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        logger.LogInformation("CharId={C} chose deity={D}", charId, deity.Name);
        return (true, loc.Get("deity.chosen", lang, new { name = deity.Name }));
    }

    public async Task<(bool, string)> PrayAsync(long charId, string lang)
    {
        var cd = await db.CharacterDeities
            .Include(d => d.Deity)
            .FirstOrDefaultAsync(d => d.CharacterId == charId);
        if (cd is null) return (false, loc.Get("error.not_found", lang));

        // Cooldown: once per real hour
        if (cd.LastPrayedAt.HasValue
            && cd.LastPrayedAt.Value.AddHours(1) > DateTime.UtcNow)
            return (false, loc.Get("error.bad_request", lang));

        cd.Devotion    += 10;
        cd.LastPrayedAt = DateTime.UtcNow;

        // Tier up
        cd.Tier = cd.Devotion switch
        {
            >= 10_000 => "saint",
            >= 5_000  => "devotee",
            >= 1_000  => "believer",
            _         => "follower",
        };

        await db.SaveChangesAsync();
        return (true, loc.Get("deity.prayed", lang, new { devotion = cd.Devotion }));
    }

    public async Task<CharacterDeity?> GetMyDeityAsync(long charId) =>
        await db.CharacterDeities
            .Include(d => d.Deity)
            .FirstOrDefaultAsync(d => d.CharacterId == charId);

    public async Task<List<DeityQuest>> GetDeityQuestsAsync(int deityId, long charId)
    {
        var cd = await db.CharacterDeities
            .FirstOrDefaultAsync(d => d.CharacterId == charId && d.DeityId == deityId);
        if (cd is null) return [];

        return await db.DeityQuests
            .Where(q => q.DeityId == deityId && q.MinTier == cd.Tier)
            .ToListAsync();
    }

    public async Task<(bool, string)> OfferAsync(long charId, int itemId, int qty, string lang)
    {
        var cd = await db.CharacterDeities.FirstOrDefaultAsync(d => d.CharacterId == charId);
        if (cd is null) return (false, loc.Get("error.not_found", lang));

        var inv = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == itemId && i.Quantity >= qty);
        if (inv is null) return (false, loc.Get("inventory.item_not_found", lang));

        inv.Quantity -= qty;
        if (inv.Quantity == 0) db.Inventories.Remove(inv);

        var devGained = qty * 5;
        cd.Devotion += devGained;

        await db.SaveChangesAsync();
        return (true, loc.Get("deity.offered", lang, new { devotion = devGained }));
    }
}

// ─── Civil Profession ────────────────────────────────────────

public interface ICivilProfessionService
{
    Task<List<CivilProfession>>       GetAllAsync();
    Task<List<CharacterCivilProfession>> GetMyProfessionsAsync(long charId);
    Task<(bool Ok, string Msg)>       UnlockAsync(long charId, int professionId, string lang);
    Task<(bool Ok, string Msg)>       CreateWorkAsync(long charId, int professionId, string title, string content, int price, string lang);
    Task<(bool Ok, string Msg)>       PracticeAsync(long charId, int professionId, string lang);
    Task<List<PlayerWork>>            GetWorksAsync(string workType, int page);
    Task<(bool Ok, string Msg)>       BuyWorkAsync(long charId, long workId, string lang);
}

public class CivilProfessionService(
    GameDbContext db, ILocalizationService loc,
    ILogger<CivilProfessionService> logger) : ICivilProfessionService
{
    public async Task<List<CivilProfession>> GetAllAsync() =>
        await db.CivilProfessions.ToListAsync();

    public async Task<List<CharacterCivilProfession>> GetMyProfessionsAsync(long charId) =>
        await db.CharacterCivilProfessions
            .Where(p => p.CharacterId == charId)
            .Include(p => p.Profession)
            .ToListAsync();

    public async Task<(bool, string)> UnlockAsync(long charId, int professionId, string lang)
    {
        var prof = await db.CivilProfessions.FindAsync(professionId);
        if (prof is null) return (false, loc.Get("error.not_found", lang));

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Level < prof.RequiredLevel || char_.Gold < prof.UnlockCost)
            return (false, loc.Get("error.bad_request", lang));

        if (await db.CharacterCivilProfessions.AnyAsync(p =>
            p.CharacterId == charId && p.ProfessionId == professionId))
            return (false, loc.Get("error.bad_request", lang));

        char_.Gold -= prof.UnlockCost;

        db.CharacterCivilProfessions.Add(new CharacterCivilProfession
        {
            CharacterId  = charId,
            ProfessionId = professionId,
            Mastery      = 0,
            UnlockedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        logger.LogInformation("CivilProfession unlocked: charId={C} prof={P}", charId, prof.Name);
        return (true, loc.Get("profession.unlocked", lang, new { name = prof.Name }));
    }

    public async Task<(bool, string)> CreateWorkAsync(
        long charId, int professionId, string title, string content, int price, string lang)
    {
        var cp = await db.CharacterCivilProfessions
            .FirstOrDefaultAsync(p => p.CharacterId == charId && p.ProfessionId == professionId);
        if (cp is null) return (false, loc.Get("error.forbidden", lang));

        var prof = await db.CivilProfessions.FindAsync(professionId);

        db.PlayerWorks.Add(new PlayerWork
        {
            CreatorId    = charId,
            ProfessionId = professionId,
            Title        = title.Trim(),
            Content      = content,
            WorkType     = prof!.WorkType,
            Price        = price,
            IsPublished  = true,
        });

        // Gain mastery
        cp.Mastery = Math.Min(100, cp.Mastery + 2);

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> PracticeAsync(long charId, int professionId, string lang)
    {
        var cp = await db.CharacterCivilProfessions
            .Include(p => p.Profession)
            .FirstOrDefaultAsync(p => p.CharacterId == charId && p.ProfessionId == professionId);
        if (cp is null) return (false, loc.Get("error.forbidden", lang));

        if (cp.LastPracticeAt.HasValue
            && cp.LastPracticeAt.Value.AddMinutes(30) > DateTime.UtcNow)
            return (false, loc.Get("error.bad_request", lang));

        cp.Mastery         = Math.Min(100, cp.Mastery + 1);
        cp.LastPracticeAt  = DateTime.UtcNow;
        cp.TotalPractices++;

        // EXP and gold based on mastery
        var expGain  = cp.Mastery / 10 + 5;
        var goldGain = cp.Mastery / 5;

        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Exp,  c => c.Exp  + expGain)
                .SetProperty(c => c.Gold, c => c.Gold + goldGain));

        await db.SaveChangesAsync();
        return (true, loc.Get("profession.practiced", lang,
            new { mastery = cp.Mastery, exp = expGain, gold = goldGain }));
    }

    public async Task<List<PlayerWork>> GetWorksAsync(string workType, int page) =>
        await db.PlayerWorks
            .Where(w => w.WorkType == workType && w.IsPublished)
            .Include(w => w.Creator)
            .OrderByDescending(w => w.Likes)
            .ThenByDescending(w => w.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .ToListAsync();

    public async Task<(bool, string)> BuyWorkAsync(long charId, long workId, string lang)
    {
        var work = await db.PlayerWorks
            .Include(w => w.Creator)
            .FirstOrDefaultAsync(w => w.Id == workId && w.IsPublished && w.Price > 0);
        if (work is null) return (false, loc.Get("error.not_found", lang));
        if (work.CreatorId == charId) return (false, loc.Get("error.bad_request", lang));

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < work.Price)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = work.Price, have = char_.Gold }));

        char_.Gold -= work.Price;

        // 80% to creator
        var creatorShare = (long)(work.Price * 0.8);
        await db.Characters.Where(c => c.Id == work.CreatorId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Gold, c => c.Gold + creatorShare));

        work.Views++;

        await db.SaveChangesAsync();
        return (true, "OK");
    }
}
