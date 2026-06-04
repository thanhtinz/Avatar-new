// FantasyWorld.Shared/DTOs/EconomyDtos.cs
using MessagePack;

namespace FantasyWorld.Shared.DTOs;

// ─── Shop ────────────────────────────────────────────────────

[MessagePackObject]
public record NpcShopItemDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] int    ItemId,
    [property: Key(2)] string ItemName,
    [property: Key(3)] string Rarity,
    [property: Key(4)] int    BuyPrice,
    [property: Key(5)] int    SellPrice,
    [property: Key(6)] int?   Stock,
    [property: Key(7)] bool   IsAvailable
);

[MessagePackObject]
public record BuyItemRequest(
    [property: Key(0)] int ShopItemId,
    [property: Key(1)] int Quantity
);

[MessagePackObject]
public record SellItemRequest(
    [property: Key(0)] int ShopId,
    [property: Key(1)] int InventoryId,
    [property: Key(2)] int Quantity
);

// ─── Player Shop ─────────────────────────────────────────────

[MessagePackObject]
public record PlayerShopDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string ShopType,
    [property: Key(3)] string OwnerName,
    [property: Key(4)] int    Level,
    [property: Key(5)] bool   IsOpen,
    [property: Key(6)] int    ListingCount
);

[MessagePackObject]
public record PlayerShopListingDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] int    ItemId,
    [property: Key(2)] string ItemName,
    [property: Key(3)] string Rarity,
    [property: Key(4)] int    Quantity,
    [property: Key(5)] int    Price,
    [property: Key(6)] string SellerName
);

// ─── Auction ─────────────────────────────────────────────────

[MessagePackObject]
public record AuctionListingDto(
    [property: Key(0)]  long   Id,
    [property: Key(1)]  string ListingType,
    [property: Key(2)]  int?   ItemId,
    [property: Key(3)]  string ItemName,
    [property: Key(4)]  string Rarity,
    [property: Key(5)]  int    Quantity,
    [property: Key(6)]  int    StartPrice,
    [property: Key(7)]  int?   BuyoutPrice,
    [property: Key(8)]  int    CurrentBid,
    [property: Key(9)]  string? CurrentBidderName,
    [property: Key(10)] int    BidCount,
    [property: Key(11)] string Status,
    [property: Key(12)] long   EndsAtMs,
    [property: Key(13)] string SellerName
);

[MessagePackObject]
public record PlaceBidRequest(
    [property: Key(0)] long AuctionId,
    [property: Key(1)] int  Amount
);

[MessagePackObject]
public record CreateAuctionRequest(
    [property: Key(0)] string ListingType,
    [property: Key(1)] int?   ItemId,
    [property: Key(2)] long?  PetId,
    [property: Key(3)] int    Quantity,
    [property: Key(4)] int    StartPrice,
    [property: Key(5)] int?   BuyoutPrice,
    [property: Key(6)] int    DurationHours  // 1-72h
);

[MessagePackObject]
public record AuctionBidDto(
    [property: Key(0)] long   BidderId,
    [property: Key(1)] string BidderName,
    [property: Key(2)] int    Amount,
    [property: Key(3)] long   BidAtMs
);

// ─── Market Price ────────────────────────────────────────────

[MessagePackObject]
public record MarketPriceDto(
    [property: Key(0)] int   ItemId,
    [property: Key(1)] string ItemName,
    [property: Key(2)] int   BasePrice,
    [property: Key(3)] int   CurrentPrice,
    [property: Key(4)] int   Supply,
    [property: Key(5)] int   Demand,
    [property: Key(6)] float PriceTrend  // -1.0 to 1.0
);

// ─── Restaurant ──────────────────────────────────────────────

[MessagePackObject]
public record RestaurantDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string TypeName,
    [property: Key(3)] string OwnerName,
    [property: Key(4)] int    Level,
    [property: Key(5)] int    Reputation,
    [property: Key(6)] bool   IsOpen,
    [property: Key(7)] int    MenuItemCount
);

[MessagePackObject]
public record RestaurantMenuItemDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] int    ItemId,
    [property: Key(2)] string ItemName,
    [property: Key(3)] int    Price,
    [property: Key(4)] bool   IsAvailable,
    [property: Key(5)] int?   DailyLimit,
    [property: Key(6)] int    SoldToday
);

[MessagePackObject]
public record ApplyRestaurantRequest(
    [property: Key(0)] int    TypeId,
    [property: Key(1)] string Name,
    [property: Key(2)] string? Description,
    [property: Key(3)] int    MapId
);

[MessagePackObject]
public record OrderRequest(
    [property: Key(0)] long RestaurantId,
    [property: Key(1)] long MenuItemId,
    [property: Key(2)] int  Quantity
);

// ─── Market Stall ────────────────────────────────────────────

[MessagePackObject]
public record MarketStallDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] int    StallNumber,
    [property: Key(2)] string StallType,
    [property: Key(3)] bool   IsAvailable,
    [property: Key(4)] string? RenterName,
    [property: Key(5)] long?  RentedUntilMs
);

[MessagePackObject]
public record RentStallRequest(
    [property: Key(0)] int StallId,
    [property: Key(1)] int Days
);

[MessagePackObject]
public record CreateMarketListingRequest(
    [property: Key(0)] string ListingType,
    [property: Key(1)] int?   ItemId,
    [property: Key(2)] long?  PetId,
    [property: Key(3)] int    Quantity,
    [property: Key(4)] int    Price
);
