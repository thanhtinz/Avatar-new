// FantasyWorld.Server/Data/Entities/Entities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Data.Entities;

// ─── Account ─────────────────────────────────────────────────

[Table("accounts")]
public class Account
{
    [Key] public long Id { get; set; }
    [MaxLength(32)]  public string Username     { get; set; } = "";
    [MaxLength(255)] public string PasswordHash { get; set; } = "";
    [MaxLength(128)] public string? Email       { get; set; }
    [MaxLength(20)]  public string? Phone       { get; set; }
    public AccountRole Role      { get; set; } = AccountRole.Player;
    public bool       IsBanned   { get; set; }
    [MaxLength(255)] public string? BanReason   { get; set; }
    public DateTime?  BanUntil   { get; set; }
    [MaxLength(45)]  public string? LastIp      { get; set; }
    public DateTime?  LastLogin  { get; set; }
    public DateTime   CreatedAt  { get; set; } = DateTime.UtcNow;

    public ICollection<Character>  Characters { get; set; } = [];
    public ICollection<Session>    Sessions   { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}

// ─── Session ─────────────────────────────────────────────────

[Table("sessions")]
public class Session
{
    [Key] public long Id { get; set; }
    public long AccountId { get; set; }
    [MaxLength(512)] public string Token { get; set; } = "";
    public ClientType ClientType { get; set; } = ClientType.Unity;
    [MaxLength(45)]  public string? IpAddress  { get; set; }
    [MaxLength(255)] public string? DeviceInfo { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt  { get; set; }

    public Account Account { get; set; } = null!;
}

// ─── Character ───────────────────────────────────────────────

[Table("characters")]
public class Character
{
    [Key] public long Id { get; set; }
    public long AccountId { get; set; }
    [MaxLength(32)] public string Name { get; set; } = "";
    public Gender Gender      { get; set; } = Gender.Male;

    // Appearance
    public int HairStyle  { get; set; }
    public int HairColor  { get; set; }
    public int FaceStyle  { get; set; }
    public int BodyStyle  { get; set; }
    public int SkinColor  { get; set; }

    // Stats
    public int  Level  { get; set; } = 1;
    public long Exp    { get; set; }
    public long Gold   { get; set; }
    public int  Diamond { get; set; }

    // Position
    public int   MapId { get; set; } = 1;
    public float PosX  { get; set; } = 100f;
    public float PosY  { get; set; } = 100f;

    // Life stats
    public byte Hunger { get; set; } = 100;
    public byte Energy { get; set; } = 100;
    public byte Mood   { get; set; } = 100;

    // Combat stats
    public int Hp    { get; set; } = 100;
    public int HpMax { get; set; } = 100;
    public int Mp    { get; set; } = 50;
    public int MpMax { get; set; } = 50;
    public int Atk   { get; set; } = 10;
    public int Def   { get; set; } = 5;
    public int Spd   { get; set; } = 10;

    public long     OnlineTime { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? LastOnline { get; set; }

    // Navigation
    public Account           Account        { get; set; } = null!;
    public EquipmentSlot?    EquipmentSlot  { get; set; }
    public CharacterFameStat? FameStat      { get; set; }
    public FishingRecord?    FishingRecord  { get; set; }
    public CitizenCard?      CitizenCard    { get; set; }
    public ICollection<InventoryItem>  Inventory     { get; set; } = [];
    public ICollection<CharacterSkill> Skills        { get; set; } = [];
    public ICollection<CharacterQuest> Quests        { get; set; } = [];
    public ICollection<Pet>            Pets          { get; set; } = [];
    public ICollection<ClanMember>     ClanMemberships { get; set; } = [];
    public ICollection<CharacterAcademy> AcademyEnrollments { get; set; } = [];
    public ICollection<CharacterFaction> FactionMemberships { get; set; } = [];
}

// ─── Equipment Slots ─────────────────────────────────────────

[Table("equipment_slots")]
public class EquipmentSlot
{
    [Key] public long CharacterId { get; set; }
    public long? Head      { get; set; }
    public long? Chest     { get; set; }
    public long? Legs      { get; set; }
    public long? Boots     { get; set; }
    public long? Weapon    { get; set; }
    public long? Offhand   { get; set; }
    public long? Accessory1 { get; set; }
    public long? Accessory2 { get; set; }

