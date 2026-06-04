// FantasyWorld.Server/Services/CharacterService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Services;

public interface ICharacterService
{
    Task<(bool Ok, string Msg, long CharId)> CreateAsync(long accountId, CreateCharacterRequestDto dto, string lang);
    Task<CharacterFullDto?> GetFullAsync(long charId);
    Task<List<CharacterSummaryDto>> GetByAccountAsync(long accountId);
    Task UpdatePositionAsync(long charId, int mapId, float x, float y);
    Task SetOnlineAsync(long charId);
    Task<(int Level, long Exp, List<int> LevelUps)> AddExpAsync(long charId, long amount);
    Task ModifyGoldAsync(long charId, long delta);
    Task UpdateLifeStatsAsync(long charId, int? hunger, int? energy, int? mood);
    Task AddOnlineTimeAsync(long charId, long seconds);
    Task<List<PlayerEnterDto>> GetOnlineOnMapAsync(int mapId);
}

public class CharacterService(
    GameDbContext db,
    ILocalizationService loc,
    ILogger<CharacterService> logger) : ICharacterService
{
    private static readonly long[] ExpTable = Enumerable
        .Range(1, 200)
        .Select(lv => (long)Math.Floor(100 * Math.Pow(lv, 1.5)))
        .ToArray();

    // ─── Create ──────────────────────────────────────────────
    public async Task<(bool, string, long)> CreateAsync(
        long accountId, CreateCharacterRequestDto dto, string lang)
    {
        // Max 3 characters per account
        var count = await db.Characters.CountAsync(c => c.AccountId == accountId);
        if (count >= 3)
            return (false, loc.Get("error.bad_request", lang), 0);

        // Name uniqueness
        var nameLower = dto.Name.Trim();
        if (await db.Characters.AnyAsync(c => c.Name == nameLower))
            return (false, loc.Get("character.name_taken", lang), 0);

        var character = new Character
        {
            AccountId = accountId,
            Name      = nameLower,
            Gender    = dto.Gender,
            HairStyle = dto.HairStyle,
            HairColor = dto.HairColor,
            FaceStyle = dto.FaceStyle,
            BodyStyle = dto.BodyStyle,
            SkinColor = dto.SkinColor,
        };

        db.Characters.Add(character);
        await db.SaveChangesAsync();

        // Init related rows
        var charId = character.Id;
        db.EquipmentSlots.Add(new EquipmentSlot   { CharacterId = charId });
        db.CharacterFameStats.Add(new CharacterFameStat { CharacterId = charId });
        db.FishingRecords.Add(new FishingRecord    { CharacterId = charId });
        db.CitizenCards.Add(new CitizenCard
        {
            CharacterId = charId,
            CardNumber  = $"FW{charId:D8}",
        });

        // Starter items: 5x Potion nhỏ (id=1), 3x Bánh Mì (id=5)
        db.Inventories.Add(new InventoryItem { CharacterId = charId, ItemId = 1, Quantity = 5, Slot = 0 });
        db.Inventories.Add(new InventoryItem { CharacterId = charId, ItemId = 5, Quantity = 3, Slot = 1 });

        await db.SaveChangesAsync();

        logger.LogInformation("Character created: {Name} (id={Id}, account={AccountId})",
            nameLower, charId, accountId);

        return (true, loc.Get("character.create_success", lang, new { name = nameLower }), charId);
    }

    // ─── Get full ────────────────────────────────────────────
    public async Task<CharacterFullDto?> GetFullAsync(long charId)
    {
        var c = await db.Characters
            .Include(c => c.FactionMemberships).ThenInclude(cf => cf.Faction)
            .Include(c => c.ClanMemberships).ThenInclude(cm => cm.Clan)
            .Include(c => c.AcademyEnrollments).ThenInclude(ca => ca.Academy)
            .FirstOrDefaultAsync(c => c.Id == charId);

        if (c is null) return null;

        return new CharacterFullDto(
            c.Id, c.Name, c.Level, c.Exp, c.Gender,
            c.HairStyle, c.HairColor, c.FaceStyle, c.BodyStyle, c.SkinColor,
            c.MapId, c.PosX, c.PosY,
            c.Hp, c.HpMax, c.Mp, c.MpMax,
            c.Atk, c.Def, c.Spd,
            c.Gold, c.Diamond,
            c.Hunger, c.Energy, c.Mood,
            c.FactionMemberships.FirstOrDefault()?.Faction.Name,
            c.ClanMemberships.FirstOrDefault()?.Clan.Name,
            c.AcademyEnrollments.FirstOrDefault()?.Academy.Name
        );
    }

    // ─── Get by account ──────────────────────────────────────
    public async Task<List<CharacterSummaryDto>> GetByAccountAsync(long accountId)
    {
        return await db.Characters
            .Where(c => c.AccountId == accountId)
            .Select(c => new CharacterSummaryDto(
                c.Id, c.Name, c.Level, c.Gender, c.MapId, c.PosX, c.PosY))
            .ToListAsync();
    }

    // ─── Update position ─────────────────────────────────────
    public async Task UpdatePositionAsync(long charId, int mapId, float x, float y)
    {
        await db.Characters
            .Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.MapId, mapId)
                .SetProperty(c => c.PosX,  x)
                .SetProperty(c => c.PosY,  y));
    }

    // ─── Set online ──────────────────────────────────────────
    public async Task SetOnlineAsync(long charId)
    {
        await db.Characters
            .Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LastOnline, DateTime.UtcNow));
    }

    // ─── Add EXP / Level Up ──────────────────────────────────
    public async Task<(int Level, long Exp, List<int> LevelUps)> AddExpAsync(long charId, long amount)
    {
        var c = await db.Characters.FindAsync(charId)
            ?? throw new InvalidOperationException("Character not found");

        var level    = c.Level;
        var exp      = c.Exp + amount;
        var levelUps = new List<int>();

        while (level < 200 && exp >= ExpTable[level - 1])
        {
            exp -= ExpTable[level - 1];
            level++;
            levelUps.Add(level);
        }

        c.Level = level;
        c.Exp   = exp;
        await db.SaveChangesAsync();

        return (level, exp, levelUps);
    }

    // ─── Modify gold ─────────────────────────────────────────
    public async Task ModifyGoldAsync(long charId, long delta)
    {
        var c = await db.Characters.FindAsync(charId)
            ?? throw new InvalidOperationException("Character not found");

        if (delta < 0 && c.Gold < Math.Abs(delta))
            throw new InvalidOperationException("INSUFFICIENT_GOLD");

        c.Gold += delta;
        await db.SaveChangesAsync();
    }

    // ─── Life stats ──────────────────────────────────────────
    public async Task UpdateLifeStatsAsync(long charId, int? hunger, int? energy, int? mood)
    {
        var c = await db.Characters.FindAsync(charId);
        if (c is null) return;

        if (hunger.HasValue) c.Hunger = (byte)Math.Clamp((c.Hunger + hunger.Value), 0, 100);
        if (energy.HasValue) c.Energy = (byte)Math.Clamp((c.Energy + energy.Value), 0, 100);
        if (mood.HasValue)   c.Mood   = (byte)Math.Clamp((c.Mood   + mood.Value),   0, 100);

        await db.SaveChangesAsync();
    }

    // ─── Online time ─────────────────────────────────────────
    public async Task AddOnlineTimeAsync(long charId, long seconds)
    {
        await db.Characters
            .Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.OnlineTime, c => c.OnlineTime + seconds));
    }

    // ─── Online players on map ───────────────────────────────
    public async Task<List<PlayerEnterDto>> GetOnlineOnMapAsync(int mapId)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        return await db.Characters
            .Where(c => c.MapId == mapId && c.LastOnline >= cutoff)
            .Select(c => new PlayerEnterDto(
                c.Id, c.Name, c.Level, c.PosX, c.PosY, c.Gender))
            .ToListAsync();
    }
}
