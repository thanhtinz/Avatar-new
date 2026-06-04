// FantasyWorld.Server/Services/World/PetService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Services.World;

public interface IPetService
{
    Task<List<WildPetDto>>      GetWildPetsOnMapAsync(int mapId);
    Task<CatchResultDto>        TryCatchAsync(long charId, CatchPetRequest req, string lang);
    Task<List<PetDto>>          GetMyPetsAsync(long charId);
    Task<(bool Ok, string Msg)> SetActiveAsync(long charId, long petId, string lang);
    Task<(bool Ok, string Msg)> NicknameAsync(long charId, PetNicknameRequest req, string lang);
    Task<(bool Ok, string Msg)> ReleaseAsync(long charId, long petId, string lang);
    Task<(bool Ok, string Msg)> SendToRanchAsync(long charId, long petId, long ranchId, string lang);
    Task<(bool Ok, string Msg)> FeedPetAsync(long charId, long petId, int foodItemId, string lang);
    Task SpawnWildPetsAsync();       // cron
    Task DecayPetHungerAsync();      // cron
}

public class PetService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc,
    ILogger<PetService>  logger) : IPetService
{
    private const int MaxActivePets   = 1;
    private const int MaxPetsOwned    = 30;
    private static readonly string[] Personalities =
        ["brave","timid","calm","rash","gentle","quirky"];

    // ─── Wild pets on map ────────────────────────────────────
    public async Task<List<WildPetDto>> GetWildPetsOnMapAsync(int mapId)
    {
        return await db.WildPetSpawns
            .Where(w => w.MapId == mapId && w.IsAlive)
            .Include(w => w.Species)
            .Select(w => new WildPetDto(
                w.Id,
                w.SpeciesId,
                w.Species.Name,
                w.Species.Rarity.ToString(),
                w.Species.Element,
                w.PosX, w.PosY,
                w.Species.CatchRate,
                false)) // shiny calc at catch time
            .ToListAsync();
    }

    // ─── Try catch ───────────────────────────────────────────
    public async Task<CatchResultDto> TryCatchAsync(
        long charId, CatchPetRequest req, string lang)
    {
        var spawn = await db.WildPetSpawns
            .Include(w => w.Species)
            .FirstOrDefaultAsync(w => w.Id == req.SpawnId && w.IsAlive);

        if (spawn is null)
            return new CatchResultDto(false,
                loc.Get("error.not_found", lang), null, 0, false);

        // Đếm số thú đang có
        var petCount = await db.Pets.CountAsync(p => p.OwnerId == charId);
        if (petCount >= MaxPetsOwned)
            return new CatchResultDto(false,
                loc.Get("pet.max_pets", lang), null, 0, false);

        // Tính tỷ lệ bắt
        int catchRate = spawn.Species.CatchRate;

        // Bonus từ mồi
        if (req.BaitItemId.HasValue)
        {
            var bait = await db.Inventories
                .FirstOrDefaultAsync(i => i.CharacterId == charId
                    && i.ItemId == req.BaitItemId && i.Quantity > 0);
            if (bait is not null)
            {
                catchRate = Math.Min(95, catchRate + 20);
                bait.Quantity--;
                if (bait.Quantity == 0) db.Inventories.Remove(bait);
            }
        }

        // Roll
        var rng    = new Random();
        bool isShiny  = rng.Next(1000) < 3; // 0.3% shiny
        bool success  = rng.Next(100) < catchRate;

        // Ghi log
        db.PetCatchLogs.Add(new PetCatchLog
        {
            CharacterId = charId,
            SpeciesId   = spawn.SpeciesId,
            Success     = success,
            MapId       = spawn.MapId,
        });

        if (!success)
        {
            await db.SaveChangesAsync();
            return new CatchResultDto(false,
                loc.Get("pet.escaped", lang, new { name = spawn.Species.Name }),
                null, catchRate, isShiny);
        }

        // Tính chỉ số ngẫu nhiên (±10% base)
        var s      = spawn.Species;
        var spread = (v: int v) => (int)(v * (0.9 + rng.NextDouble() * 0.2));

        var pet = new Pet
        {
            OwnerId      = charId,
            SpeciesId    = spawn.SpeciesId,
            Personality  = Personalities[rng.Next(Personalities.Length)],
            ColorVariant = rng.Next(8),
            IsShiny      = isShiny,
            Level        = 1,
            Happiness    = 50,
            Hunger       = 100,
            Hp           = spread(s.BaseHp),
            HpMax        = spread(s.BaseHp),
            Atk          = spread(s.BaseAtk),
            Def          = spread(s.BaseDef),
            Spd          = spread(s.BaseSpd),
            CaughtMapId  = spawn.MapId,
        };
        db.Pets.Add(pet);

        // Đánh dấu wild pet đã bị bắt
        spawn.IsAlive  = false;
        spawn.CaughtAt = DateTime.UtcNow;
        spawn.CaughtBy = charId;

        // Cập nhật fame stats
        await db.CharacterFameStats
            .Where(f => f.CharacterId == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.PetsOwned, f => f.PetsOwned + 1));

        await db.SaveChangesAsync();

        logger.LogInformation("Pet caught: charId={Char} species={Species} shiny={Shiny}",
            charId, spawn.Species.Name, isShiny);

        var dto = MapPetToDto(pet, spawn.Species);
        return new CatchResultDto(true,
            loc.Get("pet.caught", lang, new { name = spawn.Species.Name, rarity = s.Rarity }),
            dto, catchRate, isShiny);
    }

    // ─── Get my pets ─────────────────────────────────────────
    public async Task<List<PetDto>> GetMyPetsAsync(long charId)
    {
        return await db.Pets
            .Where(p => p.OwnerId == charId)
            .Include(p => p.Species)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.Level)
            .Select(p => new PetDto(
                p.Id, p.Species.Name, p.Nickname,
                p.Species.Rarity.ToString(), p.Species.Element,
                p.Personality, p.ColorVariant, p.IsShiny,
                p.Level, p.Happiness, p.Hunger,
                p.Hp, p.HpMax, p.Atk, p.Def, p.Spd, p.IsActive))
            .ToListAsync();
    }

    // ─── Set active ──────────────────────────────────────────
    public async Task<(bool, string)> SetActiveAsync(long charId, long petId, string lang)
    {
        var pet = await db.Pets.FirstOrDefaultAsync(p => p.Id == petId && p.OwnerId == charId);
        if (pet is null) return (false, loc.Get("error.not_found", lang));

        // Tắt pet đang active
        await db.Pets
            .Where(p => p.OwnerId == charId && p.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));

        pet.IsActive = true;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Nickname ────────────────────────────────────────────
    public async Task<(bool, string)> NicknameAsync(
        long charId, PetNicknameRequest req, string lang)
    {
        if (req.Nickname.Length > 20)
            return (false, loc.Get("error.bad_request", lang));

        var pet = await db.Pets.FirstOrDefaultAsync(p =>
            p.Id == req.PetId && p.OwnerId == charId);
        if (pet is null) return (false, loc.Get("error.not_found", lang));

        pet.Nickname = req.Nickname.Trim();
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Release ─────────────────────────────────────────────
    public async Task<(bool, string)> ReleaseAsync(long charId, long petId, string lang)
    {
        var pet = await db.Pets.FirstOrDefaultAsync(p =>
            p.Id == petId && p.OwnerId == charId);
        if (pet is null) return (false, loc.Get("error.not_found", lang));

        db.Pets.Remove(pet);
        await db.CharacterFameStats
            .Where(f => f.CharacterId == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.PetsOwned, f => f.PetsOwned - 1));

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Send to ranch ───────────────────────────────────────
    public async Task<(bool, string)> SendToRanchAsync(
        long charId, long petId, long ranchId, string lang)
    {
        var pet = await db.Pets.FirstOrDefaultAsync(p =>
            p.Id == petId && p.OwnerId == charId);
        if (pet is null) return (false, loc.Get("error.not_found", lang));

        var ranch = await db.PetRanches
            .Include(r => r.Pets)
            .FirstOrDefaultAsync(r => r.Id == ranchId && r.OwnerId == charId);
        if (ranch is null) return (false, loc.Get("error.not_found", lang));

        if (ranch.Pets.Count >= ranch.MaxCap)
            return (false, loc.Get("pet.max_pets", lang));

        pet.IsActive = false;
        db.RanchPets.Add(new RanchPet { RanchId = ranchId, PetId = petId });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Feed pet ────────────────────────────────────────────
    public async Task<(bool, string)> FeedPetAsync(
        long charId, long petId, int foodItemId, string lang)
    {
        var pet = await db.Pets.FirstOrDefaultAsync(p =>
            p.Id == petId && p.OwnerId == charId);
        if (pet is null) return (false, loc.Get("error.not_found", lang));

        var food = await db.Inventories
            .Include(i => i.Item)
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == foodItemId
                && i.Quantity > 0
                && i.Item.ItemType == ItemType.PetFood);
        if (food is null) return (false, loc.Get("inventory.item_not_found", lang));

        food.Quantity--;
        if (food.Quantity == 0) db.Inventories.Remove(food);

        var effect = food.Item.EffectJson is null ? null
            : JsonSerializer.Deserialize<Dictionary<string, int>>(food.Item.EffectJson);

        pet.Hunger    = (byte)Math.Min(100, pet.Hunger    + (effect?.GetValueOrDefault("hunger", 20) ?? 20));
        pet.Happiness = (byte)Math.Min(100, pet.Happiness + (effect?.GetValueOrDefault("pet_happiness", 10) ?? 10));

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Spawn wild pets (cron) ──────────────────────────────
    public async Task SpawnWildPetsAsync()
    {
        var maps = await db.Maps.Where(m => m.IsActive).Select(m => m.Id).ToListAsync();
        var species = await db.PetSpecies.ToListAsync();
        var rng = new Random();

        foreach (var mapId in maps)
        {
            var existing = await db.WildPetSpawns
                .CountAsync(w => w.MapId == mapId && w.IsAlive);

            // Duy trì tối đa 5 wild pet per map
            var toSpawn = Math.Max(0, 5 - existing);
            if (toSpawn == 0) continue;

            var mapSpecies = species.Where(s =>
            {
                if (s.HabitatMapIds is null) return true;
                var ids = JsonSerializer.Deserialize<List<int>>(s.HabitatMapIds) ?? [];
                return ids.Contains(mapId);
            }).ToList();

            if (!mapSpecies.Any()) continue;

            for (int i = 0; i < toSpawn; i++)
            {
                var sp = mapSpecies[rng.Next(mapSpecies.Count)];
                db.WildPetSpawns.Add(new WildPetSpawn
                {
                    MapId     = mapId,
                    SpeciesId = sp.Id,
                    PosX      = (float)(rng.NextDouble() * 100),
                    PosY      = (float)(rng.NextDouble() * 100),
                });
            }
        }

        await db.SaveChangesAsync();
    }

    // ─── Decay hunger (cron) ─────────────────────────────────
    public async Task DecayPetHungerAsync()
    {
        // Active pets đói dần, happy giảm nếu đói
        var activePets = await db.Pets.Where(p => p.IsActive).ToListAsync();
        foreach (var pet in activePets)
        {
            pet.Hunger    = (byte)Math.Max(0, pet.Hunger - 2);
            if (pet.Hunger < 30)
                pet.Happiness = (byte)Math.Max(0, pet.Happiness - 1);
        }
        await db.SaveChangesAsync();
    }

    // ─── Helper ──────────────────────────────────────────────
    private static PetDto MapPetToDto(Pet p, PetSpecies s) => new(
        p.Id, s.Name, p.Nickname,
        s.Rarity.ToString(), s.Element,
        p.Personality, p.ColorVariant, p.IsShiny,
        p.Level, p.Happiness, p.Hunger,
        p.Hp, p.HpMax, p.Atk, p.Def, p.Spd, p.IsActive);
}
