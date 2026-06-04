// FantasyWorld.Server/Data/Entities/EntertainmentEntities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FantasyWorld.Server.Data.Entities;

// ─── Casino ──────────────────────────────────────────────────

[Table("casino_games")]
public class CasinoGame
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string GameType { get; set; } = "slots"; // slots|roulette|card_flip|wheel|dice
    public int   MinBet    { get; set; } = 10;
    public int   MaxBet    { get; set; } = 10_000;
    public float HouseEdge { get; set; } = 0.05f;
    public bool  IsActive  { get; set; } = true;

    public ICollection<CasinoLog> Logs { get; set; } = [];
}

[Table("casino_logs")]
public class CasinoLog
{
    [Key] public long Id { get; set; }
    public long  CharacterId { get; set; }
    public int   GameId      { get; set; }
    public int   BetAmount   { get; set; }
    public string? ResultJson { get; set; }
    public int   Payout      { get; set; }
    public int   NetChange   { get; set; }  // dương=win, âm=loss
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public Character   Character { get; set; } = null!;
    public CasinoGame  Game      { get; set; } = null!;
}

[Table("casino_daily_limits")]
public class CasinoDailyLimit
{
    [Key] public long Id { get; set; }
    public long CharacterId  { get; set; }
    public int  TotalBetToday { get; set; }
    public int  TotalLossToday { get; set; }
    public Date Date          { get; set; }

    public Character Character { get; set; } = null!;
}

// ─── Mini Games ──────────────────────────────────────────────

[Table("mini_games")]
public class MiniGame
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string GameType { get; set; } = "puzzle";
    // puzzle|rhythm|memory|timed_collect|maze|tower_defense
    public string? Description  { get; set; }
    public int  EntryFee        { get; set; }
    public string? RewardJson   { get; set; }
    public string? HighScoreRewardJson { get; set; }
    public int  CooldownMin     { get; set; } = 30;
    public bool IsActive        { get; set; } = true;

    public ICollection<MiniGameScore> Scores { get; set; } = [];
}

[Table("mini_game_scores")]
public class MiniGameScore
{
    [Key] public long Id { get; set; }
    public long  CharacterId { get; set; }
    public int   MiniGameId  { get; set; }
    public int   Score       { get; set; }
    public int   BestScore   { get; set; }
    public int   PlayCount   { get; set; }
    public DateTime? LastPlayed { get; set; }

    public Character Character { get; set; } = null!;
    public MiniGame  MiniGame  { get; set; } = null!;
}

// ─── Mount Race ──────────────────────────────────────────────

[Table("mount_race_tracks")]
public class MountRaceTrack
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int  MapId      { get; set; }
    public int  LengthM    { get; set; } = 500;
    public int  Obstacles  { get; set; } = 5;
    public int  MinLevel   { get; set; } = 1;
    public string? TrackLayoutJson { get; set; }  // checkpoint positions

    public ICollection<MountRace> Races { get; set; } = [];
}

