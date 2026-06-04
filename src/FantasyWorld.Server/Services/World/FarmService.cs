// FantasyWorld.Server/Services/World/FarmService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Server.BackgroundServices;

namespace FantasyWorld.Server.Services.World;

public interface IFarmService
{
    Task<List<FarmPlotDto>>    GetPlotsAsync(long charId);
    Task<(bool Ok, string Msg)> PlantAsync(long charId, PlantRequest req, string lang);
    Task<(bool Ok, string Msg)> WaterAsync(long charId, long plotId, string lang);
    Task<(bool Ok, string Msg)> FertilizeAsync(long charId, long plotId, int itemId, string lang);
    Task<(bool Ok, string Msg, HarvestResultDto? Result)> HarvestAsync(long charId, long plotId, string lang);
    Task<(bool Ok, string Msg)> CreatePlotAsync(long charId, int mapId, float x, float y, string lang);
    Task GrowCropsAsync();  // cron mỗi phút
}

public class FarmService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<FarmService> logger) : IFarmService
{
    private const int MaxPlotsPerChar = 12;

    // ─── Get plots ───────────────────────────────────────────
    public async Task<List<FarmPlotDto>> GetPlotsAsync(long charId)
    {
        var plots = await db.FarmPlots
            .Where(p => p.OwnerId == charId)
            .Include(p => p.CurrentCrop).ThenInclude(c => c!.Crop)
            .ToListAsync();

        return plots.Select(p =>
        {
            var crop = p.CurrentCrop;
            return new FarmPlotDto(
                p.Id, p.PlotType, p.PosX, p.PosY,
                crop?.Crop.Name,
                crop?.GrowthStage ?? 0,
                crop?.IsWatered ?? false,
                crop?.IsFertilized ?? false,
                crop?.GrowthStage == 3,
                crop?.ReadyAt is null ? null
                    : new DateTimeOffset(crop.ReadyAt).ToUnixTimeMilliseconds());
        }).ToList();
    }

    // ─── Plant ───────────────────────────────────────────────
    public async Task<(bool, string)> PlantAsync(
        long charId, PlantRequest req, string lang)
    {
        var plot = await db.FarmPlots
            .Include(p => p.CurrentCrop)
            .FirstOrDefaultAsync(p => p.Id == req.PlotId && p.OwnerId == charId);

        if (plot is null) return (false, loc.Get("error.not_found", lang));
        if (plot.CurrentCrop is not null && !plot.CurrentCrop.IsHarvested)
            return (false, loc.Get("error.bad_request", lang));

        // Tìm crop từ seed item
        var crop = await db.Crops
            .FirstOrDefaultAsync(c => c.SeedItemId == req.SeedItemId);
        if (crop is null) return (false, loc.Get("error.bad_request", lang));

        // Kiểm tra mùa
        var wt = WorldTimeService.Current;
        if (crop.SeasonsJson is not null)
        {
            var seasons = JsonSerializer.Deserialize<List<string>>(crop.SeasonsJson) ?? [];
            var curSeason = wt.Season.ToString().ToLower();
            if (!seasons.Contains(curSeason))
                return (false, loc.Get("error.bad_request", lang));
        }

        // Lấy hạt giống từ inventory
        var seed = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == req.SeedItemId && i.Quantity > 0);
        if (seed is null) return (false, loc.Get("inventory.item_not_found", lang));

        seed.Quantity--;
        if (seed.Quantity == 0) db.Inventories.Remove(seed);

