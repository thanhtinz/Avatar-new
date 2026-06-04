// FantasyWorld.Server/Services/Gameplay/MuseumService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;

namespace FantasyWorld.Server.Services.Gameplay;

public interface IMuseumService
{
    Task<Museum?>                    GetServerMuseumAsync(int mapId);
    Task<List<MuseumExhibit>>        GetExhibitsAsync(int museumId, string? category);
    Task<(bool Ok, string Msg)>      DonateAsync(long charId, int museumId, int itemId, string lang);
    Task<List<MuseumDonation>>       GetMyDonationsAsync(long charId);
    Task<(bool Ok, string Msg)>      ClaimCollectionRewardAsync(long charId, int achievementId, string lang);
}

public class MuseumService(
    GameDbContext db, ILocalizationService loc) : IMuseumService
{
    public async Task<Museum?> GetServerMuseumAsync(int mapId) =>
        await db.Museums.FirstOrDefaultAsync(m => m.MapId == mapId);

    public async Task<List<MuseumExhibit>> GetExhibitsAsync(int museumId, string? category)
    {
        var q = db.MuseumExhibits.Where(e => e.MuseumId == museumId);
        if (category != null) q = q.Where(e => e.Category == category);
        return await q.Include(e => e.DonorCharacter).OrderBy(e => e.Category).ThenBy(e => e.Name).ToListAsync();
    }

    public async Task<(bool, string)> DonateAsync(long charId, int museumId, int itemId, string lang)
    {
        var museum  = await db.Museums.FindAsync(museumId);
        if (museum is null) return (false, loc.Get("error.not_found", lang));

        var item = await db.Items.FindAsync(itemId);
        if (item is null || item.ItemType.ToString().ToLower() is not ("fish" or "fossil" or "artifact" or "insect" or "flower"))
            return (false, loc.Get("error.bad_request", lang));

        // Already in museum?
        var alreadyDonated = await db.MuseumExhibits
            .AnyAsync(e => e.MuseumId == museumId && e.ItemId == itemId);
        if (alreadyDonated) return (false, loc.Get("error.bad_request", lang));

        // Remove from inventory
        var inv = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId && i.ItemId == itemId && i.Quantity > 0);
        if (inv is null) return (false, loc.Get("inventory.item_not_found", lang));

        inv.Quantity--;
        if (inv.Quantity == 0) db.Inventories.Remove(inv);

        db.MuseumExhibits.Add(new MuseumExhibit
        {
            MuseumId   = museumId,
            ItemId     = itemId,
            Name       = item.Name,
            Category   = item.ItemType.ToString().ToLower(),
            DonorId    = charId,
            DonatedAt  = DateTime.UtcNow,
        });

        db.MuseumDonations.Add(new MuseumDonation
        {
            CharacterId = charId,
            MuseumId    = museumId,
            ItemId      = itemId,
        });

        // Reward donor
        await db.Characters.Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Exp, c => c.Exp + 50));

        await db.SaveChangesAsync();
        return (true, loc.Get("museum.donated", lang, new { item = item.Name }));
    }

    public async Task<List<MuseumDonation>> GetMyDonationsAsync(long charId) =>
        await db.MuseumDonations.Where(d => d.CharacterId == charId)
            .Include(d => d.Item).ToListAsync();

    public async Task<(bool, string)> ClaimCollectionRewardAsync(long charId, int achievementId, string lang)
    {
        var ca = await db.CharacterAchievements
            .Include(a => a.Achievement)
            .FirstOrDefaultAsync(a => a.CharacterId == charId
                && a.AchievementId == achievementId
                && a.IsCompleted && !a.RewardClaimed);
        if (ca is null) return (false, loc.Get("error.not_found", lang));

        ca.RewardClaimed = true;
        // Parse and apply reward JSON
        if (ca.Achievement.RewardJson is not null)
        {
            var reward = JsonSerializer.Deserialize<Dictionary<string, int>>(ca.Achievement.RewardJson) ?? [];
            if (reward.TryGetValue("gold", out var g))
                await db.Characters.Where(c => c.Id == charId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Gold, c => c.Gold + g));
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }
}
