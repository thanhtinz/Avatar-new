// FantasyWorld.Server/Data/Entities/WorldEntities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Data.Entities;

// ─── Pet Skill ───────────────────────────────────────────────

[Table("pet_skills")]
public class PetSkill
{
    [Key] public int Id { get; set; }
    public int  SpeciesId   { get; set; }
    [MaxLength(64)] public string SkillName { get; set; } = "";
    public int  LearnLevel  { get; set; } = 1;
    public int  MpCost      { get; set; } = 10;
    public int  Power       { get; set; } = 50;
    public string? EffectJson { get; set; }
    public Element Element  { get; set; } = Element.None;

    public PetSpecies Species { get; set; } = null!;
}

// ─── Pet Catch Attempt ───────────────────────────────────────

[Table("pet_catch_logs")]
public class PetCatchLog
{
    [Key] public long Id { get; set; }
    public long  CharacterId { get; set; }
    public int   SpeciesId   { get; set; }
    public bool  Success     { get; set; }
    public int   MapId       { get; set; }
    public DateTime CaughtAt { get; set; } = DateTime.UtcNow;

    public Character  Character { get; set; } = null!;
    public PetSpecies Species   { get; set; } = null!;
}

// ─── Wild Pet Spawn ──────────────────────────────────────────

[Table("wild_pet_spawns")]
public class WildPetSpawn
{
    [Key] public long Id { get; set; }
    public int   MapId     { get; set; }
    public int   SpeciesId { get; set; }
    public float PosX      { get; set; }
    public float PosY      { get; set; }
    public bool  IsAlive   { get; set; } = true;
    public DateTime SpawnedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? CaughtAt  { get; set; }
    public long?  CaughtBy    { get; set; }

    public PetSpecies Species { get; set; } = null!;
}

// ─── Fishing ─────────────────────────────────────────────────

[Table("fish_species")]
public class FishSpecies
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public ItemRarity Rarity    { get; set; } = ItemRarity.Common;
    public float MinWeight      { get; set; } = 0.1f;
    public float MaxWeight      { get; set; } = 5.0f;
    [MaxLength(32)] public string Habitat { get; set; } = "lake"; // river|lake|ocean|underground|magic_pond
    public string? SeasonJson   { get; set; } // ["spring","summer"]
    public string? TimeJson     { get; set; } // ["day","night","full_moon"]
    public string? MapIdsJson   { get; set; }
    public int  SellPrice       { get; set; } = 10;
    public int? MuseumExhibitId { get; set; }
}

[Table("fishing_logs")]
public class FishingLog
{
    [Key] public long Id { get; set; }
    public long  CharacterId { get; set; }
    public int?  FishId      { get; set; }
    public float Weight      { get; set; }
    public int   MapId       { get; set; }
    [MaxLength(16)] public string Result { get; set; } = "nothing"; // caught|escaped|nothing
    public int  GoldEarned   { get; set; }
    public int  ExpEarned    { get; set; }
    public DateTime FishedAt { get; set; } = DateTime.UtcNow;

    public Character   Character { get; set; } = null!;
    public FishSpecies? Fish     { get; set; }
}

// ─── Farm ────────────────────────────────────────────────────

[Table("crops")]
public class Crop
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int    GrowMinutes    { get; set; } = 60;
    public string? SeasonsJson   { get; set; } // ["spring","summer"]
    public int    HarvestItemId  { get; set; }
    public int    HarvestQtyMin  { get; set; } = 1;
    public int    HarvestQtyMax  { get; set; } = 3;
    public int    SeedItemId     { get; set; }
    public int    ExpReward      { get; set; } = 10;
    public float  WaterBonus     { get; set; } = 1.5f;  // tưới nước → +50% tốc độ
    public float  FertBonus      { get; set; } = 2.0f;  // bón phân → +100% tốc độ

    public Item HarvestItem { get; set; } = null!;
    public Item SeedItem    { get; set; } = null!;
}

