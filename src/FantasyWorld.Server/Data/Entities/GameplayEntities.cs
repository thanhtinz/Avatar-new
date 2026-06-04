// FantasyWorld.Server/Data/Entities/GameplayEntities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FantasyWorld.Server.Data.Entities;

// ─── Faction ─────────────────────────────────────────────────
[Table("character_factions")]
public class CharacterFaction
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  FactionId   { get; set; }
    public int  Rank        { get; set; } = 1;
    public int  Contribution { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public Character Character { get; set; } = null!;
    public Faction   Faction   { get; set; } = null!;
}

[Table("faction_quests")]
public class FactionQuest
{
    [Key] public int Id { get; set; }
    public int    FactionId   { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int    MinRank     { get; set; } = 1;
    public string? ObjectiveJson { get; set; }
    public string? RewardJson   { get; set; }
    public Faction Faction { get; set; } = null!;
}

[Table("character_faction_quests")]
public class CharacterFactionQuest
{
    [Key] public long Id { get; set; }
    public long  CharacterId    { get; set; }
    public int   FactionQuestId { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "active";
    public DateTime? CompletedAt { get; set; }
    public Character    Character    { get; set; } = null!;
    public FactionQuest FactionQuest { get; set; } = null!;
}

[Table("faction_shop_items")]
public class FactionShopItem
{
    [Key] public int Id { get; set; }
    public int  FactionId    { get; set; }
    public int  ItemId       { get; set; }
    public int  MinRank      { get; set; } = 1;
    public int  PriceGold    { get; set; }
    public int  PriceDiamond { get; set; }
    public int? Stock        { get; set; }
    public Faction Faction { get; set; } = null!;
    public Item    Item    { get; set; } = null!;
}

[Table("faction_wars")]
public class FactionWar
{
    [Key] public int Id { get; set; }
    public int   AttackerId { get; set; }
    public int   DefenderId { get; set; }
    public int?  WinnerId   { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "ongoing";
    public string? ScoreJson { get; set; }
    public DateTime StartAt  { get; set; } = DateTime.UtcNow;
    public DateTime EndAt    { get; set; }
    public Faction Attacker { get; set; } = null!;
    public Faction Defender { get; set; } = null!;
}

// ─── Company ─────────────────────────────────────────────────
[Table("company_types")]
public class CompanyType
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int MaxEmployees { get; set; } = 10;
    public string? BonusJson { get; set; }
}

[Table("company_employees")]
public class CompanyEmployee
{
    [Key] public long Id { get; set; }
    public long CompanyId   { get; set; }
    public long CharacterId { get; set; }
    [MaxLength(32)] public string Role { get; set; } = "employee";
    public int  Salary      { get; set; }
    public DateTime? LastPaid { get; set; }
    public Company   Company   { get; set; } = null!;
    public Character Character { get; set; } = null!;
}

[Table("company_transactions")]
public class CompanyTransaction
{
    [Key] public long Id { get; set; }
    public long   CompanyId   { get; set; }
    [MaxLength(32)] public string TxType { get; set; } = "deposit";
    public long   Amount      { get; set; }
    [MaxLength(255)] public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Company Company { get; set; } = null!;
}

// ─── Real Estate ─────────────────────────────────────────────
[Table("land_zones")]
public class LandZone
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int MapId { get; set; }
    [MaxLength(32)] public string ZoneType { get; set; } = "residential";
    public ICollection<LandPlot> Plots { get; set; } = [];
}

[Table("land_transfer_history")]
public class LandTransferHistory
{
    [Key] public long Id { get; set; }
    public int   PlotId    { get; set; }
    public long? FromOwner { get; set; }
    public long  ToOwner   { get; set; }
    public int   Price     { get; set; }
    public DateTime TransferredAt { get; set; } = DateTime.UtcNow;
}

[Table("house_ratings")]
public class HouseRating
{
    [Key] public long Id { get; set; }
    public long  HouseId  { get; set; }
    public long  RaterId  { get; set; }
    public int   Stars    { get; set; } = 3;
    [MaxLength(255)] public string? Comment { get; set; }
    public DateTime RatedAt { get; set; } = DateTime.UtcNow;
    public House   House   { get; set; } = null!;
    public Character Rater { get; set; } = null!;
}

// ─── Museum ──────────────────────────────────────────────────
[Table("museums")]
public class Museum
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int MapId { get; set; }
    public ICollection<MuseumExhibit> Exhibits { get; set; } = [];
}