    public Character Character { get; set; } = null!;
}

// ─── Item ────────────────────────────────────────────────────

[Table("items")]
public class Item
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public ItemType   ItemType   { get; set; }
    public ItemRarity Rarity     { get; set; } = ItemRarity.Common;
    public string?    Description { get; set; }
    public int?       IconId     { get; set; }
    public bool       Stackable  { get; set; } = true;
    public int        MaxStack   { get; set; } = 99;
    public int        SellPrice  { get; set; }
    public int        BuyPrice   { get; set; }
    public string?    StatsJson  { get; set; }
    public string?    EffectJson { get; set; }
    public int        LevelReq   { get; set; }

    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
}

// ─── Inventory ───────────────────────────────────────────────

[Table("inventories")]
public class InventoryItem
{
    [Key] public long Id { get; set; }
    public long CharacterId  { get; set; }
    public int  ItemId       { get; set; }
    public int  Quantity     { get; set; } = 1;
    public int  Slot         { get; set; }
    public int  EnchantLevel { get; set; }
    public int  Durability   { get; set; } = 100;
    public string? CustomJson { get; set; }
    public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
    public Item       Item      { get; set; } = null!;
}

// ─── Map ─────────────────────────────────────────────────────

[Table("maps")]
public class Map
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public MapType MapType  { get; set; } = MapType.Field;
    public int?    RegionId { get; set; }
    public int     Width    { get; set; } = 100;
    public int     Height   { get; set; } = 100;
    [MaxLength(64)] public string? BgMusic  { get; set; }
    public int     MinLevel { get; set; }
    public bool    IsPvp    { get; set; }
    public bool    IsActive { get; set; } = true;

    public Region?           Region   { get; set; }
    public ICollection<Portal> Portals { get; set; } = [];
}

// ─── Portal ──────────────────────────────────────────────────

[Table("portals")]
public class Portal
{
    [Key] public int Id { get; set; }
    public int   FromMapId { get; set; }
    public float FromPosX  { get; set; }
    public float FromPosY  { get; set; }
    public int   ToMapId   { get; set; }
    public float ToPosX    { get; set; }
    public float ToPosY    { get; set; }
    [MaxLength(64)] public string? Name  { get; set; }
    [MaxLength(32)] public string  PortalType { get; set; } = "free";
    public int  Fare     { get; set; }
    public int  MinLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public Map FromMap { get; set; } = null!;
    public Map ToMap   { get; set; } = null!;
}

// ─── Region ──────────────────────────────────────────────────

[Table("regions")]
public class Region
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    [MaxLength(64)] public string? Icon { get; set; }
    public ICollection<Map> Maps { get; set; } = [];
}

// ─── Pet Species ─────────────────────────────────────────────

[Table("pet_species")]
public class PetSpecies
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name    { get; set; } = "";
    public PetRarity Rarity   { get; set; } = PetRarity.Common;
    public Element   Element  { get; set; } = Element.None;
    public int BaseHp         { get; set; } = 100;
    public int BaseAtk        { get; set; } = 10;
    public int BaseDef        { get; set; } = 5;
    public int BaseSpd        { get; set; } = 10;
    public int CatchRate      { get; set; } = 50;
    public int? SpriteId      { get; set; }
    public string? HabitatMapIds { get; set; }

    public ICollection<Pet> Pets { get; set; } = [];
}

// ─── Pet ─────────────────────────────────────────────────────

[Table("pets")]
public class Pet
{
    [Key] public long Id { get; set; }
    public long OwnerId    { get; set; }
    public int  SpeciesId  { get; set; }
    [MaxLength(32)] public string? Nickname { get; set; }
    [MaxLength(16)] public string Personality { get; set; } = "calm";
    public int  ColorVariant { get; set; }
    public bool IsShiny      { get; set; }
    public int  Level        { get; set; } = 1;
    public int  Exp          { get; set; }
    public int  Happiness    { get; set; } = 50;
    public int  Hunger       { get; set; } = 100;
    public int  Hp           { get; set; } = 100;
    public int  HpMax        { get; set; } = 100;
    public int  Atk          { get; set; } = 10;
    public int  Def          { get; set; } = 5;
    public int  Spd          { get; set; } = 10;
    public bool IsActive     { get; set; }
    public DateTime CaughtAt { get; set; } = DateTime.UtcNow;
    public int? CaughtMapId  { get; set; }

