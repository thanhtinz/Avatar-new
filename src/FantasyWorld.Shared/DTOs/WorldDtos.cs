// FantasyWorld.Shared/DTOs/WorldDtos.cs
using MessagePack;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Shared.DTOs;

// ─── Pet ─────────────────────────────────────────────────────

[MessagePackObject]
public record WildPetDto(
    [property: Key(0)] long   SpawnId,
    [property: Key(1)] int    SpeciesId,
    [property: Key(2)] string Name,
    [property: Key(3)] string Rarity,
    [property: Key(4)] Element Element,
    [property: Key(5)] float  PosX,
    [property: Key(6)] float  PosY,
    [property: Key(7)] int    CatchRate,
    [property: Key(8)] bool   IsShiny
);

[MessagePackObject]
public record PetDto(
    [property: Key(0)]  long   Id,
    [property: Key(1)]  string SpeciesName,
    [property: Key(2)]  string? Nickname,
    [property: Key(3)]  string Rarity,
    [property: Key(4)]  Element Element,
    [property: Key(5)]  string Personality,
    [property: Key(6)]  int    ColorVariant,
    [property: Key(7)]  bool   IsShiny,
    [property: Key(8)]  int    Level,
    [property: Key(9)]  int    Happiness,
    [property: Key(10)] int    Hunger,
    [property: Key(11)] int    Hp,
    [property: Key(12)] int    HpMax,
    [property: Key(13)] int    Atk,
    [property: Key(14)] int    Def,
    [property: Key(15)] int    Spd,
    [property: Key(16)] bool   IsActive
);

[MessagePackObject]
public record CatchPetRequest(
    [property: Key(0)] long SpawnId,
    [property: Key(1)] int? BaitItemId  // dùng mồi tăng tỷ lệ
);

[MessagePackObject]
public record CatchResultDto(
    [property: Key(0)] bool    Success,
    [property: Key(1)] string  Message,
    [property: Key(2)] PetDto? Pet,
    [property: Key(3)] int     CatchRate,
    [property: Key(4)] bool    IsShiny
);

[MessagePackObject]
public record PetNicknameRequest(
    [property: Key(0)] long   PetId,
    [property: Key(1)] string Nickname
);

// ─── Fishing ─────────────────────────────────────────────────

[MessagePackObject]
public record FishingResultDto(
    [property: Key(0)] string Result,     // caught|escaped|nothing
    [property: Key(1)] string? FishName,
    [property: Key(2)] string? Rarity,
    [property: Key(3)] float   Weight,
    [property: Key(4)] int     GoldEarned,
    [property: Key(5)] int     ExpEarned,
    [property: Key(6)] bool    IsRecord,
    [property: Key(7)] bool    IsMuseumNew
);

[MessagePackObject]
public record FishingRecordDto(
    [property: Key(0)] int   TotalCaught,
    [property: Key(1)] float TotalWeight,
    [property: Key(2)] string? BiggestFishName,
    [property: Key(3)] float  BiggestWeight,
    [property: Key(4)] int    SpeciesCount
);

// ─── Farm ────────────────────────────────────────────────────

[MessagePackObject]
public record FarmPlotDto(
    [property: Key(0)] long    PlotId,
    [property: Key(1)] string  PlotType,
    [property: Key(2)] float   PosX,
    [property: Key(3)] float   PosY,
    [property: Key(4)] string? CropName,
    [property: Key(5)] byte    GrowthStage,
    [property: Key(6)] bool    IsWatered,
    [property: Key(7)] bool    IsFertilized,
    [property: Key(8)] bool    IsReady,
    [property: Key(9)] long?   ReadyAtMs
);

[MessagePackObject]
public record PlantRequest(
    [property: Key(0)] long PlotId,
    [property: Key(1)] int  SeedItemId
);

[MessagePackObject]
public record HarvestResultDto(
    [property: Key(0)] string CropName,
    [property: Key(1)] int    Quantity,
    [property: Key(2)] int    ExpEarned
);

// ─── Dungeon ─────────────────────────────────────────────────

[MessagePackObject]
public record DungeonDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string DungeonType,
    [property: Key(3)] int    MinPlayers,
    [property: Key(4)] int    MaxPlayers,
    [property: Key(5)] int    MinLevel,
    [property: Key(6)] int    Floors,
    [property: Key(7)] bool   HasTraps,
    [property: Key(8)] bool   HasPuzzles,
    [property: Key(9)] bool   OnCooldown
);

[MessagePackObject]
public record DungeonRunDto(
    [property: Key(0)] long   RunId,
    [property: Key(1)] int    DungeonId,
    [property: Key(2)] string DungeonName,
    [property: Key(3)] int    CurrentFloor,
    [property: Key(4)] int    TotalFloors,
    [property: Key(5)] string Status,
    [property: Key(6)] int    TotalExp,
    [property: Key(7)] int    TotalGold
);

// ─── World Event ─────────────────────────────────────────────

[MessagePackObject]
public record WorldEventDto(
    [property: Key(0)] long   InstanceId,
    [property: Key(1)] string Name,
    [property: Key(2)] string EventType,
    [property: Key(3)] string DescVi,
    [property: Key(4)] string DescEn,
    [property: Key(5)] long   EndsAtMs,
    [property: Key(6)] int    Participants,
    [property: Key(7)] string Status
);

[MessagePackObject]
public record ContributeEventRequest(
    [property: Key(0)] long InstanceId,
    [property: Key(1)] int  Amount
);

// ─── NPC ─────────────────────────────────────────────────────

[MessagePackObject]
public record NpcStateDto(
    [property: Key(0)] int    NpcId,
    [property: Key(1)] string Name,
    [property: Key(2)] string NpcType,
    [property: Key(3)] float  PosX,
    [property: Key(4)] float  PosY,
    [property: Key(5)] string CurrentActivity,
    [property: Key(6)] int    Affection    // quan hệ với nhân vật hiện tại
);

[MessagePackObject]
public record NpcInteractRequest(
    [property: Key(0)] int    NpcId,
    [property: Key(1)] string ActionType   // "talk"|"trade"|"quest"|"gift"
);

// ─── Battle ──────────────────────────────────────────────────

[MessagePackObject]
public record BattleStartDto(
    [property: Key(0)] long   BattleId,
    [property: Key(1)] string DefenderType,
    [property: Key(2)] long   DefenderId,
    [property: Key(3)] string DefenderName,
    [property: Key(4)] int    DefenderHp,
    [property: Key(5)] int    DefenderLevel
);

[MessagePackObject]
public record BattleActionRequest(
    [property: Key(0)] long   BattleId,
    [property: Key(1)] string ActionType,  // "attack"|"skill"|"item"|"flee"
    [property: Key(2)] int?   SkillId,
    [property: Key(3)] int?   ItemId
);

[MessagePackObject]
public record BattleResultDto(
    [property: Key(0)] string Outcome,     // win|lose|flee
    [property: Key(1)] int    ExpGained,
    [property: Key(2)] int    GoldGained,
    [property: Key(3)] List<string> ItemsDropped,
    [property: Key(4)] bool   LeveledUp,
    [property: Key(5)] int    NewLevel
);
