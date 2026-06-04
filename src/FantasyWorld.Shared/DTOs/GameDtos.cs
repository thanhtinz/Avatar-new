// FantasyWorld.Shared/DTOs/GameDtos.cs
using MessagePack;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Shared.DTOs;

// ─── Auth ────────────────────────────────────────────────────

[MessagePackObject]
public record LoginRequestDto(
    [property: Key(0)] string Username,
    [property: Key(1)] string Password,
    [property: Key(2)] string Lang = "vi"
);

[MessagePackObject]
public record LoginResponseDto(
    [property: Key(0)] bool   Success,
    [property: Key(1)] string Message,
    [property: Key(2)] string? AccessToken,
    [property: Key(3)] string? RefreshToken,
    [property: Key(4)] AccountInfoDto? Account,
    [property: Key(5)] List<CharacterSummaryDto>? Characters
);

[MessagePackObject]
public record AccountInfoDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Username,
    [property: Key(2)] string Role
);

// ─── Character ───────────────────────────────────────────────

[MessagePackObject]
public record CharacterSummaryDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Level,
    [property: Key(3)] Gender Gender,
    [property: Key(4)] int    MapId,
    [property: Key(5)] float  PosX,
    [property: Key(6)] float  PosY
);

[MessagePackObject]
public record CharacterFullDto(
    [property: Key(0)]  long   Id,
    [property: Key(1)]  string Name,
    [property: Key(2)]  int    Level,
    [property: Key(3)]  long   Exp,
    [property: Key(4)]  Gender Gender,
    [property: Key(5)]  int    HairStyle,
    [property: Key(6)]  int    HairColor,
    [property: Key(7)]  int    FaceStyle,
    [property: Key(8)]  int    BodyStyle,
    [property: Key(9)]  int    SkinColor,
    [property: Key(10)] int    MapId,
    [property: Key(11)] float  PosX,
    [property: Key(12)] float  PosY,
    [property: Key(13)] int    Hp,
    [property: Key(14)] int    HpMax,
    [property: Key(15)] int    Mp,
    [property: Key(16)] int    MpMax,
    [property: Key(17)] int    Atk,
    [property: Key(18)] int    Def,
    [property: Key(19)] int    Spd,
    [property: Key(20)] long   Gold,
    [property: Key(21)] int    Diamond,
    [property: Key(22)] byte   Hunger,
    [property: Key(23)] byte   Energy,
    [property: Key(24)] byte   Mood,
    [property: Key(25)] string? FactionName,
    [property: Key(26)] string? ClanName,
    [property: Key(27)] string? AcademyName
);

[MessagePackObject]
public record CreateCharacterRequestDto(
    [property: Key(0)] string Name,
    [property: Key(1)] Gender Gender      = Gender.Male,
    [property: Key(2)] int    HairStyle   = 0,
    [property: Key(3)] int    HairColor   = 0,
    [property: Key(4)] int    FaceStyle   = 0,
    [property: Key(5)] int    BodyStyle   = 0,
    [property: Key(6)] int    SkinColor   = 0
);

// ─── Movement ────────────────────────────────────────────────

[MessagePackObject]
public record MoveDto(
    [property: Key(0)] float   X,
    [property: Key(1)] float   Y,
    [property: Key(2)] int     MapId,
    [property: Key(3)] MoveDir Dir = MoveDir.Down
);

[MessagePackObject]
public record PlayerMoveDto(
    [property: Key(0)] long    CharId,
    [property: Key(1)] float   X,
    [property: Key(2)] float   Y,
    [property: Key(3)] MoveDir Dir
);

[MessagePackObject]
public record PlayerEnterDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Level,
    [property: Key(3)] float  X,
    [property: Key(4)] float  Y,
    [property: Key(5)] Gender Gender
);

[MessagePackObject]
public record MapChangedDto(
    [property: Key(0)] int   MapId,
    [property: Key(1)] string MapName,
    [property: Key(2)] float  X,
    [property: Key(3)] float  Y,
    [property: Key(4)] bool   IsPvp,
    [property: Key(5)] List<PlayerEnterDto> Players
);

// ─── Chat ────────────────────────────────────────────────────

[MessagePackObject]
public record ChatSendDto(
    [property: Key(0)] ChatChannel Channel,
    [property: Key(1)] string      Content,
    [property: Key(2)] long?       TargetCharId = null
);

[MessagePackObject]
public record ChatReceiveDto(
    [property: Key(0)] long        SenderId,
    [property: Key(1)] string      SenderName,
    [property: Key(2)] int         SenderLevel,
    [property: Key(3)] ChatChannel Channel,
    [property: Key(4)] string      Content,
    [property: Key(5)] long        Timestamp,
    [property: Key(6)] bool        IsPrivate    = false
);

// ─── World ───────────────────────────────────────────────────

[MessagePackObject]
public record WorldTimeDto(
    [property: Key(0)] int        GameHour,
    [property: Key(1)] int        GameMinute,
    [property: Key(2)] bool       IsDay,
    [property: Key(3)] SeasonCode Season,
    [property: Key(4)] int        MoonPhase,
    [property: Key(5)] long       ServerTimestamp
);

[MessagePackObject]
public record SeasonChangeDto(
    [property: Key(0)] SeasonCode Season,
    [property: Key(1)] string     NameVi,
    [property: Key(2)] string     NameEn
);

// ─── Game Ready ──────────────────────────────────────────────

[MessagePackObject]
public record GameReadyDto(
    [property: Key(0)] CharacterFullDto          Character,
    [property: Key(1)] List<PlayerEnterDto>      MapPlayers,
    [property: Key(2)] WorldTimeDto              WorldTime,
    [property: Key(3)] int                       OnlineCount
);

// ─── Error ───────────────────────────────────────────────────

[MessagePackObject]
public record ErrorDto(
    [property: Key(0)] string Code,
    [property: Key(1)] string MessageVi,
    [property: Key(2)] string MessageEn
);