    public Character  Owner   { get; set; } = null!;
    public PetSpecies Species { get; set; } = null!;
}

// ─── Clan ────────────────────────────────────────────────────

[Table("clans")]
public class Clan
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public long LeaderId   { get; set; }
    [MaxLength(64)] public string? Emblem { get; set; }
    public string? Description   { get; set; }
    public int  Level            { get; set; } = 1;
    public int  MaxMembers       { get; set; } = 20;
    public long Gold             { get; set; }
    public int? HouseMapId       { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

    public ICollection<ClanMember> Members { get; set; } = [];
}

[Table("clan_members")]
public class ClanMember
{
    [Key] public long Id { get; set; }
    public int  ClanId      { get; set; }
    public long CharacterId { get; set; }
    [MaxLength(16)] public string Role { get; set; } = "member";
    public int  Contribution  { get; set; }
    public DateTime JoinedAt  { get; set; } = DateTime.UtcNow;

    public Clan      Clan      { get; set; } = null!;
    public Character Character { get; set; } = null!;
}

// ─── Academy ─────────────────────────────────────────────────

[Table("academies")]
public class Academy
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name    { get; set; } = "";
    public Element Element     { get; set; }
    public string? Description { get; set; }
    public string? BonusJson   { get; set; }

    public ICollection<CharacterAcademy> Enrollments { get; set; } = [];
}

[Table("character_academy")]
public class CharacterAcademy
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  AcademyId   { get; set; }
    public int  EnrolledAt_Unix { get; set; }
    public int  Grade       { get; set; } = 1;
    public int  RankPoints  { get; set; }
    public int  ExamPasses  { get; set; }

    public Character Character { get; set; } = null!;
    public Academy   Academy   { get; set; } = null!;
}

// ─── Faction ─────────────────────────────────────────────────

[Table("factions")]
public class Faction
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name        { get; set; } = "";
    [MaxLength(32)] public string Code        { get; set; } = "";
    public string? Description    { get; set; }
    [MaxLength(7)] public string? ColorHex    { get; set; }
    public int? CapitalMapId      { get; set; }

    public ICollection<CharacterFaction> Members { get; set; } = [];
}

[Table("character_faction")]
public class CharacterFaction
{
    [Key] public long Id { get; set; }
    public long CharacterId  { get; set; }
    public int  FactionId    { get; set; }
    public int  Rank         { get; set; } = 1;
    public int  Contribution { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
    public Faction   Faction   { get; set; } = null!;
}

// ─── Skill ───────────────────────────────────────────────────

[Table("skills")]
public class Skill
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(16)] public string SkillType { get; set; } = "active";
    public Element Element   { get; set; } = Element.None;
    public int? AcademyId    { get; set; }
    public int? JobId        { get; set; }
    public string? Description { get; set; }
    public int MpCost        { get; set; } = 10;
    public int CooldownS     { get; set; }
    public int Power         { get; set; }
    public string? EffectJson { get; set; }
    public int LearnLevel    { get; set; } = 1;

    public ICollection<CharacterSkill> CharacterSkills { get; set; } = [];
}

[Table("character_skills")]
public class CharacterSkill
{
    [Key] public long Id { get; set; }
    public long CharacterId { get; set; }
    public int  SkillId     { get; set; }
    public int  SkillLevel  { get; set; } = 1;
    public bool IsEquipped  { get; set; }
    public DateTime LearnedAt { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
    public Skill     Skill     { get; set; } = null!;
}

// ─── Quest ───────────────────────────────────────────────────

[Table("quests")]
public class Quest
{
    [Key] public int Id { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(32)]  public string QuestType { get; set; } = "side";
    public string? Description   { get; set; }
    public string? ObjectiveJson { get; set; }
    public string? RewardJson    { get; set; }
    public int  MinLevel         { get; set; } = 1;
    public bool IsRepeatable     { get; set; }

    public ICollection<CharacterQuest> CharacterQuests { get; set; } = [];
}

[Table("character_quests")]
public class CharacterQuest
{
    [Key] public long Id { get; set; }
    public long CharacterId   { get; set; }
    public int  QuestId       { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "active";
    public string?   ProgressJson { get; set; }
    public DateTime  AcceptedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt  { get; set; }

    public Character Character { get; set; } = null!;
    public Quest     Quest     { get; set; } = null!;
}

// ─── Message ─────────────────────────────────────────────────

[Table("messages")]
public class Message
{
    [Key] public long Id { get; set; }
    public long       SenderId   { get; set; }
    public long?      ReceiverId { get; set; }
    public ChatChannel Channel   { get; set; } = ChatChannel.Map;
    public string     Content    { get; set; } = "";
    public DateTime   SentAt     { get; set; } = DateTime.UtcNow;
    public bool       IsRead     { get; set; }

