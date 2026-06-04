// FantasyWorld.Server/Services/Economy/ShopService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Economy;

public interface IShopService
{
    // NPC Shop
    Task<List<NpcShopItemDto>> GetNpcShopItemsAsync(int shopId, long charId, string lang);
    Task<(bool Ok, string Msg, int NewGold)> BuyFromNpcAsync(long charId, int shopItemId, int qty, string lang);
    Task<(bool Ok, string Msg, int GoldEarned)> SellToNpcAsync(long charId, int inventoryId, int qty, string lang);

    // Player Shop
    Task<(bool Ok, string Msg, long ShopId)> CreatePlayerShopAsync(long ownerId, string name, string type, int mapId, string lang);
    Task<(bool Ok, string Msg)> AddListingAsync(long ownerId, int itemId, int qty, int price, string lang);
    Task<(bool Ok, string Msg)> BuyFromPlayerShopAsync(long buyerId, long listingId, int qty, string lang);
    Task<(bool Ok, string Msg)> RemoveListingAsync(long ownerId, long listingId, string lang);
    Task<List<PlayerShopListingDto>> GetShopListingsAsync(long shopId);
    Task<List<PlayerShopDto>>        GetShopsOnMapAsync(int mapId);
    Task<PlayerShopDto?>             GetMyShopAsync(long charId);
    Task<(bool Ok, string Msg)>      ToggleShopAsync(long ownerId, bool isOpen, string lang);
}