        // Trồng
        var readyAt = DateTime.UtcNow.AddMinutes(crop.GrowMinutes);
        if (plot.CurrentCrop is null)
        {
            db.PlotCrops.Add(new PlotCrop
            {
                PlotId      = plot.Id,
                CropId      = crop.Id,
                PlantedAt   = DateTime.UtcNow,
                ReadyAt     = readyAt,
                GrowthStage = 0,
            });
        }
        else
        {
            plot.CurrentCrop.CropId      = crop.Id;
            plot.CurrentCrop.PlantedAt   = DateTime.UtcNow;
            plot.CurrentCrop.ReadyAt     = readyAt;
            plot.CurrentCrop.GrowthStage = 0;
            plot.CurrentCrop.IsWatered   = false;
            plot.CurrentCrop.IsFertilized = false;
            plot.CurrentCrop.IsHarvested = false;
        }

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Water ───────────────────────────────────────────────
    public async Task<(bool, string)> WaterAsync(
        long charId, long plotId, string lang)
    {
        var plot = await db.FarmPlots
            .Include(p => p.CurrentCrop)
            .FirstOrDefaultAsync(p => p.Id == plotId && p.OwnerId == charId);

        if (plot?.CurrentCrop is null || plot.CurrentCrop.GrowthStage >= 3)
            return (false, loc.Get("error.bad_request", lang));

        if (plot.CurrentCrop.IsWatered)
            return (false, loc.Get("error.bad_request", lang));

        plot.CurrentCrop.IsWatered = true;

        // Tưới nước rút ngắn thời gian 33%
        var crop    = await db.Crops.FindAsync(plot.CurrentCrop.CropId);
        var speedUp = (int)(crop!.GrowMinutes * 0.33);
        plot.CurrentCrop.ReadyAt = plot.CurrentCrop.ReadyAt.AddMinutes(-speedUp);

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Fertilize ───────────────────────────────────────────
    public async Task<(bool, string)> FertilizeAsync(
        long charId, long plotId, int itemId, string lang)
    {
        var plot = await db.FarmPlots
            .Include(p => p.CurrentCrop)
            .FirstOrDefaultAsync(p => p.Id == plotId && p.OwnerId == charId);

        if (plot?.CurrentCrop is null || plot.CurrentCrop.GrowthStage >= 3)
            return (false, loc.Get("error.bad_request", lang));

        if (plot.CurrentCrop.IsFertilized)
            return (false, loc.Get("error.bad_request", lang));

        // Lấy phân bón từ inventory
        var fert = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == itemId && i.Quantity > 0);
        if (fert is null) return (false, loc.Get("inventory.item_not_found", lang));

        fert.Quantity--;
        if (fert.Quantity == 0) db.Inventories.Remove(fert);

        plot.CurrentCrop.IsFertilized = true;

        // Bón phân rút ngắn thêm 50%
        var crop    = await db.Crops.FindAsync(plot.CurrentCrop.CropId);
        var speedUp = (int)(crop!.GrowMinutes * 0.5);
        plot.CurrentCrop.ReadyAt = plot.CurrentCrop.ReadyAt.AddMinutes(-speedUp);
        if (plot.CurrentCrop.ReadyAt < DateTime.UtcNow)
            plot.CurrentCrop.ReadyAt = DateTime.UtcNow.AddSeconds(10);

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Harvest ─────────────────────────────────────────────
    public async Task<(bool, string, HarvestResultDto?)> HarvestAsync(
        long charId, long plotId, string lang)
    {
        var plot = await db.FarmPlots
            .Include(p => p.CurrentCrop).ThenInclude(c => c!.Crop).ThenInclude(c => c.HarvestItem)
            .FirstOrDefaultAsync(p => p.Id == plotId && p.OwnerId == charId);

        if (plot?.CurrentCrop is null)
            return (false, loc.Get("error.not_found", lang), null);

        if (plot.CurrentCrop.GrowthStage < 3)
            return (false, loc.Get("error.bad_request", lang), null);

        var crop = plot.CurrentCrop.Crop;
        var rng  = new Random();
        var qty  = rng.Next(crop.HarvestQtyMin, crop.HarvestQtyMax + 1);

        // Bonus phân bón → thêm 50% sản lượng
        if (plot.CurrentCrop.IsFertilized)
            qty = (int)(qty * 1.5);

        // Thêm vào inventory
        var existing = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == crop.HarvestItemId
                && i.Quantity < crop.HarvestItem.MaxStack);

        if (existing is not null) existing.Quantity += qty;
        else
        {
            var maxSlot = await db.Inventories
                .Where(i => i.CharacterId == charId)
                .MaxAsync(i => (int?)i.Slot) ?? -1;
            db.Inventories.Add(new InventoryItem
            {
                CharacterId = charId,
                ItemId      = crop.HarvestItemId,
                Quantity    = qty,
                Slot        = maxSlot + 1,
            });
        }

        // Cộng exp
        await db.Characters
            .Where(c => c.Id == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Exp, c => c.Exp + crop.ExpReward));

        plot.CurrentCrop.IsHarvested = true;
        plot.CurrentCrop.GrowthStage = 4; // harvested

        await db.SaveChangesAsync();

        logger.LogDebug("Harvest: charId={Char} crop={Crop} qty={Qty}",
            charId, crop.Name, qty);

        return (true, "OK", new HarvestResultDto(crop.Name, qty, crop.ExpReward));
    }

    // ─── Create plot ─────────────────────────────────────────
    public async Task<(bool, string)> CreatePlotAsync(
        long charId, int mapId, float x, float y, string lang)
    {
        var count = await db.FarmPlots.CountAsync(p => p.OwnerId == charId);
        if (count >= MaxPlotsPerChar)
            return (false, loc.Get("error.bad_request", lang));

        db.FarmPlots.Add(new FarmPlot
        {
            OwnerId = charId,
            MapId   = mapId,
            PosX    = x,
            PosY    = y,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Grow crops cron (mỗi phút) ──────────────────────────
    public async Task GrowCropsAsync()
    {
        var now = DateTime.UtcNow;

        // Stage 0→1 (25%), 1→2 (50%), 2→3 (75%), 3=ready
        var growing = await db.PlotCrops
            .Include(pc => pc.Crop)
            .Where(pc => !pc.IsHarvested && pc.GrowthStage < 3)
            .ToListAsync();

        foreach (var pc in growing)
        {
            var totalMinutes = pc.Crop.GrowMinutes;
            var elapsed      = (now - pc.PlantedAt).TotalMinutes;
            var progress     = elapsed / totalMinutes;

            pc.GrowthStage = progress switch
            {
                >= 1.0 => 3,
                >= 0.75 => 2,
                >= 0.5  => 1,
                >= 0.25 => 1,
                _ => 0
            };
        }

        if (growing.Count > 0)
            await db.SaveChangesAsync();
    }
}