    public Character Sender { get; set; } = null!;
}

// ─── Festival ────────────────────────────────────────────────

[Table("festivals")]
public class Festival
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public string?  Description   { get; set; }
    public DateTime StartDate     { get; set; }
    public DateTime EndDate       { get; set; }
    public int?     MapId         { get; set; }
    public string?  RewardJson    { get; set; }
    public bool     IsActive      { get; set; }
}

// ─── World Event ─────────────────────────────────────────────

[Table("world_events")]
public class WorldEvent
{
    [Key] public int Id { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(64)]  public string EventType { get; set; } = "";
    public string? Description      { get; set; }
    [MaxLength(32)] public string TriggerType { get; set; } = "random";
    public float   TriggerChance    { get; set; } = 0.01f;
    public int     DurationMin      { get; set; } = 60;
    public string? AffectedMaps     { get; set; }
    public string? RewardJson       { get; set; }
    [MaxLength(255)] public string? AnnouncementMsg { get; set; }
    public int     CooldownHours    { get; set; } = 24;
    public bool    IsActive         { get; set; } = true;

    public ICollection<WorldEventInstance> Instances { get; set; } = [];
}

[Table("world_event_instances")]
public class WorldEventInstance
{
    [Key] public long Id { get; set; }
    public int   EventId      { get; set; }
    public long? TriggeredBy  { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "announced";
    public DateTime  StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAt    { get; set; }
    public int       Participants { get; set; }

    public WorldEvent Event { get; set; } = null!;
}

// ─── Transaction ─────────────────────────────────────────────

[Table("transactions")]
public class Transaction
{
    [Key] public long Id { get; set; }
    public long    AccountId     { get; set; }
    [MaxLength(16)] public string Type { get; set; } = "topup";
    public long?   AmountVnd     { get; set; }
    public int?    DiamondAmount { get; set; }
    [MaxLength(16)] public string PaymentMethod { get; set; } = "vnpay";
    [MaxLength(16)] public string Status { get; set; } = "pending";
    [MaxLength(64)] public string? RefCode { get; set; }
    public string?  Note          { get; set; }
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;

    public Account Account { get; set; } = null!;
}

// ─── Supporting small tables ─────────────────────────────────

[Table("character_fame_stats")]
public class CharacterFameStat
{
    [Key] public long CharacterId { get; set; }
    public long TotalGoldEarned  { get; set; }
    public int  HouseScore       { get; set; }
    public int  FishCaught       { get; set; }
    public int  ItemsCollected   { get; set; }
    public int  FashionScore     { get; set; }
    public int  RecipesMastered  { get; set; }
    public long TradeVolume      { get; set; }
    public int  PetsOwned        { get; set; }
    public int  CardsOwned       { get; set; }
    public int  LandmarksVisited { get; set; }

    public Character Character { get; set; } = null!;
}

[Table("fishing_records")]
public class FishingRecord
{
    [Key] public long CharacterId  { get; set; }
    public int   TotalCaught       { get; set; }
    public float TotalWeight       { get; set; }
    public int?  BiggestFishId     { get; set; }
    public float BiggestWeight     { get; set; }
    public int?  RarestFishId      { get; set; }
    public string SpeciesCaughtJson { get; set; } = "[]";

    public Character Character { get; set; } = null!;
}

[Table("citizen_cards")]
public class CitizenCard
{
    [Key] public long CharacterId { get; set; }
    [MaxLength(20)] public string? CardNumber { get; set; }
    [MaxLength(255)] public string? Bio        { get; set; }
    public string?  HobbiesJson        { get; set; }
    public long?    FavoritePetId      { get; set; }
    public int?     FavoriteRegionId   { get; set; }
    public int?     FavoriteFoodItem   { get; set; }
    public int?     ActiveTitleId      { get; set; }
    [MaxLength(7)] public string ThemeColor { get; set; } = "#4A90D9";
    public bool     IsPublic   { get; set; } = true;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
}