[Table("farm_plots")]
public class FarmPlot
{
    [Key] public long Id { get; set; }
    public long  OwnerId   { get; set; }
    public int   MapId     { get; set; }
    public float PosX      { get; set; }
    public float PosY      { get; set; }
    [MaxLength(16)] public string PlotType { get; set; } = "crop"; // crop|tree|flower|mushroom

    public Character Owner { get; set; } = null!;
    public PlotCrop? CurrentCrop { get; set; }
}

[Table("plot_crops")]
public class PlotCrop
{
    [Key] public long PlotId    { get; set; }
    public int  CropId          { get; set; }
    public DateTime PlantedAt   { get; set; } = DateTime.UtcNow;
    public DateTime ReadyAt     { get; set; }
    public byte GrowthStage     { get; set; }  // 0=seed,1=sprout,2=grown,3=ready
    public bool IsWatered       { get; set; }
    public bool IsFertilized    { get; set; }
    public bool IsHarvested     { get; set; }

    public FarmPlot Plot  { get; set; } = null!;
    public Crop     Crop  { get; set; } = null!;
}

// ─── Pet Ranch ───────────────────────────────────────────────

[Table("pet_ranches")]
public class PetRanch
{
    [Key] public long Id { get; set; }
    public long   OwnerId  { get; set; }
    public int    MapId    { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int    Level    { get; set; } = 1;
    public int    MaxCap   { get; set; } = 10;

    public Character Owner { get; set; } = null!;
    public ICollection<RanchPet> Pets { get; set; } = [];
}

[Table("ranch_pets")]
public class RanchPet
{
    [Key] public long Id { get; set; }
    public long RanchId  { get; set; }
    public long PetId    { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;

    public PetRanch Ranch { get; set; } = null!;
    public Pet      Pet   { get; set; } = null!;
}

// ─── Dungeon ─────────────────────────────────────────────────

[Table("dungeons")]
public class Dungeon
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string DungeonType { get; set; } = "dungeon"; // dungeon|pyramid|ancient_city|cave
    public int  MapId        { get; set; }
    public int  MinPlayers   { get; set; } = 1;
    public int  MaxPlayers   { get; set; } = 4;
    public int  MinLevel     { get; set; } = 1;
    public int  Floors       { get; set; } = 1;
    public bool HasTraps     { get; set; } = true;
    public bool HasPuzzles   { get; set; }
    public int? BossId       { get; set; }
    public string? RewardJson { get; set; }
    public int  CooldownHours { get; set; } = 24;

    public ICollection<DungeonRun> Runs { get; set; } = [];
}

[Table("dungeon_runs")]
public class DungeonRun
{
    [Key] public long Id { get; set; }
    public int    DungeonId    { get; set; }
    public long   PartyId      { get; set; }
    public int    Floor        { get; set; } = 1;
    [MaxLength(16)] public string Status { get; set; } = "in_progress"; // in_progress|completed|failed
    public int    TotalExp     { get; set; }
    public int    TotalGold    { get; set; }
    public string? LootJson    { get; set; }
    public DateTime StartedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public Dungeon Dungeon { get; set; } = null!;
    public ICollection<DungeonRunMember> Members { get; set; } = [];
}

[Table("dungeon_run_members")]
public class DungeonRunMember
{
    [Key] public long Id { get; set; }
    public long RunId       { get; set; }
    public long CharacterId { get; set; }
    public int  DamageDealt { get; set; }
    public int  HealingDone { get; set; }
    public bool IsAlive     { get; set; } = true;

    public DungeonRun Run       { get; set; } = null!;
    public Character  Character { get; set; } = null!;
}

// ─── World Event ─────────────────────────────────────────────

[Table("world_event_participants")]
public class WorldEventParticipant
{
    [Key] public long Id { get; set; }
    public long InstanceId   { get; set; }
    public long CharacterId  { get; set; }
    public int  Contribution { get; set; }
    public bool RewardClaimed { get; set; }
    public string? RewardJson { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public WorldEventInstance Instance  { get; set; } = null!;
    public Character          Character { get; set; } = null!;
}

// ─── NPC AI ──────────────────────────────────────────────────

[Table("npc_ai_profiles")]
public class NpcAiProfile
{
    [Key] public int NpcId { get; set; }
    [MaxLength(16)] public string Personality { get; set; } = "friendly"; // friendly|grumpy|shy|energetic|wise|mysterious
    public int  HomeMapId  { get; set; }
    public float HomePosX  { get; set; }
    public float HomePosY  { get; set; }
    public int? WorkFacilityId { get; set; }
    public string? MemoryJson  { get; set; } // JSON of player interactions remembered

