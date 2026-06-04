// FantasyWorld.Server/Services/Gameplay/FactionService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Gameplay;

public interface IFactionService
{
    Task<List<Faction>>              GetAllAsync();
    Task<(bool Ok, string Msg)>      JoinAsync(long charId, int factionId, string lang);
    Task<(bool Ok, string Msg)>      LeaveAsync(long charId, string lang);
    Task<CharacterFaction?>          GetMyFactionAsync(long charId);
    Task<(bool Ok, string Msg)>      AddContributionAsync(long charId, int points);
    Task<List<FactionQuest>>         GetQuestsAsync(int factionId, long charId);
    Task<(bool Ok, string Msg)>      AcceptFactionQuestAsync(long charId, int questId, string lang);
    Task<(bool Ok, string Msg)>      CompleteFactionQuestAsync(long charId, int questId, string lang);
    Task<List<FactionShopItem>>      GetShopAsync(int factionId, long charId);
    Task<(bool Ok, string Msg)>      BuyFromFactionShopAsync(long charId, int shopItemId, string lang);
    Task<FactionWar?>                GetActiveWarAsync();
    Task<(bool Ok, string Msg)>      ContributeToWarAsync(long charId, int warId, int amount, string lang);
    Task                             ProcessWarAsync(); // cron
}

public class FactionService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<FactionService> logger) : IFactionService
{
    public async Task<List<Faction>> GetAllAsync() =>
        await db.Factions.ToListAsync();

    // ─── Join ────────────────────────────────────────────────
    public async Task<(bool, string)> JoinAsync(long charId, int factionId, string lang)
    {
        var already = await db.CharacterFactions.AnyAsync(cf => cf.CharacterId == charId);
        if (already) return (false, loc.Get("error.bad_request", lang));

        var faction = await db.Factions.FindAsync(factionId);
        if (faction is null) return (false, loc.Get("error.not_found", lang));

        db.CharacterFactions.Add(new CharacterFaction
        {
            CharacterId = charId,
            FactionId   = factionId,
            Rank        = 1,
        });
        await db.SaveChangesAsync();

        logger.LogInformation("CharId={C} joined faction={F}", charId, faction.Name);
        return (true, loc.Get("faction.joined", lang, new { name = faction.Name }));
    }

