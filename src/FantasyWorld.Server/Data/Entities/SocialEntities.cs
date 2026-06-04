// FantasyWorld.Server/Data/Entities/SocialEntities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Data.Entities;

// ─── Friend Request ──────────────────────────────────────────

[Table("friend_requests")]
public class FriendRequest
{
    [Key] public long Id { get; set; }
    public long FromId   { get; set; }
    public long ToId     { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "pending"; // pending|accepted|rejected
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Character From { get; set; } = null!;
    public Character To   { get; set; } = null!;
}

// ─── Relationship ────────────────────────────────────────────

[Table("relationships")]
public class Relationship
{
    [Key] public long Id { get; set; }
    public long CharacterA   { get; set; }
    public long CharacterB   { get; set; }
    public RelType RelType   { get; set; }
    public long? InitiatedBy { get; set; }
    public int  Intimacy     { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public Character A { get; set; } = null!;
    public Character B { get; set; } = null!;
}

// ─── Party ───────────────────────────────────────────────────

[Table("parties")]
public class Party
{
    [Key] public long Id { get; set; }
    public long  LeaderId  { get; set; }
    public int?  DungeonId { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "forming"; // forming|in_dungeon|completed|disbanded
    public int   MaxMembers { get; set; } = 4;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Character Leader { get; set; } = null!;
    public ICollection<PartyMember> Members { get; set; } = [];
}

[Table("party_members")]
public class PartyMember
{
    [Key] public long Id { get; set; }
    public long PartyId     { get; set; }
    public long CharacterId { get; set; }
    [MaxLength(16)] public string Role { get; set; } = "member"; // leader|member
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Party     Party     { get; set; } = null!;
    public Character Character { get; set; } = null!;
}

// ─── Clan Quest ──────────────────────────────────────────────

[Table("clan_quests")]
public class ClanQuest
{
    [Key] public int Id { get; set; }
    public int    ClanId      { get; set; }
    public string Name        { get; set; } = "";
    public string? Description { get; set; }
    public string? ObjectiveJson { get; set; }
    public string? RewardJson    { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "active";
    public DateTime? ExpiresAt { get; set; }

    public Clan Clan { get; set; } = null!;
}

// ─── Clan Boss ───────────────────────────────────────────────

[Table("clan_bosses")]
public class ClanBoss
{
    [Key] public int Id { get; set; }
    public int    ClanId      { get; set; }
    [MaxLength(64)] public string BossName { get; set; } = "";
    public long   Hp           { get; set; } = 1_000_000;
    public long   HpMax        { get; set; } = 1_000_000;
    public int    Level        { get; set; } = 50;
    public string? RewardJson  { get; set; }
    public DateTime? LastKilledAt  { get; set; }
    public int    RespawnHours { get; set; } = 24;

    public Clan Clan { get; set; } = null!;
}

// ─── Clan Storage ────────────────────────────────────────────

[Table("clan_storage")]
public class ClanStorage
{
    [Key] public long Id { get; set; }
    public int  ClanId      { get; set; }
    public int  ItemId      { get; set; }
    public int  Quantity    { get; set; } = 1;
    public long? DepositedBy { get; set; }
    public DateTime DepositedAt { get; set; } = DateTime.UtcNow;

    public Clan Clan { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

// ─── Block List ──────────────────────────────────────────────

[Table("character_blocks")]
public class CharacterBlock
{
    [Key] public long Id { get; set; }
    public long BlockerId  { get; set; }
    public long BlockedId  { get; set; }
    public DateTime BlockedAt { get; set; } = DateTime.UtcNow;

    public Character Blocker { get; set; } = null!;
    public Character Blocked { get; set; } = null!;
}

// ─── Party Invite ────────────────────────────────────────────

[Table("party_invites")]
public class PartyInvite
{
    [Key] public long Id { get; set; }
    public long PartyId     { get; set; }
    public long InviterId   { get; set; }
    public long InviteeId   { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "pending";
    public DateTime SentAt    { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);

    public Party     Party   { get; set; } = null!;
    public Character Inviter { get; set; } = null!;
    public Character Invitee { get; set; } = null!;
}