[Table("museum_exhibits")]
public class MuseumExhibit
{
    [Key] public long Id { get; set; }
    public int   MuseumId { get; set; }
    public int   ItemId   { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string Category { get; set; } = "fish";
    public long? DonorId  { get; set; }
    public DateTime DonatedAt { get; set; } = DateTime.UtcNow;
    public Museum    Museum          { get; set; } = null!;
    public Item      Item            { get; set; } = null!;
    public Character? DonorCharacter { get; set; }
}

[Table("museum_donations")]
public class MuseumDonation
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  MuseumId    { get; set; }
    public int  ItemId      { get; set; }
    public bool RewardClaimed { get; set; }
    public DateTime DonatedAt { get; set; } = DateTime.UtcNow;
    public Character Character { get; set; } = null!;
    public Item      Item      { get; set; } = null!;
}

// ─── Travel ──────────────────────────────────────────────────
[Table("landmarks")]
public class Landmark
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name       { get; set; } = "";
    [MaxLength(64)] public string RegionName { get; set; } = "";
    public int  MapId      { get; set; }
    public float PosX      { get; set; }
    public float PosY      { get; set; }
    public int  ExpReward  { get; set; } = 100;
    public int  GoldReward { get; set; } = 50;
    public ICollection<TravelPassportStamp> Stamps { get; set; } = [];
}

[Table("travel_passports")]
public class TravelPassport
{
    [Key] public long CharacterId { get; set; }
    public int  StampCount { get; set; }
    public Character Character { get; set; } = null!;
}

[Table("travel_passport_stamps")]
public class TravelPassportStamp
{
    [Key] public long Id { get; set; }
    public long CharacterId  { get; set; }
    public int  LandmarkId   { get; set; }
    [MaxLength(64)] public string RegionName { get; set; } = "";
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
    public Character Character { get; set; } = null!;
    public Landmark  Landmark  { get; set; } = null!;
}

[Table("regional_specialties")]
public class RegionalSpecialty
{
    [Key] public int Id { get; set; }
    public int RegionId { get; set; }
    public int ItemId   { get; set; }
    public Item Item { get; set; } = null!;
}

