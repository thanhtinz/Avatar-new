// FantasyWorld.Shared/DTOs/EntertainmentDtos.cs
using MessagePack;

namespace FantasyWorld.Shared.DTOs;

// ─── Casino ──────────────────────────────────────────────────

[MessagePackObject]
public record CasinoGameDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string GameType,
    [property: Key(3)] int    MinBet,
    [property: Key(4)] int    MaxBet
);

[MessagePackObject]
public record CasinoBetRequest(
    [property: Key(0)] int GameId,
    [property: Key(1)] int Amount,
    [property: Key(2)] string? ExtraJson   // roulette: number, card: choice
);

[MessagePackObject]
public record CasinoResultDto(
    [property: Key(0)] bool   Won,
    [property: Key(1)] int    Payout,
    [property: Key(2)] int    NetChange,
    [property: Key(3)] string ResultDetail,
    [property: Key(4)] long   NewGold
);

// ─── Mini Game ───────────────────────────────────────────────

[MessagePackObject]
public record MiniGameDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string GameType,
    [property: Key(3)] string Description,
    [property: Key(4)] int    EntryFee,
    [property: Key(5)] int    BestScore,
    [property: Key(6)] bool   OnCooldown,
    [property: Key(7)] long?  CooldownUntilMs
);

[MessagePackObject]
public record SubmitScoreRequest(
    [property: Key(0)] int MiniGameId,
    [property: Key(1)] int Score
);

[MessagePackObject]
public record MiniGameResultDto(
    [property: Key(0)] int    Score,
    [property: Key(1)] bool   IsNewRecord,
    [property: Key(2)] int    GoldEarned,
    [property: Key(3)] int    ExpEarned,
    [property: Key(4)] int    Rank        // rank trên leaderboard
);

[MessagePackObject]
public record MiniGameLeaderboardDto(
    [property: Key(0)] int    Rank,
    [property: Key(1)] long   CharId,
    [property: Key(2)] string CharName,
    [property: Key(3)] int    BestScore
);

// ─── Mount Race ──────────────────────────────────────────────

[MessagePackObject]
public record MountRaceDto(
    [property: Key(0)] long   RaceId,
    [property: Key(1)] string TrackName,
    [property: Key(2)] string Status,
    [property: Key(3)] int    EntryFee,
    [property: Key(4)] int    CurrentRacers,
    [property: Key(5)] int    MaxRacers,
    [property: Key(6)] long?  StartsAtMs
);

[MessagePackObject]
public record JoinRaceRequest(
    [property: Key(0)] long RaceId,
    [property: Key(1)] long? MountPetId
);

[MessagePackObject]
public record RaceUpdateDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string CharName,
    [property: Key(2)] float  Progress,  // 0.0 - 1.0
    [property: Key(3)] int    Position
);

[MessagePackObject]
public record RaceFinishDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string CharName,
    [property: Key(2)] int    Position,
    [property: Key(3)] int    TimeMs,
    [property: Key(4)] int    GoldEarned
);

// ─── Fashion ─────────────────────────────────────────────────

[MessagePackObject]
public record FashionItemDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Slot,
    [property: Key(3)] string Rarity,
    [property: Key(4)] string Source,
    [property: Key(5)] int    PriceGold,
    [property: Key(6)] int    PriceDiamond,
    [property: Key(7)] bool   Dyeable,
    [property: Key(8)] bool   HasEffect,
    [property: Key(9)] bool   Owned
);

[MessagePackObject]
public record OutfitDto(
    [property: Key(0)] int?   TopId,
    [property: Key(1)] int?   BottomId,
    [property: Key(2)] int?   ShoesId,
    [property: Key(3)] int?   HatId,
    [property: Key(4)] int?   GlassesId,
    [property: Key(5)] int?   CapeId,
    [property: Key(6)] int?   WingsId,
    [property: Key(7)] int?   AuraId,
    [property: Key(8)] string? DyeJson,
    [property: Key(9)] string? EffectJson
);

[MessagePackObject]
public record WearRequest(
    [property: Key(0)] string Slot,
    [property: Key(1)] int?   FashionItemId,
    [property: Key(2)] string? DyeColorHex,
    [property: Key(3)] int?   EffectId
);

[MessagePackObject]
public record SavePresetRequest(
    [property: Key(0)] int    SlotNumber,
    [property: Key(1)] string PresetName
);

[MessagePackObject]
public record SubmitDesignRequest(
    [property: Key(0)] string Name,
    [property: Key(1)] string Slot,
    [property: Key(2)] string DesignData,
    [property: Key(3)] int    PriceGold,
    [property: Key(4)] int    PriceDiamond
);

[MessagePackObject]
public record FashionDesignDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Slot,
    [property: Key(3)] string DesignerName,
    [property: Key(4)] int    PriceGold,
    [property: Key(5)] int    PriceDiamond,
    [property: Key(6)] int    SalesCount,
    [property: Key(7)] string Status,
    [property: Key(8)] string? ThumbnailUrl
);

// ─── Performance ─────────────────────────────────────────────

[MessagePackObject]
public record PerformanceDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string PerformerName,
    [property: Key(2)] string StageName,
    [property: Key(3)] string PerfType,
    [property: Key(4)] string? Title,
    [property: Key(5)] string Status,
    [property: Key(6)] int    AudienceCount,
    [property: Key(7)] long   TotalTips,
    [property: Key(8)] long?  StartAtMs
);

[MessagePackObject]
public record StartPerformanceRequest(
    [property: Key(0)] int    StageId,
    [property: Key(1)] string PerfType,
    [property: Key(2)] string? Title,
    [property: Key(3)] long?   WorkId
);

[MessagePackObject]
public record SendTipRequest(
    [property: Key(0)] long   PerformanceId,
    [property: Key(1)] string TipType,
    [property: Key(2)] int    Amount,
    [property: Key(3)] int?   ItemId,
    [property: Key(4)] string? Message
);

[MessagePackObject]
public record TipReceivedDto(
    [property: Key(0)] long   PerformanceId,
    [property: Key(1)] string TipperName,
    [property: Key(2)] string TipType,
    [property: Key(3)] int    Amount,
    [property: Key(4)] string? Message
);

// ─── Photo ───────────────────────────────────────────────────

[MessagePackObject]
public record TakePhotoRequest(
    [property: Key(0)] string PhotoType,
    [property: Key(1)] int    MapId,
    [property: Key(2)] float  PosX,
    [property: Key(3)] float  PosY,
    [property: Key(4)] int?   FrameId,
    [property: Key(5)] string? StickersJson,
    [property: Key(6)] List<long> TaggedCharIds,
    [property: Key(7)] string? Caption,
    [property: Key(8)] string  PhotoUrl
);

[MessagePackObject]
public record PhotoDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string TakerName,
    [property: Key(2)] string PhotoUrl,
    [property: Key(3)] string PhotoType,
    [property: Key(4)] string? Caption,
    [property: Key(5)] int    Likes,
    [property: Key(6)] long   TakenAtMs
);
