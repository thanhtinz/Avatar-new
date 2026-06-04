// FantasyWorld.Server/Services/Economy/MarketPriceService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Server.BackgroundServices;

namespace FantasyWorld.Server.Services.Economy;

public interface IMarketPriceService
{
    Task<MarketPriceDto?> GetPriceAsync(int itemId);
    Task<List<MarketPriceDto>> GetAllPricesAsync(int page, int pageSize);
    Task<List<PriceHistory>>  GetHistoryAsync(int itemId, int hours);
    Task RecordSaleAsync(int itemId, int qty, bool isBuy);       // gọi từ ShopService
    Task UpdateAllPricesAsync();                                   // gọi từ cron mỗi 5 phút
    int  GetCurrentPrice(int itemId);
}

public class MarketPriceService(
    GameDbContext db,
    ILogger<MarketPriceService> logger) : IMarketPriceService
{
    // Cache in-memory để tính nhanh
    private readonly Dictionary<int, int> _priceCache = new();

    // ─── Get price ───────────────────────────────────────────
    public async Task<MarketPriceDto?> GetPriceAsync(int itemId)
    {
        var mp = await db.MarketPrices
            .Include(m => m.Item)
            .FirstOrDefaultAsync(m => m.ItemId == itemId);
        if (mp is null) return null;

        var trend = await CalcTrendAsync(itemId);
        return new MarketPriceDto(
            mp.ItemId, mp.Item.Name,
            mp.BasePrice, mp.CurrentPrice,
            mp.Supply, mp.Demand, trend);
    }

    public async Task<List<MarketPriceDto>> GetAllPricesAsync(int page, int pageSize)
    {
        var prices = await db.MarketPrices
            .Include(m => m.Item)
            .OrderBy(m => m.Item.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return prices.Select(mp => new MarketPriceDto(
            mp.ItemId, mp.Item.Name,
            mp.BasePrice, mp.CurrentPrice,
            mp.Supply, mp.Demand, 0f)).ToList();
    }

    public async Task<List<PriceHistory>> GetHistoryAsync(int itemId, int hours) =>
        await db.PriceHistory
            .Where(p => p.ItemId == itemId
                && p.RecordedAt >= DateTime.UtcNow.AddHours(-hours))
            .OrderByDescending(p => p.RecordedAt)
            .Take(288) // tối đa 288 điểm (1 ngày @ 5 phút/điểm)
            .ToListAsync();

    // ─── Record sale ─────────────────────────────────────────
    public async Task RecordSaleAsync(int itemId, int qty, bool isBuy)
    {
        var mp = await db.MarketPrices
            .FirstOrDefaultAsync(m => m.ItemId == itemId);
        if (mp is null) return;

        if (isBuy)
        {
            mp.Demand  += qty;
            mp.Supply  -= Math.Min(qty, mp.Supply - 1);
        }
        else
        {
            mp.Supply  += qty;
            mp.Demand  -= Math.Min(qty, mp.Demand - 1);
        }

        await db.SaveChangesAsync();
    }

    // ─── Update all prices (cron) ────────────────────────────
    public async Task UpdateAllPricesAsync()
    {
        var prices = await db.MarketPrices.Include(m => m.Item).ToListAsync();
        var wt     = WorldTimeService.Current;

        // Tính season modifier
        var seasonMultipliers = await db.SeasonalPriceModifiers
            .Where(s => s.SeasonId == (int)wt.Season + 1)
            .ToDictionaryAsync(s => s.ItemId, s => s.Multiplier);

        var histories = new List<PriceHistory>();

        foreach (var mp in prices)
        {
            var oldPrice = mp.CurrentPrice;

            // ─ Tính giá dựa vào cung/cầu ─
            var ratio = mp.Demand == 0 ? 2.0 :
                        (double)mp.Demand / Math.Max(mp.Supply, 1);

            // Elasticity curve: ratio 1.0 = giá gốc, > 1 = tăng, < 1 = giảm
            float multiplier = ratio switch
            {
                > 2.0 => 1.5f,
                > 1.5 => 1.3f,
                > 1.2 => 1.15f,
                > 0.8 => 1.0f,
                > 0.5 => 0.85f,
                _     => 0.7f,
            };

            // Áp dụng season modifier
            if (seasonMultipliers.TryGetValue(mp.ItemId, out var seasonMult))
                multiplier *= seasonMult;

            // Smooth transition (không thay đổi giá đột ngột)
            var targetPrice = (int)(mp.BasePrice * multiplier);
            var diff        = targetPrice - mp.CurrentPrice;
            mp.CurrentPrice += (int)(diff * 0.1); // chỉ tiến 10% về target mỗi tick

            // Giới hạn 50% - 300% giá gốc
            mp.CurrentPrice = Math.Clamp(mp.CurrentPrice,
                mp.BasePrice / 2, mp.BasePrice * 3);

            mp.Multiplier    = multiplier;
            mp.LastUpdated   = DateTime.UtcNow;

            // Decay supply/demand về 1000 (equilibrium)
            mp.Supply = (int)(mp.Supply * 0.95 + 1000 * 0.05);
            mp.Demand = (int)(mp.Demand * 0.95 + 1000 * 0.05);

            // Cập nhật cache
            _priceCache[mp.ItemId] = mp.CurrentPrice;

            // Ghi lịch sử nếu giá thay đổi > 1%
            if (Math.Abs(oldPrice - mp.CurrentPrice) > oldPrice * 0.01)
            {
                histories.Add(new PriceHistory
                {
                    ItemId     = mp.ItemId,
                    Price      = mp.CurrentPrice,
                    Supply     = mp.Supply,
                    Demand     = mp.Demand,
                    RecordedAt = DateTime.UtcNow,
                });
            }
        }

        if (histories.Count > 0)
            db.PriceHistory.AddRange(histories);

        await db.SaveChangesAsync();

        logger.LogDebug("Market prices updated: {Count} items", prices.Count);
    }

    public int GetCurrentPrice(int itemId) =>
        _priceCache.TryGetValue(itemId, out var p) ? p : 0;

    // ─── Trend calc ──────────────────────────────────────────
    private async Task<float> CalcTrendAsync(int itemId)
    {
        var recent = await db.PriceHistory
            .Where(h => h.ItemId == itemId
                && h.RecordedAt >= DateTime.UtcNow.AddHours(-24))
            .OrderBy(h => h.RecordedAt)
            .Select(h => h.Price)
            .ToListAsync();

        if (recent.Count < 2) return 0f;

        var first = recent.First();
        var last  = recent.Last();
        return first == 0 ? 0 : (float)(last - first) / first;
    }
}