// ─── News ────────────────────────────────────────────────────
[Table("news_articles")]
public class NewsArticle
{
    [Key] public long Id { get; set; }
    public long   AuthorId    { get; set; }
    [MaxLength(100)] public string Title   { get; set; } = "";
    public string Content     { get; set; } = "";
    public int    Likes       { get; set; }
    public bool   IsPinned    { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public Character Author { get; set; } = null!;
}

// ─── Treasure Hunt ───────────────────────────────────────────
[Table("treasure_hunts")]
public class TreasureHunt
{
    [Key] public int Id { get; set; }
    public long  CreatorId   { get; set; }
    [MaxLength(128)] public string Title { get; set; } = "";
    [MaxLength(500)] public string Clue1 { get; set; } = "";
    [MaxLength(500)] public string Clue2 { get; set; } = "";
    [MaxLength(500)] public string Clue3 { get; set; } = "";
    public int   MapId       { get; set; }
    public float TreasureX   { get; set; }
    public float TreasureY   { get; set; }
    public int   GoldReward  { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "active";
    public long? FinderId    { get; set; }
    public DateTime? FoundAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Character Creator { get; set; } = null!;
}

// ─── Guild Territory ─────────────────────────────────────────
[Table("guild_territories")]
public class GuildTerritory
{
    [Key] public int Id { get; set; }
    public int  ClanId      { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int  MapId       { get; set; }
    public int  Tier        { get; set; } = 0;
    [MaxLength(16)] public string TierName { get; set; } = "hamlet";
    public long TaxPool     { get; set; }
    public DateTime? LastTaxAt { get; set; }
    public Clan Clan { get; set; } = null!;
    public ICollection<TerritoryBuilding> Buildings { get; set; } = [];
}

[Table("territory_buildings")]
public class TerritoryBuilding
{
    [Key] public long Id { get; set; }
    public int   TerritoryId  { get; set; }
    [MaxLength(32)] public string BuildingType { get; set; } = "barracks";
    public int   Level   { get; set; } = 1;
    public DateTime BuiltAt { get; set; } = DateTime.UtcNow;
    public GuildTerritory Territory { get; set; } = null!;
}

// ─── Rare Profession ─────────────────────────────────────────
[Table("rare_professions")]
public class RareProfession
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public string? Description    { get; set; }
    public int  ServerSlotLimit   { get; set; } = 5;
    public int  RequiredLevel     { get; set; } = 50;
    public string? RequiredConditionJson { get; set; }
    public string? BonusJson      { get; set; }
}

[Table("character_rare_professions")]
public class CharacterRareProfession
{
    [Key] public long Id { get; set; }
    public long  CharacterId  { get; set; }
    public int   ProfessionId { get; set; }
    public bool  IsActive     { get; set; } = true;
    public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;
    public Character     Character  { get; set; } = null!;
    public RareProfession Profession { get; set; } = null!;
}

// ─── Deity ───────────────────────────────────────────────────
[Table("deities")]
public class Deity
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? BlessingJson { get; set; }
    public string? LoreVi       { get; set; }
    public string? LoreEn       { get; set; }
    public ICollection<DeityQuest> Quests { get; set; } = [];
}

[Table("character_deities")]
public class CharacterDeity
{
    [Key] public long CharacterId { get; set; }
    public int   DeityId      { get; set; }
    public int   Devotion     { get; set; }
    [MaxLength(16)] public string Tier { get; set; } = "follower";
    public DateTime? LastPrayedAt { get; set; }
    public DateTime ChosenAt  { get; set; } = DateTime.UtcNow;
    public Character Character { get; set; } = null!;
    public Deity     Deity     { get; set; } = null!;
}

[Table("deity_quests")]
public class DeityQuest
{
    [Key] public int Id { get; set; }
    public int    DeityId    { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(16)] public string MinTier { get; set; } = "follower";
    public string? RewardJson { get; set; }
    public Deity Deity { get; set; } = null!;
}

// ─── Civil Profession ────────────────────────────────────────
[Table("civil_professions")]
public class CivilProfession
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name     { get; set; } = "";
    [MaxLength(32)] public string WorkType { get; set; } = "music";
    public int  RequiredLevel  { get; set; } = 1;
    public int  UnlockCost     { get; set; } = 1_000;
    public string? BonusJson   { get; set; }
}