[Table("mount_races")]
public class MountRace
{
    [Key] public long Id { get; set; }
    public int   TrackId    { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "waiting"; // waiting|countdown|racing|finished
    public int   EntryFee   { get; set; } = 100;
    public int   MaxRacers  { get; set; } = 8;
    public string? RewardJson { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public MountRaceTrack Track   { get; set; } = null!;
    public ICollection<MountRaceEntry> Entries { get; set; } = [];
}

[Table("mount_race_entries")]
public class MountRaceEntry
{
    [Key] public long Id { get; set; }
    public long  RaceId          { get; set; }
    public long  CharacterId     { get; set; }
    public long? MountPetId      { get; set; }
    public int?  FinishPosition  { get; set; }
    public int?  FinishTimeMs    { get; set; }
    public string? RewardEarned  { get; set; }

    public MountRace Race      { get; set; } = null!;
    public Character Character { get; set; } = null!;
    public Pet?      MountPet  { get; set; }
}

// ─── Fashion ─────────────────────────────────────────────────

[Table("fashion_items")]
public class FashionItem
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string Slot { get; set; } = "top";
    // top|bottom|shoes|hat|glasses|cape|wings|aura|full_set|accessory
    [MaxLength(16)] public string Rarity { get; set; } = "common";
    public int?  FactionId   { get; set; }
    [MaxLength(32)] public string Source { get; set; } = "shop"; // shop|craft|event|designer|gacha
    public long? DesignerId  { get; set; }
    public int   PriceGold   { get; set; }
    public int   PriceDiamond { get; set; }
    public string? PreviewJson { get; set; }
    public bool  Dyeable     { get; set; } = true;
    public bool  HasEffect   { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Character? Designer { get; set; }
}

[Table("character_wardrobe")]
public class WardrobeItem
{
    [Key] public long Id { get; set; }
    public long  CharacterId    { get; set; }
    public int   FashionItemId  { get; set; }
    [MaxLength(7)] public string? DyeColorHex { get; set; }
    public int?  EffectId       { get; set; }
    public DateTime ObtainedAt  { get; set; } = DateTime.UtcNow;

    public Character   Character   { get; set; } = null!;
    public FashionItem FashionItem { get; set; } = null!;
}

[Table("character_outfit")]
public class CharacterOutfit
{
    [Key] public long CharacterId { get; set; }
    public int? TopId       { get; set; }
    public int? BottomId    { get; set; }
    public int? ShoesId     { get; set; }
    public int? HatId       { get; set; }
    public int? GlassesId   { get; set; }
    public int? CapeId      { get; set; }
    public int? WingsId     { get; set; }
    public int? AuraId      { get; set; }
    public string? DyeJson  { get; set; }     // {"top":"#FF4444","wings":"#00AAFF"}
    public string? EffectJson { get; set; }   // {"wings":"sparkling","aura":"flames"}

    public Character Character { get; set; } = null!;
}

[Table("outfit_presets")]
public class OutfitPreset
{
    [Key] public long Id { get; set; }
    public long  CharacterId  { get; set; }
    [MaxLength(32)] public string PresetName { get; set; } = "";
    public int   SlotNumber   { get; set; } = 1;
    public string OutfitJson  { get; set; } = "{}";
    public DateTime SavedAt   { get; set; } = DateTime.UtcNow;

    public Character Character { get; set; } = null!;
}

[Table("fashion_designs")]
public class FashionDesign
{
    [Key] public long Id { get; set; }
    public long  DesignerId  { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string Slot { get; set; } = "top";
    public string DesignData  { get; set; } = "{}";
    public string? ThumbnailUrl { get; set; }
    public int   PriceGold    { get; set; }
    public int   PriceDiamond { get; set; }
    public int   SalesCount   { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "draft"; // draft|pending_review|approved|rejected
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt  { get; set; }
    public string? ReviewerNote  { get; set; }

    public Character Designer { get; set; } = null!;
    public ICollection<FashionDesignPurchase> Purchases { get; set; } = [];
}

[Table("fashion_design_purchases")]
public class FashionDesignPurchase
{
    [Key] public long Id { get; set; }
    public long  DesignId  { get; set; }
    public long  BuyerId   { get; set; }
    public int   PricePaid { get; set; }
    public DateTime BoughtAt { get; set; } = DateTime.UtcNow;

    public FashionDesign Design  { get; set; } = null!;
    public Character     Buyer   { get; set; } = null!;
}

// ─── Performance ─────────────────────────────────────────────

[Table("performance_stages")]
public class PerformanceStage
{
    [Key] public int Id { get; set; }
    public int   MapId    { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(16)] public string StageType { get; set; } = "square"; // square|theater|tavern|street
    public int   Capacity { get; set; } = 50;
    public bool  IsActive { get; set; } = true;

    public ICollection<Performance> Performances { get; set; } = [];
}

[Table("performances")]
public class Performance
{
    [Key] public long Id { get; set; }
    public long  PerformerId    { get; set; }
    public int   StageId        { get; set; }
    [MaxLength(32)] public string PerfType { get; set; } = "music"; // music|singing|dance|group|magic_show
    public long? WorkId         { get; set; }
    [MaxLength(128)] public string? Title { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "upcoming"; // upcoming|live|ended
    public DateTime? StartAt    { get; set; }
    public DateTime? EndAt      { get; set; }
    public int  AudienceCount   { get; set; }
    public long TotalTips       { get; set; }

    public Character         Performer { get; set; } = null!;
    public PerformanceStage  Stage     { get; set; } = null!;
    public ICollection<PerformanceTip>         Tips    { get; set; } = [];
    public ICollection<PerformanceGroupMember> Members { get; set; } = [];
}

[Table("performance_tips")]
public class PerformanceTip
{
    [Key] public long Id { get; set; }
    public long  PerformanceId { get; set; }
    public long  TipperId      { get; set; }
    [MaxLength(16)] public string TipType { get; set; } = "gold"; // gold|flower|gift_item|diamond
    public int   Amount        { get; set; }
    public int?  ItemId        { get; set; }
    [MaxLength(255)] public string? Message { get; set; }
    public DateTime TippedAt   { get; set; } = DateTime.UtcNow;

    public Performance Performance { get; set; } = null!;
    public Character   Tipper      { get; set; } = null!;
}

[Table("performance_group_members")]
public class PerformanceGroupMember
{
    [Key] public long Id { get; set; }
    public long  PerformanceId { get; set; }
    public long  CharacterId   { get; set; }
    [MaxLength(32)] public string? Role { get; set; }

    public Performance Performance { get; set; } = null!;
    public Character   Character   { get; set; } = null!;
}

// ─── Player Works (music, painting, novel...) ────────────────

[Table("player_works")]
public class PlayerWork
{
    [Key] public long Id { get; set; }
    public long  CreatorId    { get; set; }
    public int   ProfessionId { get; set; }
    [MaxLength(128)] public string Title { get; set; } = "";
    public string? Content    { get; set; }
    [MaxLength(32)] public string WorkType { get; set; } = "music";
    // music|painting|novel|fashion_design|dance_routine|recipe
    public int   Price        { get; set; }
    public int   Likes        { get; set; }
    public int   Views        { get; set; }
    public bool  IsPublished  { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Character Creator { get; set; } = null!;
}

// ─── Photo System ────────────────────────────────────────────

[Table("photo_frames")]
public class PhotoFrame
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(16)] public string Source { get; set; } = "default";
    public string? PreviewUrl { get; set; }
    public int PriceGold      { get; set; }
}

[Table("photos")]
public class Photo
{
    [Key] public long Id { get; set; }
    public long  TakerId    { get; set; }
    [MaxLength(255)] public string PhotoUrl { get; set; } = "";
    [MaxLength(16)] public string PhotoType { get; set; } = "selfie"; // selfie|group|scenery|event
    public int?  FrameId    { get; set; }
    public int?  MapId      { get; set; }
    public float PosX       { get; set; }
    public float PosY       { get; set; }
    public string? StickersJson  { get; set; }
    public string? TaggedChars   { get; set; }
    [MaxLength(255)] public string? Caption { get; set; }
    public int   Likes      { get; set; }
    public bool  IsPublic   { get; set; } = true;
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;

    public Character  Taker { get; set; } = null!;
    public PhotoFrame? Frame { get; set; }
}

[Table("photo_albums")]
public class PhotoAlbum
{
    [Key] public long Id { get; set; }
    public long  OwnerId       { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public long? CoverPhotoId  { get; set; }
    public bool  IsPublic      { get; set; } = true;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public Character Owner { get; set; } = null!;
    public ICollection<AlbumPhoto> Photos { get; set; } = [];
}

[Table("album_photos")]
public class AlbumPhoto
{
    public long AlbumId   { get; set; }
    public long PhotoId   { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public PhotoAlbum Album { get; set; } = null!;
    public Photo      Photo { get; set; } = null!;
}