    public Npc Npc { get; set; } = null!;
}

[Table("npc_schedules")]
public class NpcSchedule
{
    [Key] public int Id { get; set; }
    public int  NpcId          { get; set; }
    public byte GameHourStart  { get; set; }
    public byte GameHourEnd    { get; set; }
    [MaxLength(16)] public string Activity { get; set; } = "work";  // sleep|work|shop|eat|wander|socialize|fish|pray
    [MaxLength(16)] public string LocationType { get; set; } = "work";
    public int?  TargetMapId   { get; set; }
    public float TargetPosX    { get; set; }
    public float TargetPosY    { get; set; }
    public string? DialogJson  { get; set; }

    public Npc Npc { get; set; } = null!;
}

[Table("npc_character_relations")]
public class NpcCharacterRelation
{
    [Key] public long Id { get; set; }
    public int  NpcId        { get; set; }
    public long CharacterId  { get; set; }
    public int  Affection    { get; set; }  // -100 to 100
    public int  Interactions { get; set; }
    public DateTime? LastMetAt { get; set; }
    public string? NotesJson   { get; set; } // NPC remembers player actions

    public Npc       Npc       { get; set; } = null!;
    public Character Character { get; set; } = null!;
}

// ─── NPC (base) ──────────────────────────────────────────────

[Table("npcs")]
public class Npc
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(32)] public string NpcType { get; set; } = "shop"; // shop|quest|wander|guard|boss|event
    public int?  MapId         { get; set; }
    public float PosX          { get; set; }
    public float PosY          { get; set; }
    public float WanderRadius  { get; set; }
    [MaxLength(32)] public string? ActiveHours { get; set; } // "08:00-22:00"
    public string? DialogJson  { get; set; }
    public int   SpriteId      { get; set; }

    public NpcAiProfile? AiProfile { get; set; }
    public ICollection<NpcSchedule> Schedules { get; set; } = [];
    public ICollection<NpcCharacterRelation> Relations { get; set; } = [];
}

// ─── Monster ─────────────────────────────────────────────────

[Table("monsters")]
public class Monster
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] public string Name { get; set; } = "";
    public int    Level       { get; set; } = 1;
    public int    HpMax       { get; set; } = 100;
    public int    Atk         { get; set; } = 10;
    public int    Def         { get; set; } = 5;
    public int    Spd         { get; set; } = 10;
    public int    ExpReward   { get; set; } = 10;
    public int    GoldReward  { get; set; } = 5;
    public string? MapIdsJson { get; set; }
    public string? LootJson   { get; set; } // [{"item_id":1,"rate":0.1,"qty":1}]
    public int    SpriteId    { get; set; }
    public Element Element    { get; set; } = Element.None;
    public bool   IsBoss      { get; set; }
}

[Table("battle_logs")]
public class BattleLog
{
    [Key] public long Id { get; set; }
    public long   AttackerId   { get; set; }
    [MaxLength(16)] public string DefenderType { get; set; } = "monster";
    public long?  DefenderId   { get; set; }
    public int    MapId        { get; set; }
    [MaxLength(16)] public string Outcome { get; set; } = "win";
    public int    DamageDealt  { get; set; }
    public int    DamageTaken  { get; set; }
    public int    ExpGained    { get; set; }
    public int    GoldGained   { get; set; }
    public string? ItemsDropped { get; set; }
    public int    DurationSec  { get; set; }
    public DateTime FoughtAt   { get; set; } = DateTime.UtcNow;

    public Character Attacker { get; set; } = null!;
}
