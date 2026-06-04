// FantasyWorld.Shared/DTOs/GameplayDtos.cs
using MessagePack;

namespace FantasyWorld.Shared.DTOs;

// ─── Faction ─────────────────────────────────────────────────
[MessagePackObject]
public record FactionDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Description,
    [property: Key(3)] string Color,
    [property: Key(4)] int    MemberCount,
    [property: Key(5)] bool   IsJoined,
    [property: Key(6)] int    MyRank,
    [property: Key(7)] int    MyContribution
);

[MessagePackObject]
public record FactionWarDto(
    [property: Key(0)] int    WarId,
    [property: Key(1)] string AttackerName,
    [property: Key(2)] string DefenderName,
    [property: Key(3)] int    AttackerScore,
    [property: Key(4)] int    DefenderScore,
    [property: Key(5)] long   EndsAtMs
);

// ─── Company ─────────────────────────────────────────────────
[MessagePackObject]
public record CompanyDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string TypeName,
    [property: Key(3)] string OwnerName,
    [property: Key(4)] long   Balance,
    [property: Key(5)] int    EmployeeCount,
    [property: Key(6)] bool   IsOpen
);

[MessagePackObject]
public record EmployeeDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Level,
    [property: Key(3)] string Role,
    [property: Key(4)] int    Salary,
    [property: Key(5)] bool   IsOnline
);

// ─── Real Estate ─────────────────────────────────────────────
[MessagePackObject]
public record LandPlotDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string ZoneName,
    [property: Key(2)] int    MapId,
    [property: Key(3)] float  PosX,
    [property: Key(4)] float  PosY,
    [property: Key(5)] int    BasePrice,
    [property: Key(6)] int?   ListingPrice,
    [property: Key(7)] bool   IsOwned,
    [property: Key(8)] bool   IsForSale,
    [property: Key(9)] string? OwnerName
);

[MessagePackObject]
public record HouseDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string OwnerName,
    [property: Key(3)] int    Level,
    [property: Key(4)] int    Score,
    [property: Key(5)] bool   IsPublic,
    [property: Key(6)] int    VisitCount,
    [property: Key(7)] string? LayoutJson
);

// ─── Museum ──────────────────────────────────────────────────
[MessagePackObject]
public record MuseumExhibitDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Category,
    [property: Key(3)] string? DonorName
);

// ─── Travel ──────────────────────────────────────────────────
[MessagePackObject]
public record LandmarkDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string RegionName,
    [property: Key(3)] int    MapId,
    [property: Key(4)] int    ExpReward,
    [property: Key(5)] int    GoldReward,
    [property: Key(6)] bool   Visited
);

[MessagePackObject]
public record PassportDto(
    [property: Key(0)] long  CharId,
    [property: Key(1)] int   StampCount,
    [property: Key(2)] List<string> VisitedRegions
);

// ─── Territory ───────────────────────────────────────────────
[MessagePackObject]
public record TerritoryDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string ClanName,
    [property: Key(2)] string TerritoryName,
    [property: Key(3)] int    Tier,
    [property: Key(4)] string TierName,
    [property: Key(5)] long   TaxPool,
    [property: Key(6)] List<string> Buildings
);

// ─── Profession ──────────────────────────────────────────────
[MessagePackObject]
public record RareProfessionDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] int    ServerSlotLimit,
    [property: Key(3)] int    CurrentSlotUsed,
    [property: Key(4)] int    RequiredLevel,
    [property: Key(5)] bool   IsUnlocked
);

[MessagePackObject]
public record CivilProfessionDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string WorkType,
    [property: Key(3)] int    UnlockCost,
    [property: Key(4)] int    Mastery,
    [property: Key(5)] bool   IsUnlocked
);

// ─── Deity ───────────────────────────────────────────────────
[MessagePackObject]
public record DeityDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string Description,
    [property: Key(3)] bool   IsChosen,
    [property: Key(4)] int    MyDevotion,
    [property: Key(5)] string MyTier
);

// ─── Leaderboard ─────────────────────────────────────────────
[MessagePackObject]
public record LeaderboardDto(
    [property: Key(0)] string Category,
    [property: Key(1)] List<LeaderboardRowDto> Rows
);

[MessagePackObject]
public record LeaderboardRowDto(
    [property: Key(0)] int    Rank,
    [property: Key(1)] long   CharId,
    [property: Key(2)] string Name,
    [property: Key(3)] int    Level,
    [property: Key(4)] long   Score
);

// ─── Festival ────────────────────────────────────────────────
[MessagePackObject]
public record FestivalDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string NameVi,
    [property: Key(2)] string NameEn,
    [property: Key(3)] bool   IsActive,
    [property: Key(4)] long   EndAtMs,
    [property: Key(5)] int    TotalTasks,
    [property: Key(6)] int    CompletedTasks,
    [property: Key(7)] long   BaseRewardGold
);

[MessagePackObject]
public record FestivalTaskDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Title,
    [property: Key(2)] bool   IsCompleted
);

// ─── UGC ─────────────────────────────────────────────────────
[MessagePackObject]
public record UgcContentDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string ContentType,
    [property: Key(2)] string Title,
    [property: Key(3)] string CreatorName,
    [property: Key(4)] float  Rating,
    [property: Key(5)] int    RatingCount,
    [property: Key(6)] int    VisitCount,
    [property: Key(7)] long   CreatedAtMs
);

// ─── Endgame ─────────────────────────────────────────────────
[MessagePackObject]
public record EndgameRealmDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Name,
    [property: Key(2)] string NameEn,
    [property: Key(3)] int    RequiredLevel,
    [property: Key(4)] bool   IsUnlocked
);

// ─── Treasure Hunt ───────────────────────────────────────────
[MessagePackObject]
public record TreasureHuntDto(
    [property: Key(0)] int    Id,
    [property: Key(1)] string Title,
    [property: Key(2)] string Clue1,
    [property: Key(3)] string Clue2,
    [property: Key(4)] string Clue3,
    [property: Key(5)] int    MapId,
    [property: Key(6)] int    GoldReward
);

[MessagePackObject]
public record SubmitLocationRequest(
    [property: Key(0)] int   HuntId,
    [property: Key(1)] float X,
    [property: Key(2)] float Y
);

// ─── Hotel ───────────────────────────────────────────────────
[MessagePackObject]
public record HotelRoomDto(
    [property: Key(0)] long   RoomId,
    [property: Key(1)] int    RoomNumber,
    [property: Key(2)] string RoomType,
    [property: Key(3)] int    PricePerDay,
    [property: Key(4)] bool   IsAvailable,
    [property: Key(5)] long?  OccupiedUntilMs
);

[MessagePackObject]
public record RentRoomRequest(
    [property: Key(0)] long RoomId,
    [property: Key(1)] int  Days
);
