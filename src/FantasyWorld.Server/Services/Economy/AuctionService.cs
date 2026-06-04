// FantasyWorld.Server/Services/Economy/AuctionService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Economy;

public interface IAuctionService
{
    Task<(bool Ok, string Msg, long AuctionId)> CreateAsync(long sellerId, CreateAuctionRequest req, string lang);
    Task<(bool Ok, string Msg)>                 PlaceBidAsync(long bidderId, long auctionId, int amount, string lang);
    Task<(bool Ok, string Msg)>                 BuyoutAsync(long buyerId, long auctionId, string lang);
    Task<(bool Ok, string Msg)>                 CancelAsync(long sellerId, long auctionId, string lang);
    Task<List<AuctionListingDto>>               GetActiveAsync(string? type, int page, int pageSize);
    Task<AuctionListingDto?>                    GetByIdAsync(long auctionId);
    Task<List<AuctionListingDto>>               GetMyListingsAsync(long charId);
    Task<List<AuctionBidDto>>                   GetBidsAsync(long auctionId);
    Task                                        ProcessExpiredAsync();   // gọi từ background job
}

public class AuctionService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc,
    ILogger<AuctionService> logger) : IAuctionService
{
    private const double AuctionFeeRate = 0.05; // 5% phí đăng

    // ─── Create ──────────────────────────────────────────────
    public async Task<(bool, string, long)> CreateAsync(
        long sellerId, CreateAuctionRequest req, string lang)
    {
        if (req.DurationHours is < 1 or > 72)
            return (false, loc.Get("error.bad_request", lang), 0);

        if (req.BuyoutPrice.HasValue && req.BuyoutPrice <= req.StartPrice)
            return (false, loc.Get("error.bad_request", lang), 0);

        // Kiểm tra item có trong inventory
        if (req.ListingType == "item" && req.ItemId.HasValue)
        {
            var total = await db.Inventories
                .Where(i => i.CharacterId == sellerId && i.ItemId == req.ItemId)
                .SumAsync(i => i.Quantity);
            if (total < req.Quantity)
                return (false, loc.Get("inventory.item_not_found", lang), 0);

            // Trừ khỏi inventory
            await RemoveFromInventoryAsync(sellerId, req.ItemId.Value, req.Quantity);
        }
        else if (req.ListingType == "pet" && req.PetId.HasValue)
        {
            var pet = await db.Pets.FirstOrDefaultAsync(p =>
                p.Id == req.PetId && p.OwnerId == sellerId);
            if (pet is null) return (false, loc.Get("error.not_found", lang), 0);
            pet.OwnerId = 0; // tạm giao cho hệ thống giữ
        }

        var auction = new AuctionListing
        {
            SellerId     = sellerId,
            ListingType  = req.ListingType,
            ItemId       = req.ItemId,
            PetId        = req.PetId,
            Quantity     = req.Quantity,
            StartPrice   = req.StartPrice,
            BuyoutPrice  = req.BuyoutPrice,
            CurrentBid   = 0,
            Status       = "active",
            StartsAt     = DateTime.UtcNow,
            EndsAt       = DateTime.UtcNow.AddHours(req.DurationHours),
        };

        db.AuctionListings.Add(auction);
        await db.SaveChangesAsync();

        logger.LogInformation("Auction created: id={Id} seller={Seller} item={Item}",
            auction.Id, sellerId, req.ItemId);

        return (true, "OK", auction.Id);
    }

    // ─── Place bid ───────────────────────────────────────────
    public async Task<(bool, string)> PlaceBidAsync(
        long bidderId, long auctionId, int amount, string lang)
    {
        var auction = await db.AuctionListings
            .Include(a => a.Item)
            .FirstOrDefaultAsync(a => a.Id == auctionId && a.Status == "active");

        if (auction is null)
            return (false, loc.Get("error.not_found", lang));

        if (auction.EndsAt < DateTime.UtcNow)
            return (false, loc.Get("error.bad_request", lang));

        if (auction.SellerId == bidderId)
            return (false, loc.Get("error.forbidden", lang));

        var minBid = auction.CurrentBid > 0
            ? (int)(auction.CurrentBid * 1.05)  // tăng ít nhất 5%
            : auction.StartPrice;

        if (amount < minBid)
            return (false, loc.Get("error.bad_request", lang));

        var bidder = await db.Characters.FindAsync(bidderId)!;
        if (bidder!.Gold < amount)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = amount, have = bidder.Gold }));

        // Hoàn tiền cho bidder cũ
        if (auction.CurrentBidder.HasValue && auction.CurrentBidder != bidderId)
        {
            await db.Characters
                .Where(c => c.Id == auction.CurrentBidder.Value)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Gold, c => c.Gold + auction.CurrentBid));
        }

        // Giữ tiền của bidder mới
        bidder.Gold -= amount;

        // Cập nhật bid
        var prevWinner = auction.CurrentBidder;
        auction.CurrentBid    = amount;
        auction.CurrentBidder = bidderId;
        auction.BidCount++;

        // Gia hạn 5 phút nếu bid trong 5 phút cuối
        if ((auction.EndsAt - DateTime.UtcNow).TotalMinutes < 5)
            auction.EndsAt = DateTime.UtcNow.AddMinutes(5);

        // Đánh dấu bid cũ thua
        await db.AuctionBids
            .Where(b => b.AuctionId == auctionId && b.IsWinning)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsWinning, false));

        db.AuctionBids.Add(new AuctionBid
        {
            AuctionId = auctionId,
            BidderId  = bidderId,
            Amount    = amount,
            IsWinning = true,
        });

        await db.SaveChangesAsync();

        logger.LogInformation("Bid placed: auction={Auction} bidder={Bidder} amount={Amount}",
            auctionId, bidderId, amount);

        return (true, "OK");
    }

    // ─── Buyout ──────────────────────────────────────────────
    public async Task<(bool, string)> BuyoutAsync(
        long buyerId, long auctionId, string lang)
    {
        var auction = await db.AuctionListings
            .Include(a => a.Item)
            .FirstOrDefaultAsync(a => a.Id == auctionId
                && a.Status == "active" && a.BuyoutPrice.HasValue);

        if (auction is null) return (false, loc.Get("error.not_found", lang));
        if (auction.SellerId == buyerId) return (false, loc.Get("error.forbidden", lang));

        var buyer = await db.Characters.FindAsync(buyerId)!;
        if (buyer!.Gold < auction.BuyoutPrice!.Value)
            return (false, loc.Get("character.insufficient_gold", lang,
                new { need = auction.BuyoutPrice.Value, have = buyer.Gold }));

        // Hoàn tiền bidder hiện tại nếu có
        if (auction.CurrentBidder.HasValue)
        {
            await db.Characters
                .Where(c => c.Id == auction.CurrentBidder.Value)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Gold, c => c.Gold + auction.CurrentBid));
        }

        // Trừ gold buyer
        buyer.Gold -= auction.BuyoutPrice.Value;

        // Cộng gold seller (trừ phí)
        var fee       = (int)(auction.BuyoutPrice.Value * AuctionFeeRate);
        var sellerNet = auction.BuyoutPrice.Value - fee;
        await db.Characters
            .Where(c => c.Id == auction.SellerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Gold, c => c.Gold + sellerNet));

        // Giao item
        await DeliverAuctionItemAsync(auction, buyerId);

        auction.Status       = "sold";
        auction.CurrentBid   = auction.BuyoutPrice.Value;
        auction.CurrentBidder = buyerId;
        await db.SaveChangesAsync();

        return (true, "OK");
    }

    // ─── Cancel ──────────────────────────────────────────────
    public async Task<(bool, string)> CancelAsync(
        long sellerId, long auctionId, string lang)
    {
        var auction = await db.AuctionListings
            .FirstOrDefaultAsync(a => a.Id == auctionId
                && a.SellerId == sellerId && a.Status == "active");

        if (auction is null) return (false, loc.Get("error.not_found", lang));

        // Không hủy được nếu đã có bid
        if (auction.BidCount > 0)
            return (false, loc.Get("error.forbidden", lang));

        // Hoàn trả item
        if (auction.ListingType == "item" && auction.ItemId.HasValue)
        {
            var item = await db.Items.FindAsync(auction.ItemId.Value)!;
            await AddToInventoryAsync(sellerId, auction.ItemId.Value, auction.Quantity, item!.MaxStack);
        }
        else if (auction.ListingType == "pet" && auction.PetId.HasValue)
        {
            await db.Pets.Where(p => p.Id == auction.PetId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.OwnerId, sellerId));
        }

        auction.Status = "cancelled";
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Get listings ────────────────────────────────────────
    public async Task<List<AuctionListingDto>> GetActiveAsync(
        string? type, int page, int pageSize)
    {
        var query = db.AuctionListings
            .Where(a => a.Status == "active" && a.EndsAt > DateTime.UtcNow)
            .Include(a => a.Seller)
            .Include(a => a.Item)
            .AsQueryable();

        if (type is not null)
            query = query.Where(a => a.ListingType == type);

        return await query
            .OrderByDescending(a => a.EndsAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync();
    }

    public async Task<AuctionListingDto?> GetByIdAsync(long auctionId)
    {
        var a = await db.AuctionListings
            .Include(a => a.Seller)
            .Include(a => a.Item)
            .FirstOrDefaultAsync(a => a.Id == auctionId);
        return a is null ? null : MapToDto(a);
    }

    public async Task<List<AuctionListingDto>> GetMyListingsAsync(long charId) =>
        await db.AuctionListings
            .Where(a => a.SellerId == charId)
            .Include(a => a.Item)
            .Include(a => a.Seller)
            .OrderByDescending(a => a.StartsAt)
            .Select(a => MapToDto(a))
            .ToListAsync();

    public async Task<List<AuctionBidDto>> GetBidsAsync(long auctionId) =>
        await db.AuctionBids
            .Where(b => b.AuctionId == auctionId)
            .Include(b => b.Bidder)
            .OrderByDescending(b => b.BidAt)
            .Select(b => new AuctionBidDto(
                b.BidderId, b.Bidder.Name, b.Amount,
                new DateTimeOffset(b.BidAt).ToUnixTimeMilliseconds()))
            .ToListAsync();

    // ─── Process expired ─────────────────────────────────────
    public async Task ProcessExpiredAsync()
    {
        var expired = await db.AuctionListings
            .Include(a => a.Item)
            .Where(a => a.Status == "active" && a.EndsAt <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var auction in expired)
        {
            if (auction.CurrentBidder.HasValue && auction.CurrentBid > 0)
            {
                // Giao cho người thắng
                await DeliverAuctionItemAsync(auction, auction.CurrentBidder.Value);

                // Cộng tiền seller
                var fee       = (int)(auction.CurrentBid * AuctionFeeRate);
                var sellerNet = auction.CurrentBid - fee;
                await db.Characters
                    .Where(c => c.Id == auction.SellerId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Gold, c => c.Gold + sellerNet));

                auction.Status = "sold";
                logger.LogInformation("Auction sold: id={Id} winner={Winner} amount={Amount}",
                    auction.Id, auction.CurrentBidder, auction.CurrentBid);
            }
            else
            {
                // Không có bid — hoàn trả item
                if (auction.ItemId.HasValue)
                    await AddToInventoryAsync(auction.SellerId,
                        auction.ItemId.Value, auction.Quantity,
                        auction.Item?.MaxStack ?? 99);

                auction.Status = "expired";
            }
        }

        if (expired.Count > 0)
            await db.SaveChangesAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────
    private async Task DeliverAuctionItemAsync(AuctionListing auction, long recipientId)
    {
        if (auction.ListingType == "item" && auction.ItemId.HasValue)
        {
            var item = await db.Items.FindAsync(auction.ItemId.Value);
            await AddToInventoryAsync(recipientId, auction.ItemId.Value,
                auction.Quantity, item?.MaxStack ?? 99);
        }
        else if (auction.ListingType == "pet" && auction.PetId.HasValue)
        {
            await db.Pets
                .Where(p => p.Id == auction.PetId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.OwnerId, recipientId));
        }
    }

    private async Task RemoveFromInventoryAsync(long charId, int itemId, int qty)
    {
        var rows = await db.Inventories
            .Where(i => i.CharacterId == charId && i.ItemId == itemId)
            .OrderBy(i => i.Id)
            .ToListAsync();

        int rem = qty;
        foreach (var row in rows)
        {
            if (rem <= 0) break;
            var take = Math.Min(row.Quantity, rem);
            row.Quantity -= take;
            rem -= take;
            if (row.Quantity == 0) db.Inventories.Remove(row);
        }
    }

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

    private static AuctionListingDto MapToDto(AuctionListing a) => new(
        a.Id, a.ListingType,
        a.ItemId, a.Item?.Name ?? "Unknown",
        a.Item?.Rarity.ToString() ?? "",
        a.Quantity, a.StartPrice, a.BuyoutPrice,
        a.CurrentBid, null,
        a.BidCount, a.Status,
        new DateTimeOffset(a.EndsAt).ToUnixTimeMilliseconds(),
        a.Seller.Name);
}