    // ─── Leave ───────────────────────────────────────────────
    public async Task<(bool, string)> LeaveAsync(long charId, string lang)
    {
        var cf = await db.CharacterFactions
            .FirstOrDefaultAsync(c => c.CharacterId == charId);
        if (cf is null) return (false, loc.Get("error.not_found", lang));

        db.CharacterFactions.Remove(cf);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<CharacterFaction?> GetMyFactionAsync(long charId) =>
        await db.CharacterFactions
            .Include(cf => cf.Faction)
            .FirstOrDefaultAsync(cf => cf.CharacterId == charId);

    public async Task<(bool, string)> AddContributionAsync(long charId, int points)
    {
        await db.CharacterFactions
            .Where(cf => cf.CharacterId == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(cf => cf.Contribution, cf => cf.Contribution + points));

        // Auto rank up: rank = contribution / 1000, max 10
        var cf = await db.CharacterFactions.FirstOrDefaultAsync(c => c.CharacterId == charId);
        if (cf is not null)
        {
            var newRank = Math.Min(10, cf.Contribution / 1000 + 1);
            if (newRank > cf.Rank)
            {
                cf.Rank = newRank;
                await db.SaveChangesAsync();
            }
        }
        return (true, "OK");
    }

    // ─── Faction quests ──────────────────────────────────────
    public async Task<List<FactionQuest>> GetQuestsAsync(int factionId, long charId)
    {
        var myFaction = await db.CharacterFactions
            .FirstOrDefaultAsync(cf => cf.CharacterId == charId);
        if (myFaction?.FactionId != factionId) return [];

        return await db.FactionQuests
            .Where(q => q.FactionId == factionId && q.MinRank <= myFaction.Rank)
            .ToListAsync();
    }

    public async Task<(bool, string)> AcceptFactionQuestAsync(
        long charId, int questId, string lang)
    {
        var myFaction = await db.CharacterFactions
            .FirstOrDefaultAsync(cf => cf.CharacterId == charId);
        if (myFaction is null) return (false, loc.Get("error.forbidden", lang));

        var quest = await db.FactionQuests.FindAsync(questId);
        if (quest is null || quest.FactionId != myFaction.FactionId)
            return (false, loc.Get("error.not_found", lang));

        if (myFaction.Rank < quest.MinRank)
            return (false, loc.Get("error.forbidden", lang));

        var exists = await db.CharacterFactionQuests
            .AnyAsync(q => q.CharacterId == charId && q.FactionQuestId == questId
                        && q.Status == "active");
        if (exists) return (false, loc.Get("error.bad_request", lang));

        db.CharacterFactionQuests.Add(new CharacterFactionQuest
        {
            CharacterId     = charId,
            FactionQuestId  = questId,
            Status          = "active",
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> CompleteFactionQuestAsync(
        long charId, int questId, string lang)
    {
        var cfq = await db.CharacterFactionQuests
            .Include(q => q.FactionQuest)
            .FirstOrDefaultAsync(q => q.CharacterId == charId
                && q.FactionQuestId == questId && q.Status == "active");
        if (cfq is null) return (false, loc.Get("error.not_found", lang));

        cfq.Status      = "completed";
        cfq.CompletedAt = DateTime.UtcNow;

        // Apply rewards
        var rewardJson = cfq.FactionQuest.RewardJson;
        if (rewardJson is not null)
        {
            var reward = JsonSerializer.Deserialize<Dictionary<string, int>>(rewardJson) ?? [];
            if (reward.TryGetValue("gold", out var gold))
                await db.Characters.Where(c => c.Id == charId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Gold, c => c.Gold + gold));
            if (reward.TryGetValue("contribution", out var contrib))
                await AddContributionAsync(charId, contrib);
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Faction shop ────────────────────────────────────────
    public async Task<List<FactionShopItem>> GetShopAsync(int factionId, long charId)
    {
        var myFaction = await db.CharacterFactions
            .FirstOrDefaultAsync(cf => cf.CharacterId == charId);
        if (myFaction?.FactionId != factionId) return [];

        return await db.FactionShopItems
            .Where(s => s.FactionId == factionId && s.MinRank <= myFaction.Rank)
            .Include(s => s.Item)
            .ToListAsync();
    }

    public async Task<(bool, string)> BuyFromFactionShopAsync(
        long charId, int shopItemId, string lang)
    {
        var shopItem = await db.FactionShopItems
            .Include(s => s.Item)
            .FirstOrDefaultAsync(s => s.Id == shopItemId);
        if (shopItem is null) return (false, loc.Get("error.not_found", lang));

        var myFaction = await db.CharacterFactions
            .FirstOrDefaultAsync(cf => cf.CharacterId == charId);
        if (myFaction?.FactionId != shopItem.FactionId || myFaction.Rank < shopItem.MinRank)
            return (false, loc.Get("error.forbidden", lang));

        // Check stock
        if (shopItem.Stock.HasValue && shopItem.Stock <= 0)
            return (false, loc.Get("shop.out_of_stock", lang));

        var char_ = await db.Characters.FindAsync(charId)!;
        if (shopItem.PriceGold > 0)
        {
            if (char_!.Gold < shopItem.PriceGold)
                return (false, loc.Get("character.insufficient_gold", lang,
                    new { need = shopItem.PriceGold, have = char_.Gold }));
            char_.Gold -= shopItem.PriceGold;
        }
        else if (shopItem.PriceDiamond > 0)
        {
            if (char_!.Diamond < shopItem.PriceDiamond)
                return (false, loc.Get("character.insufficient_diamond", lang));
            char_.Diamond -= shopItem.PriceDiamond;
        }

        if (shopItem.Stock.HasValue) shopItem.Stock--;

        // Add to inventory
        var maxSlot = await db.Inventories
            .Where(i => i.CharacterId == charId).MaxAsync(i => (int?)i.Slot) ?? -1;
        db.Inventories.Add(new InventoryItem
        {
            CharacterId = charId, ItemId = shopItem.ItemId,
            Quantity = 1, Slot = maxSlot + 1,
        });

        await db.SaveChangesAsync();
        return (true, loc.Get("shop.buy_success", lang,
            new { item = shopItem.Item.Name, qty = 1, price = shopItem.PriceGold }));
    }

    // ─── Faction war ─────────────────────────────────────────
    public async Task<FactionWar?> GetActiveWarAsync() =>
        await db.FactionWars
            .Include(w => w.Attacker).Include(w => w.Defender)
            .FirstOrDefaultAsync(w => w.Status == "ongoing");

    public async Task<(bool, string)> ContributeToWarAsync(
        long charId, int warId, int amount, string lang)
    {
        var war = await db.FactionWars.FindAsync(warId);
        if (war?.Status != "ongoing") return (false, loc.Get("error.not_found", lang));

        var cf = await db.CharacterFactions.FirstOrDefaultAsync(c => c.CharacterId == charId);
        if (cf is null) return (false, loc.Get("error.forbidden", lang));

        // Update scores JSON
        var scores = war.ScoreJson is null
            ? new Dictionary<string, int>()
            : JsonSerializer.Deserialize<Dictionary<string, int>>(war.ScoreJson) ?? [];

        var key = cf.FactionId.ToString();
        scores[key] = scores.GetValueOrDefault(key, 0) + amount;
        war.ScoreJson = JsonSerializer.Serialize(scores);

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Process war (cron) ──────────────────────────────────
    public async Task ProcessWarAsync()
    {
        var wars = await db.FactionWars
            .Where(w => w.Status == "ongoing" && w.EndAt < DateTime.UtcNow)
            .ToListAsync();

        foreach (var war in wars)
        {
            var scores = war.ScoreJson is null
                ? new Dictionary<string, int>()
                : JsonSerializer.Deserialize<Dictionary<string, int>>(war.ScoreJson) ?? [];

            var attackerScore = scores.GetValueOrDefault(war.AttackerId.ToString(), 0);
            var defenderScore = scores.GetValueOrDefault(war.DefenderId.ToString(), 0);

            war.WinnerId = attackerScore >= defenderScore ? war.AttackerId : war.DefenderId;
            war.Status   = "finished";

            logger.LogInformation("War finished: attacker={A} defender={D} winner={W}",
                war.AttackerId, war.DefenderId, war.WinnerId);
        }

        if (wars.Count > 0) await db.SaveChangesAsync();
    }
}
