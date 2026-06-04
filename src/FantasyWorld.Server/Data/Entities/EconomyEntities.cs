// FantasyWorld.Server/Data/Entities/EconomyEntities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FantasyWorld.Server.Data.Entities;

// ─── NPC Shop ────────────────────────────────────────────────

[Table("npc_shops")]
public class NpcShop
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int    NpcId        { get; set; }
    public int    MapId        { get; set; }
    public int?   RegionId     { get; set; }
    public int?   FactionId    { get; set; }  // null = all factions
    public int    MinReputation { get; set; } // danh tiếng tối thiểu
    public bool   IsActive     { get; set; } = true;

    public ICollection<NpcShopItem> Items { get; set; } = [];
}

[Table("npc_shop_items")]
public class NpcShopItem
{
    [Key] public int Id { get; set; }
    public int  ShopId       { get; set; }
    public int  ItemId       { get; set; }
    public int  BuyPrice     { get; set; }  // giá mua (player mua)
    public int  SellPrice    { get; set; }  // giá bán lại (player bán)
    public int? Stock        { get; set; }  // null = không giới hạn
    public int  SoldToday    { get; set; }
    public int? DailyLimit   { get; set; }
    public int  MinLevel     { get; set; }
    public bool IsAvailable  { get; set; } = true;

    public NpcShop Shop { get; set; } = null!;
    public Item    Item { get; set; } = null!;
}

// ─── Player Shop ─────────────────────────────────────────────

[Table("player_shops")]
public class PlayerShop
{
    [Key] public long Id { get; set; }
    public long   OwnerId    { get; set; }
    [MaxLength(32)] public string ShopType { get; set; } = "general"; // general|pet|fashion|material|food
    [MaxLength(64)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int    MapId       { get; set; }
    public float  PosX        { get; set; }
    public float  PosY        { get; set; }
    public int    Level       { get; set; } = 1;
    public bool   IsOpen      { get; set; } = true;
    public DateTime OpenedAt  { get; set; } = DateTime.UtcNow;

    public Character Owner { get; set; } = null!;
    public ICollection<PlayerShopListing> Listings { get; set; } = [];
}

[Table("player_shop_listings")]
public class PlayerShopListing
{
    [Key] public long Id { get; set; }
    public long  ShopId   { get; set; }
    public long  SellerId { get; set; }
    public int   ItemId   { get; set; }
    public long? PetId    { get; set; }
    public int   Quantity { get; set; } = 1;
    public int   Price    { get; set; }
    public bool  IsSold   { get; set; }
    public DateTime ListedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SoldAt  { get; set; }

    public PlayerShop Shop   { get; set; } = null!;
    public Item       Item   { get; set; } = null!;
    public Pet?       Pet    { get; set; }
}

[Table("player_shop_transactions")]
public class PlayerShopTransaction
{
    [Key] public long Id { get; set; }
    public long   ShopId     { get; set; }
    public long   BuyerId    { get; set; }
    public long   ListingId  { get; set; }
    public int    Quantity   { get; set; }
    public int    PriceEach  { get; set; }
    public int    TotalPrice { get; set; }
    public DateTime BoughtAt { get; set; } = DateTime.UtcNow;

    public Character Buyer { get; set; } = null!;
}

// ─── Auction ─────────────────────────────────────────────────

[Table("auction_house")]
public class AuctionListing
{
    [Key] public long Id { get; set; }
    public long   SellerId      { get; set; }
    [MaxLength(32)] public string ListingType { get; set; } = "item"; // item|pet|fashion|land
    public int?   ItemId        { get; set; }
    public long?  PetId         { get; set; }
    public int?   LandPlotId    { get; set; }
    public int    Quantity      { get; set; } = 1;
    public int    StartPrice    { get; set; }
    public int?   BuyoutPrice   { get; set; }
    public int    CurrentBid    { get; set; }
    public long?  CurrentBidder { get; set; }
    public int    BidCount      { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "active"; // active|sold|cancelled|expired
    public DateTime StartsAt    { get; set; } = DateTime.UtcNow;
    public DateTime EndsAt      { get; set; }

    public Character   Seller  { get; set; } = null!;
    public Item?       Item    { get; set; }
    public Pet?        Pet     { get; set; }
    public ICollection<AuctionBid> Bids { get; set; } = [];
}

[Table("auction_bids")]
public class AuctionBid
{
    [Key] public long Id { get; set; }
    public long   AuctionId { get; set; }
    public long   BidderId  { get; set; }
    public int    Amount    { get; set; }
    public bool   IsWinning { get; set; }
    public DateTime BidAt   { get; set; } = DateTime.UtcNow;

    public AuctionListing Auction { get; set; } = null!;
    public Character      Bidder  { get; set; } = null!;
}

// ─── Market Price ────────────────────────────────────────────

[Table("market_prices")]
public class MarketPrice
{
    [Key] public int Id { get; set; }
    public int    ItemId        { get; set; }
    public int    BasePrice     { get; set; }
    public int    CurrentPrice  { get; set; }
    public int    Supply        { get; set; } = 1000;
    public int    Demand        { get; set; } = 1000;
    public float  Multiplier    { get; set; } = 1.0f;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public Item Item { get; set; } = null!;
}

[Table("price_history")]
public class PriceHistory
{
    [Key] public long Id { get; set; }
    public int  ItemId     { get; set; }
    public int  Price      { get; set; }
    public int  Supply     { get; set; }
    public int  Demand     { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public Item Item { get; set; } = null!;
}

[Table("seasonal_price_modifiers")]
public class SeasonalPriceModifier
{
    [Key] public int Id { get; set; }
    public int    SeasonId   { get; set; }   // 1=spring,2=summer,3=autumn,4=winter
    public int    ItemId     { get; set; }
    public float  Multiplier { get; set; } = 1.0f;
    [MaxLength(128)] public string? Reason { get; set; }