public class ShopService(
    GameDbContext        db,
    ILocalizationService loc,
    ILogger<ShopService> logger) : IShopService
{
    // ─── NPC Buy ─────────────────────────────────────────────
    public async Task<List<NpcShopItemDto>> GetNpcShopItemsAsync(
        int shopId, long charId, string lang)
    {
        var charLevel = await db.Characters
            .Where(c => c.Id == charId)
            .Select(c => c.Level)
            .FirstOrDefaultAsync();

        return await db.NpcShopItems
            .Where(s => s.ShopId == shopId && s.IsAvailable && s.MinLevel <= charLevel)
            .Include(s => s.Item)
            .Select(s => new NpcShopItemDto(
                s.Id, s.ItemId, s.Item.Name,
                s.Item.Rarity.ToString(),
                s.BuyPrice, s.SellPrice,
                s.Stock, s.IsAvailable))
            .ToListAsync();
    }

    public async Task<(bool, string, int)> BuyFromNpcAsync(
        long charId, int shopItemId, int qty, string lang)
    {
        var shopItem = await db.NpcShopItems
            .Include(s => s.Item)
            .FirstOrDefaultAsync(s => s.Id == shopItemId && s.IsAvailable);

        if (shopItem is null)
            return (false, loc.Get("shop.out_of_stock", lang), 0);

        // Kiểm tra daily limit
        if (shopItem.DailyLimit.HasValue &&
            shopItem.SoldToday + qty > shopItem.DailyLimit.Value)
            return (false, loc.Get("shop.out_of_stock", lang), 0);

        var totalCost = shopItem.BuyPrice * qty;

        var char_ = await db.Characters.FindAsync(charId)!;
        if (char_!.Gold < totalCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = totalCost, have = char_.Gold }), 0);

        // Kiểm tra inventory
        var usedSlots = await db.Inventories
            .CountAsync(i => i.CharacterId == charId);
        if (usedSlots >= 60 && !shopItem.Item.Stackable)
            return (false, loc.Get("inventory.full", lang), 0);

        // Trừ gold
        char_.Gold -= totalCost;
        shopItem.SoldToday += qty;

        // Cộng vào inventory
        await AddToInventoryAsync(charId, shopItem.ItemId, qty, shopItem.Item.MaxStack);

        await db.SaveChangesAsync();

        logger.LogInformation("NPC buy: charId={Char} item={Item} qty={Qty} cost={Cost}",
            charId, shopItem.Item.Name, qty, totalCost);

        return (true,
            loc.Get("shop.buy_success", lang, new
            {
                item  = shopItem.Item.Name,
                qty,
                price = totalCost
            }),
            (int)char_.Gold);
    }

    public async Task<(bool, string, int)> SellToNpcAsync(
        long charId, int inventoryId, int qty, string lang)
    {
        var invItem = await db.Inventories
            .Include(i => i.Item)
            .FirstOrDefaultAsync(i => i.Id == inventoryId && i.CharacterId == charId);

        if (invItem is null || invItem.Quantity < qty)
            return (false, loc.Get("inventory.item_not_found", lang), 0);

        var earned = invItem.Item.SellPrice * qty;

        // Trừ inventory
        invItem.Quantity -= qty;
        if (invItem.Quantity == 0) db.Inventories.Remove(invItem);

        // Cộng gold
        var char_ = await db.Characters.FindAsync(charId)!;
        char_!.Gold += earned;

        // Cập nhật fame stats
        await db.CharacterFameStats
            .Where(s => s.CharacterId == charId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.TotalGoldEarned, f => f.TotalGoldEarned + earned)
                .SetProperty(f => f.TradeVolume, f => f.TradeVolume + earned));

        await db.SaveChangesAsync();

        return (true,
            loc.Get("shop.sell_success", lang, new
            {
                item  = invItem.Item.Name,
                qty,
                price = earned
            }),
            (int)char_.Gold);
    }

    // ─── Player Shop ─────────────────────────────────────────
    public async Task<(bool, string, long)> CreatePlayerShopAsync(
        long ownerId, string name, string type, int mapId, string lang)
    {
        var exists = await db.PlayerShops.AnyAsync(s => s.OwnerId == ownerId);
        if (exists)
            return (false, loc.Get("error.bad_request", lang), 0);

        var shop = new PlayerShop
        {
            OwnerId   = ownerId,
            Name      = name.Trim(),
            ShopType  = type,
            MapId     = mapId,
        };
        db.PlayerShops.Add(shop);
        await db.SaveChangesAsync();

        return (true, "OK", shop.Id);
    }

    public async Task<(bool, string)> AddListingAsync(
        long ownerId, int itemId, int qty, int price, string lang)
    {
        var shop = await db.PlayerShops
            .FirstOrDefaultAsync(s => s.OwnerId == ownerId && s.IsOpen);
        if (shop is null)
            return (false, loc.Get("shop.shop_closed", lang));

        // Kiểm tra có hàng trong inventory
        var total = await db.Inventories
            .Where(i => i.CharacterId == ownerId && i.ItemId == itemId)
            .SumAsync(i => i.Quantity);
        if (total < qty)
            return (false, loc.Get("inventory.item_not_found", lang));

        // Trừ inventory
        var rows = await db.Inventories
            .Where(i => i.CharacterId == ownerId && i.ItemId == itemId)
            .OrderBy(i => i.Id)
            .ToListAsync();

        int remaining = qty;
        foreach (var row in rows)
        {
            if (remaining <= 0) break;
            var take = Math.Min(row.Quantity, remaining);
            row.Quantity -= take;
            remaining    -= take;
            if (row.Quantity == 0) db.Inventories.Remove(row);
        }

        db.PlayerShopListings.Add(new PlayerShopListing
        {
            ShopId   = shop.Id,
            SellerId = ownerId,
            ItemId   = itemId,
            Quantity = qty,
            Price    = price,
        });

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> BuyFromPlayerShopAsync(
        long buyerId, long listingId, int qty, string lang)
    {
        var listing = await db.PlayerShopListings
            .Include(l => l.Item)
            .Include(l => l.Shop)
            .FirstOrDefaultAsync(l => l.Id == listingId && !l.IsSold && l.Shop.IsOpen);

        if (listing is null)
            return (false, loc.Get("shop.out_of_stock", lang));

        if (listing.SellerId == buyerId)
            return (false, loc.Get("error.bad_request", lang));

        if (listing.Quantity < qty)
            return (false, loc.Get("shop.out_of_stock", lang));

        var totalCost = listing.Price * qty;

        var buyer = await db.Characters.FindAsync(buyerId)!;
        if (buyer!.Gold < totalCost)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = totalCost, have = buyer.Gold }));

        // Trừ gold buyer
        buyer.Gold -= totalCost;

        // Cộng gold seller (trừ 5% phí sàn)
        var fee       = (int)(totalCost * 0.05);
        var sellerNet = totalCost - fee;
        await db.Characters
            .Where(c => c.Id == listing.SellerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + sellerNet));

        // Cập nhật listing
        listing.Quantity -= qty;
        if (listing.Quantity == 0)
        {
            listing.IsSold = true;
            listing.SoldAt = DateTime.UtcNow;
        }

        // Cộng item cho buyer
        await AddToInventoryAsync(buyerId, listing.ItemId, qty, listing.Item.MaxStack);

        // Ghi transaction
        db.PlayerShopTransactions.Add(new PlayerShopTransaction
        {
            ShopId     = listing.ShopId,
            BuyerId    = buyerId,
            ListingId  = listingId,
            Quantity   = qty,
            PriceEach  = listing.Price,
            TotalPrice = totalCost,
        });

        // Cập nhật trade volume
        await db.CharacterFameStats
            .Where(s => s.CharacterId == buyerId || s.CharacterId == listing.SellerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.TradeVolume, f => f.TradeVolume + totalCost));

        await db.SaveChangesAsync();

        logger.LogInformation("Player shop buy: buyer={Buyer} seller={Seller} item={Item} cost={Cost}",
            buyerId, listing.SellerId, listing.Item.Name, totalCost);

        return (true, loc.Get("shop.buy_success", lang, new
        {
            item  = listing.Item.Name,
            qty,
            price = totalCost
        }));
    }

    public async Task<(bool, string)> RemoveListingAsync(
        long ownerId, long listingId, string lang)
    {
        var listing = await db.PlayerShopListings
            .FirstOrDefaultAsync(l => l.Id == listingId
                && l.SellerId == ownerId && !l.IsSold);

        if (listing is null)
            return (false, loc.Get("error.not_found", lang));

        // Hoàn trả hàng vào inventory
        await AddToInventoryAsync(ownerId, listing.ItemId, listing.Quantity, 99);

        db.PlayerShopListings.Remove(listing);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<PlayerShopListingDto>> GetShopListingsAsync(long shopId) =>
        await db.PlayerShopListings
            .Where(l => l.ShopId == shopId && !l.IsSold)
            .Include(l => l.Item)
            .Include(l => l.Shop).ThenInclude(s => s.Owner)
            .Select(l => new PlayerShopListingDto(
                l.Id, l.ItemId, l.Item.Name,
                l.Item.Rarity.ToString(),
                l.Quantity, l.Price,
                l.Shop.Owner.Name))
            .ToListAsync();

    public async Task<List<PlayerShopDto>> GetShopsOnMapAsync(int mapId) =>
        await db.PlayerShops
            .Where(s => s.MapId == mapId && s.IsOpen)
            .Include(s => s.Owner)
            .Select(s => new PlayerShopDto(
                s.Id, s.Name, s.ShopType,
                s.Owner.Name, s.Level, s.IsOpen,
                s.Listings.Count(l => !l.IsSold)))
            .ToListAsync();

    public async Task<PlayerShopDto?> GetMyShopAsync(long charId)
    {
        var shop = await db.PlayerShops
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.OwnerId == charId);
        if (shop is null) return null;
        var count = await db.PlayerShopListings
            .CountAsync(l => l.ShopId == shop.Id && !l.IsSold);
        return new PlayerShopDto(shop.Id, shop.Name, shop.ShopType,
            shop.Owner.Name, shop.Level, shop.IsOpen, count);
    }

    public async Task<(bool, string)> ToggleShopAsync(
        long ownerId, bool isOpen, string lang)
    {
        var shop = await db.PlayerShops.FirstOrDefaultAsync(s => s.OwnerId == ownerId);
        if (shop is null) return (false, loc.Get("error.not_found", lang));
        shop.IsOpen = isOpen;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Helper ──────────────────────────────────────────────
    private async Task AddToInventoryAsync(long charId, int itemId, int qty, int maxStack)
    {
        var existing = await db.Inventories
            .FirstOrDefaultAsync(i => i.CharacterId == charId
                && i.ItemId == itemId && i.Quantity < maxStack);

        if (existing is not null)
        {
            var canAdd = Math.Min(qty, maxStack - existing.Quantity);
            existing.Quantity += canAdd;
            qty -= canAdd;
        }

        if (qty > 0)
        {
            var maxSlot = await db.Inventories
                .Where(i => i.CharacterId == charId)
                .MaxAsync(i => (int?)i.Slot) ?? -1;
            db.Inventories.Add(new InventoryItem
            {
                CharacterId = charId,
                ItemId      = itemId,
                Quantity    = qty,
                Slot        = maxSlot + 1,
            });
        }
    }
}