[Table("character_civil_professions")]
public class CharacterCivilProfession
{
    [Key] public long Id { get; set; }
    public long  CharacterId  { get; set; }
    public int   ProfessionId { get; set; }
    public int   Mastery      { get; set; } = 0;
    public int   TotalPractices { get; set; }
    public DateTime? LastPracticeAt { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    public Character      Character  { get; set; } = null!;
    public CivilProfession Profession { get; set; } = null!;
}

// ─── Leaderboard ─────────────────────────────────────────────
[Table("leaderboard_snapshots")]
public class LeaderboardSnapshot
{
    [Key] public long Id { get; set; }
    [MaxLength(32)] public string Category { get; set; } = "";
    public int      Rank        { get; set; }
    public long     CharacterId { get; set; }
    public long     Score       { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public Character Character  { get; set; } = null!;
}

// ─── Endgame Realm ───────────────────────────────────────────
[Table("endgame_realms")]
public class EndgameRealm
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name  { get; set; } = "";
    [MaxLength(64)] public string NameEn { get; set; } = "";
    public string? Description  { get; set; }
    public int  RequiredLevel   { get; set; } = 100;
    public string? RequiredConditionJson { get; set; }
    public int  EntryMapId      { get; set; }
    public float EntryX         { get; set; }
    public float EntryY         { get; set; }
}

[Table("character_endgame_realms")]
public class CharacterEndgameRealm
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  RealmId     { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    public Character    Character { get; set; } = null!;
    public EndgameRealm Realm     { get; set; } = null!;
}

// ─── Festival ────────────────────────────────────────────────
[Table("festivals")]
public class Festival
{
    [Key] public int Id { get; set; }
    public int  Year    { get; set; }
    public int  Month   { get; set; }
    public int  Day     { get; set; }
    [MaxLength(64)] public string NameVi { get; set; } = "";
    [MaxLength(64)] public string NameEn { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt   { get; set; }
    public bool IsActive    { get; set; }
    public long BaseRewardGold { get; set; } = 500;
    public int  BaseRewardExp  { get; set; } = 200;
    public ICollection<FestivalTask> Tasks { get; set; } = [];
}

[Table("festival_tasks")]
public class FestivalTask
{
    [Key] public int Id { get; set; }
    public int    FestivalId   { get; set; }
    [MaxLength(128)] public string Title { get; set; } = "";
    public string? ObjectiveJson { get; set; }
    [NotMapped] public bool IsCompletedByPlayer { get; set; }
    public Festival Festival { get; set; } = null!;
}

[Table("character_festival_participants")]
public class CharacterFestivalParticipant
{
    [Key] public long Id { get; set; }
    public long CharacterId  { get; set; }
    public int  FestivalId   { get; set; }
    public bool RewardClaimed { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public Character Character { get; set; } = null!;
    public Festival  Festival  { get; set; } = null!;
}

[Table("character_festival_tasks")]
public class CharacterFestivalTask
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  FestivalId  { get; set; }
    public int  TaskId      { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public Character    Character { get; set; } = null!;
    public FestivalTask Task      { get; set; } = null!;
}

// ─── UGC ─────────────────────────────────────────────────────
[Table("ugc_contents")]
public class UgcContent
{
    [Key] public long Id { get; set; }
    public long  CreatorId    { get; set; }
    [MaxLength(32)] public string ContentType { get; set; } = "house";
    [MaxLength(128)] public string Title      { get; set; } = "";
    public string DataJson    { get; set; } = "{}";
    public bool  IsPublished  { get; set; }
    public int   VisitCount   { get; set; }
    public float Rating       { get; set; }
    public int   RatingCount  { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public Character Creator { get; set; } = null!;
    public ICollection<UgcRating> Ratings { get; set; } = [];
}

[Table("ugc_ratings")]
public class UgcRating
{
    [Key] public long Id { get; set; }
    public long ContentId { get; set; }
    public long RaterId   { get; set; }
    public int  Stars     { get; set; } = 3;
    public UgcContent Content { get; set; } = null!;
    public Character  Rater   { get; set; } = null!;
}

// ─── Citizen Card ────────────────────────────────────────────
[Table("citizen_card_views")]
public class CitizenCardView
{
    [Key] public long Id { get; set; }
    public long ViewerId { get; set; }
    public long TargetId { get; set; }
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    public Character Viewer { get; set; } = null!;
    public Character Target { get; set; } = null!;
}

// ─── Hotel ───────────────────────────────────────────────────
[Table("hotels")]
public class Hotel
{
    [Key] public long Id { get; set; }
    public long?  OwnerId    { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int    MapId      { get; set; }
    public bool   IsOpen     { get; set; } = true;
    public Character? Owner  { get; set; }
    public ICollection<HotelRoom> Rooms { get; set; } = [];
}

[Table("hotel_rooms")]
public class HotelRoom
{
    [Key] public long Id { get; set; }
    public long  HotelId     { get; set; }
    public int   RoomNumber  { get; set; }
    public int   PricePerDay { get; set; } = 100;
    [MaxLength(32)] public string RoomType { get; set; } = "standard";
    public string? CustomDecorJson { get; set; }
    public Hotel        Hotel         { get; set; } = null!;
    public HotelRental? ActiveRental  { get; set; }
}

[Table("hotel_rentals")]
public class HotelRental
{
    [Key] public long Id { get; set; }
    public long  RoomId     { get; set; }
    public long  RenterId   { get; set; }
    public int   TotalCost  { get; set; }
    public DateTime CheckinAt  { get; set; }
    public DateTime CheckoutAt { get; set; }
    public HotelRoom  Room   { get; set; } = null!;
    public Character  Renter { get; set; } = null!;
}
