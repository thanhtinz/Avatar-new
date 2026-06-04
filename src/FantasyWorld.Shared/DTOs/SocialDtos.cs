// FantasyWorld.Shared/DTOs/SocialDtos.cs
using MessagePack;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Shared.DTOs;

// ─── Friend ──────────────────────────────────────────────────

[MessagePackObject]
public record FriendDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Level,
    [property: Key(3)] bool   IsOnline,
    [property: Key(4)] int    MapId,
    [property: Key(5)] string? FactionName
);

[MessagePackObject]
public record FriendRequestDto(
    [property: Key(0)] long   Id,
    [property: Key(1)] long   FromCharId,
    [property: Key(2)] string FromName,
    [property: Key(3)] int    FromLevel,
    [property: Key(4)] string Status,
    [property: Key(5)] long   SentAtMs
);

// ─── Relationship ────────────────────────────────────────────

[MessagePackObject]
public record RelationshipDto(
    [property: Key(0)] long    Id,
    [property: Key(1)] long    PartnerCharId,
    [property: Key(2)] string  PartnerName,
    [property: Key(3)] RelType RelType,
    [property: Key(4)] int     Intimacy,
    [property: Key(5)] bool    IsOnline
);

// ─── Party ───────────────────────────────────────────────────

[MessagePackObject]
public record PartyDto(
    [property: Key(0)] long             Id,
    [property: Key(1)] long             LeaderId,
    [property: Key(2)] string           LeaderName,
    [property: Key(3)] string           Status,
    [property: Key(4)] int              MaxMembers,
    [property: Key(5)] List<PartyMemberDto> Members
);

[MessagePackObject]
public record PartyMemberDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Level,
    [property: Key(3)] int    Hp,
    [property: Key(4)] int    HpMax,
    [property: Key(5)] int    Mp,
    [property: Key(6)] int    MpMax,
    [property: Key(7)] bool   IsOnline,
    [property: Key(8)] string Role
);

[MessagePackObject]
public record PartyInviteDto(
    [property: Key(0)] long   PartyId,
    [property: Key(1)] long   InviterCharId,
    [property: Key(2)] string InviterName,
    [property: Key(3)] int    CurrentMembers,
    [property: Key(4)] int    MaxMembers
);

// ─── Clan ────────────────────────────────────────────────────

[MessagePackObject]
public record ClanDto(
    [property: Key(0)]  int    Id,
    [property: Key(1)]  string Name,
    [property: Key(2)]  string? Emblem,
    [property: Key(3)]  string? Description,
    [property: Key(4)]  int    Level,
    [property: Key(5)]  long   Gold,
    [property: Key(6)]  int    MemberCount,
    [property: Key(7)]  int    MaxMembers,
    [property: Key(8)]  long   LeaderId,
    [property: Key(9)]  string LeaderName,
    [property: Key(10)] int    OnlineCount
);

[MessagePackObject]
public record ClanMemberDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string Name,
    [property: Key(2)] int    Level,
    [property: Key(3)] string Role,
    [property: Key(4)] int    Contribution,
    [property: Key(5)] bool   IsOnline,
    [property: Key(6)] long   JoinedAtMs
);

[MessagePackObject]
public record CreateClanRequest(
    [property: Key(0)] string Name,
    [property: Key(1)] string? Description = null,
    [property: Key(2)] string? Emblem      = null
);

// ─── Online status ───────────────────────────────────────────

[MessagePackObject]
public record OnlineStatusDto(
    [property: Key(0)] long   CharId,
    [property: Key(1)] string Name,
    [property: Key(2)] bool   IsOnline,
    [property: Key(3)] int?   MapId
);
