// FantasyWorld.Server/Services/World/FishingService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Server.BackgroundServices;

namespace FantasyWorld.Server.Services.World;

public interface IFishingService
{
    Task<FishingResultDto>   CastAsync(long charId, int mapId, string lang);
    Task<FishingRecordDto>   GetRecordAsync(long charId);
    Task<List<FishingLog>>   GetRecentLogsAsync(long charId, int count);
}

public class FishingService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<FishingService> logger) : IFishingService
{
    // ─── Cast ────────────────────────────────────────────────
    public async Task<FishingResultDto> CastAsync(long charId, int mapId, string lang)
    {
        var wt      = WorldTimeService.Current;
        var season  = wt.Season.ToString().ToLower();
        var timeStr = wt.IsDay ? "day" : "night";
        var isFull  = wt.MoonPhase == 14;
        var rng     = new Random();

        // Tìm cá phù hợp với map + mùa + giờ
        var allFish = await db.FishSpecies.ToListAsync();
        var eligible = allFish.Where(f =>
        {
            // Kiểm tra map
            if (f.MapIdsJson is not null)
            {
                var mapIds = JsonSerializer.Deserialize<List<int>>(f.MapIdsJson) ?? [];
                if (!mapIds.Contains(mapId)) return false;
            }
            // Kiểm tra mùa
            if (f.SeasonJson is not null)
            {
                var seasons = JsonSerializer.Deserialize<List<string>>(f.SeasonJson) ?? [];
                if (!seasons.Contains("any") && !seasons.Contains(season)) return false;
            }
            // Kiểm tra giờ
            if (f.TimeJson is not null)
            {
                var times = JsonSerializer.Deserialize<List<string>>(f.TimeJson) ?? [];
                if (times.Contains("full_moon") && !isFull) return false;
                if (!times.Contains("full_moon") &&
                    !times.Contains("any") && !times.Contains(timeStr)) return false;
            }
            return true;
        }).ToList();

        // Không có cá → nothing
        if (!eligible.Any())
        {
            await LogFishingAsync(charId, null, 0, mapId, "nothing", 0, 0);
            return new FishingResultDto("nothing", null, null, 0, 0, 0, false, false);
        }

        // 30% cơ hội bắt được cá
        if (rng.Next(100) >= 30)
        {
            await LogFishingAsync(charId, null, 0, mapId, "nothing", 0, 0);
            return new FishingResultDto("nothing", null, null, 0, 0, 0, false, false);
        }

        // Chọn cá theo weight của rarity
        var weights = eligible.Select(f => f.Rarity switch
        {
            ItemRarity.Common    => 100,
            ItemRarity.Uncommon  => 40,
            ItemRarity.Rare      => 15,
            ItemRarity.Epic      => 4,
            ItemRarity.Legendary => 1,
            _ => 50
        }).ToList();

        var totalW = weights.Sum();
        var roll   = rng.Next(totalW);
        int cumulative = 0, idx = 0;
        for (; idx < eligible.Count; idx++)
        {
            cumulative += weights[idx];
            if (roll < cumulative) break;
        }

        var fish   = eligible[idx];
        var weight = (float)(fish.MinWeight +
            rng.NextDouble() * (fish.MaxWeight - fish.MinWeight));
        weight = MathF.Round(weight, 2);

        // 10% escaped
        if (rng.Next(100) < 10)
        {
            await LogFishingAsync(charId, fish.Id, weight, mapId, "escaped", 0, 0);
            return new FishingResultDto("escaped", fish.Name,
                fish.Rarity.ToString(), weight, 0, 0, false, false);
        }

        // Tính exp và gold
        var expBase  = fish.Rarity switch
        {
            ItemRarity.Common    => 5,
            ItemRarity.Uncommon  => 15,
            ItemRarity.Rare      => 40,
            ItemRarity.Epic      => 100,
            ItemRarity.Legendary => 300,
            _ => 10
        };
        var goldEarned = (int)(fish.SellPrice * weight);
        var expEarned  = expBase + (int)(weight * 2);

        // Cộng gold và exp
        await db.Characters
            .Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + goldEarned));

        // Thêm vào inventory (fish là item nếu có)
        // Ở đây coi cá câu được chuyển thành gold trực tiếp

        // Cập nhật fishing record
        var record = await db.FishingRecords.FindAsync(charId);
        bool isRecord = false;
        if (record is not null)
        {
            record.TotalCaught++;
            record.TotalWeight += weight;

            if (weight > record.BiggestWeight)
            {
                record.BiggestWeight = weight;
                record.BiggestFishId = fish.Id;
                isRecord = true;
            }
            // Cập nhật species list
            var caught = JsonSerializer.Deserialize<List<int>>(record.SpeciesCaughtJson) ?? [];
            bool isMuseumNew = !caught.Contains(fish.Id);
            if (isMuseumNew) { caught.Add(fish.Id); record.SpeciesCaughtJson = JsonSerializer.Serialize(caught); }
        }

        // Fame stats
        await db.CharacterFameStats
            .Where(f => f.CharacterId == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.FishCaught, f => f.FishCaught + 1)
                .SetProperty(f => f.TotalGoldEarned, f => f.TotalGoldEarned + goldEarned));

        // Cộng exp
        await db.Characters
            .Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Exp, c => c.Exp + expEarned));

        await LogFishingAsync(charId, fish.Id, weight, mapId, "caught", goldEarned, expEarned);
        await db.SaveChangesAsync();

        logger.LogDebug("Fish caught: charId={Char} fish={Fish} weight={Weight}g gold={Gold}",
            charId, fish.Name, weight, goldEarned);

        return new FishingResultDto(
            "caught", fish.Name, fish.Rarity.ToString(),
            weight, goldEarned, expEarned, isRecord, false);
    }

    // ─── Get record ──────────────────────────────────────────
    public async Task<FishingRecordDto> GetRecordAsync(long charId)
    {
        var r = await db.FishingRecords
            .FirstOrDefaultAsync(f => f.CharacterId == charId);

        if (r is null) return new FishingRecordDto(0, 0, null, 0, 0);

        var biggestName = r.BiggestFishId.HasValue
            ? await db.FishSpecies
                .Where(f => f.Id == r.BiggestFishId)
                .Select(f => f.Name)
                .FirstOrDefaultAsync()
            : null;

        var speciesCount = r.SpeciesCaughtJson is null ? 0
            : (JsonSerializer.Deserialize<List<int>>(r.SpeciesCaughtJson) ?? []).Count;

        return new FishingRecordDto(
            r.TotalCaught, r.TotalWeight,
            biggestName, r.BiggestWeight, speciesCount);
    }

    public async Task<List<FishingLog>> GetRecentLogsAsync(long charId, int count) =>
        await db.FishingLogs
            .Where(f => f.CharacterId == charId)
            .OrderByDescending(f => f.FishedAt)
            .Take(count)
            .Include(f => f.Fish)
            .ToListAsync();

    // ─── Log ─────────────────────────────────────────────────
    private async Task LogFishingAsync(long charId, int? fishId,
        float weight, int mapId, string result, int gold, int exp)
    {
        db.FishingLogs.Add(new FishingLog
        {
            CharacterId = charId,
            FishId      = fishId,
            Weight      = weight,
            MapId       = mapId,
            Result      = result,
            GoldEarned  = gold,
            ExpEarned   = exp,
        });
        await db.SaveChangesAsync();
    }
}