    public Item Item { get; set; } = null!;
}

// ─── Restaurant ──────────────────────────────────────────────

[Table("restaurant_types")]
public class RestaurantType
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string Code { get; set; } = "";
    public int MaxPerServer { get; set; } = 5;

    public ICollection<RestaurantApplication> Applications { get; set; } = [];
}

[Table("restaurant_applications")]
public class RestaurantApplication
{
    [Key] public long Id { get; set; }
    public long   ApplicantId  { get; set; }
    public int    TypeId       { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public string? Description  { get; set; }
    public int    MapId         { get; set; }
    public float  PosX          { get; set; }
    public float  PosY          { get; set; }
    public int    VoteCount     { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "voting"; // voting|approved|rejected
    public DateTime VoteEndsAt  { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Character        Applicant { get; set; } = null!;
    public RestaurantType   Type      { get; set; } = null!;
    public ICollection<RestaurantVote> Votes { get; set; } = [];
    public Restaurant?      Restaurant { get; set; }
}

[Table("restaurant_votes")]
public class RestaurantVote
{
    [Key] public long Id { get; set; }
    public long ApplicationId { get; set; }
    public long VoterId       { get; set; }
    public DateTime VotedAt   { get; set; } = DateTime.UtcNow;

    public RestaurantApplication Application { get; set; } = null!;
    public Character             Voter       { get; set; } = null!;
}

[Table("restaurants")]
public class Restaurant
{
    [Key] public long Id { get; set; }
    public long ApplicationId { get; set; }
    public long OwnerId       { get; set; }
    public int  TypeId        { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int  Level         { get; set; } = 1;
    public int  Reputation    { get; set; }
    public int  MapId         { get; set; }
    public float PosX         { get; set; }
    public float PosY         { get; set; }
    public bool IsOpen        { get; set; } = true;
    public long TotalRevenue  { get; set; }
    public DateTime OpenedAt  { get; set; } = DateTime.UtcNow;

    public Character Owner   { get; set; } = null!;
    public ICollection<RestaurantMenuItem> MenuItems { get; set; } = [];
    public ICollection<RestaurantOrder>    Orders    { get; set; } = [];
}

[Table("restaurant_menu_items")]
public class RestaurantMenuItem
{
    [Key] public long Id { get; set; }
    public long RestaurantId { get; set; }
    public int  ItemId       { get; set; }
    public int  Price        { get; set; }
    public bool IsAvailable  { get; set; } = true;
    public int? DailyLimit   { get; set; }
    public int  SoldToday    { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
    public Item       Item       { get; set; } = null!;
}

[Table("restaurant_orders")]
public class RestaurantOrder
{
    [Key] public long Id { get; set; }
    public long   RestaurantId { get; set; }
    public long?  CustomerId   { get; set; }  // null = NPC
    public long   MenuItemId   { get; set; }
    public int    Quantity     { get; set; } = 1;
    public int    TotalPrice   { get; set; }
    [MaxLength(8)] public string OrderType { get; set; } = "player"; // player|npc
    [MaxLength(16)] public string Status   { get; set; } = "pending"; // pending|served|paid
    public DateTime OrderedAt  { get; set; } = DateTime.UtcNow;

    public Restaurant     Restaurant { get; set; } = null!;
    public RestaurantMenuItem MenuItem { get; set; } = null!;
}

// ─── Market Stall ────────────────────────────────────────────

[Table("market_stalls")]
public class MarketStall
{
    [Key] public int Id { get; set; }
    public int   MapId       { get; set; }
    public int   StallNumber { get; set; }
    public float PosX        { get; set; }
    public float PosY        { get; set; }
    [MaxLength(32)] public string StallType { get; set; } = "general";

    public MarketStallRental? CurrentRental { get; set; }
}

[Table("market_stall_rentals")]
public class MarketStallRental
{
    [Key] public long Id { get; set; }
    public int    StallId     { get; set; }
    public long   RenterId    { get; set; }
    public DateTime RentedFrom { get; set; }
    public DateTime RentedUntil { get; set; }
    public int    RentPriceDay { get; set; } = 50;
    public bool   IsActive    { get; set; } = true;

    public MarketStall Stall  { get; set; } = null!;
    public Character   Renter { get; set; } = null!;
    public ICollection<MarketListing> Listings { get; set; } = [];
}

[Table("market_listings")]
public class MarketListing
{
    [Key] public long Id { get; set; }
    public long   RentalId       { get; set; }
    public long   SellerId       { get; set; }
    [MaxLength(16)] public string ListingType { get; set; } = "item";
    public int?   ItemId         { get; set; }
    public long?  PetId          { get; set; }
    public int    Quantity        { get; set; } = 1;
    public int    Price          { get; set; }
    public bool   IsSold         { get; set; }
    public DateTime ListedAt     { get; set; } = DateTime.UtcNow;
    public DateTime? SoldAt      { get; set; }

    public MarketStallRental Rental { get; set; } = null!;
    public Character         Seller { get; set; } = null!;
    public Item?             Item   { get; set; }
    public Pet?              Pet    { get; set; }
}
