// FantasyWorld.Server/Services/Social/FriendService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Social;

public interface IFriendService
{
    Task<(bool Ok, string Msg)> SendRequestAsync(long fromId, long toId, string lang);
    Task<(bool Ok, string Msg)> AcceptRequestAsync(long requestId, long acceptorId, string lang);
    Task<(bool Ok, string Msg)> DeclineRequestAsync(long requestId, long declinerId, string lang);
    Task<(bool Ok, string Msg)> RemoveFriendAsync(long charId, long friendId, string lang);
    Task<List<FriendDto>>       GetFriendsAsync(long charId);
    Task<List<FriendRequestDto>> GetPendingRequestsAsync(long charId);
    Task<bool>                  IsFriendAsync(long a, long b);
    Task<(bool Ok, string Msg)> BlockCharacterAsync(long blockerId, long blockedId, string lang);
    Task<(bool Ok, string Msg)> UnblockAsync(long blockerId, long blockedId, string lang);
    Task<bool>                  IsBlockedAsync(long a, long b);
}

public class FriendService(
    GameDbContext        db,
    IGameStateService    state,
    ILocalizationService loc) : IFriendService
{
    // ─── Send request ────────────────────────────────────────
    public async Task<(bool, string)> SendRequestAsync(long fromId, long toId, string lang)
    {
        if (fromId == toId)
            return (false, loc.Get("error.bad_request", lang));

        // Chặn nếu đã block
        if (await IsBlockedAsync(fromId, toId))
            return (false, loc.Get("error.forbidden", lang));

        // Đã là bạn?
        if (await IsFriendAsync(fromId, toId))
            return (false, loc.Get("error.bad_request", lang));

        // Có request pending chưa?
        var exists = await db.FriendRequests
            .AnyAsync(r => r.FromId == fromId && r.ToId == toId && r.Status == "pending");
        if (exists)
            return (false, loc.Get("error.bad_request", lang));

        db.FriendRequests.Add(new FriendRequest { FromId = fromId, ToId = toId });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Accept ──────────────────────────────────────────────
    public async Task<(bool, string)> AcceptRequestAsync(long requestId, long acceptorId, string lang)
    {
        var req = await db.FriendRequests
            .Include(r => r.From)
            .FirstOrDefaultAsync(r => r.Id == requestId
                && r.ToId == acceptorId && r.Status == "pending");

        if (req is null)
            return (false, loc.Get("error.not_found", lang));

        req.Status = "accepted";

        // Tạo quan hệ Friend 2 chiều
        db.Relationships.Add(new Relationship
        {
            CharacterA  = Math.Min(req.FromId, acceptorId),
            CharacterB  = Math.Max(req.FromId, acceptorId),
            RelType     = RelType.Friend,
            InitiatedBy = req.FromId,
            Intimacy    = 0,
        });

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Decline ─────────────────────────────────────────────
    public async Task<(bool, string)> DeclineRequestAsync(long requestId, long declinerId, string lang)
    {
        var req = await db.FriendRequests
            .FirstOrDefaultAsync(r => r.Id == requestId
                && r.ToId == declinerId && r.Status == "pending");
        if (req is null)
            return (false, loc.Get("error.not_found", lang));

        req.Status = "rejected";
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Remove friend ───────────────────────────────────────
    public async Task<(bool, string)> RemoveFriendAsync(long charId, long friendId, string lang)
    {
        var a = Math.Min(charId, friendId);
        var b = Math.Max(charId, friendId);

        var rel = await db.Relationships
            .FirstOrDefaultAsync(r => r.CharacterA == a && r.CharacterB == b
                && r.RelType == RelType.Friend);
        if (rel is null)
            return (false, loc.Get("error.not_found", lang));

        db.Relationships.Remove(rel);

        // Xóa các friend request liên quan
        await db.FriendRequests
            .Where(r => (r.FromId == charId && r.ToId == friendId)
                     || (r.FromId == friendId && r.ToId == charId))
            .ExecuteDeleteAsync();

        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Get friends ─────────────────────────────────────────
    public async Task<List<FriendDto>> GetFriendsAsync(long charId)
    {
        var rels = await db.Relationships
            .Where(r => (r.CharacterA == charId || r.CharacterB == charId)
                     && r.RelType == RelType.Friend)
            .Select(r => r.CharacterA == charId ? r.CharacterB : r.CharacterA)
            .ToListAsync();

        var friends = await db.Characters
            .Where(c => rels.Contains(c.Id))
            .Select(c => new
            {
                c.Id, c.Name, c.Level, c.MapId,
                FactionName = c.FactionMemberships
                    .Select(f => f.Faction.Name).FirstOrDefault(),
            })
            .ToListAsync();

        return friends.Select(f => new FriendDto(
            f.Id, f.Name, f.Level,
            state.IsOnline(f.Id),
            f.MapId,
            f.FactionName
        )).ToList();
    }

    // ─── Pending requests ────────────────────────────────────
    public async Task<List<FriendRequestDto>> GetPendingRequestsAsync(long charId)
    {
        return await db.FriendRequests
            .Where(r => r.ToId == charId && r.Status == "pending")
            .Include(r => r.From)
            .OrderByDescending(r => r.SentAt)
            .Select(r => new FriendRequestDto(
                r.Id,
                r.FromId,
                r.From.Name,
                r.From.Level,
                r.Status,
                new DateTimeOffset(r.SentAt).ToUnixTimeMilliseconds()
            ))
            .ToListAsync();
    }

    // ─── Is friend ───────────────────────────────────────────
    public async Task<bool> IsFriendAsync(long a, long b)
    {
        var minId = Math.Min(a, b);
        var maxId = Math.Max(a, b);
        return await db.Relationships.AnyAsync(r =>
            r.CharacterA == minId && r.CharacterB == maxId
            && r.RelType == RelType.Friend);
    }

    // ─── Block ───────────────────────────────────────────────
    public async Task<(bool, string)> BlockCharacterAsync(long blockerId, long blockedId, string lang)
    {
        if (blockerId == blockedId)
            return (false, loc.Get("error.bad_request", lang));

        var already = await db.CharacterBlocks
            .AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId);
        if (already)
            return (false, loc.Get("error.bad_request", lang));

        // Tự động hủy bạn bè nếu có
        await RemoveFriendAsync(blockerId, blockedId, lang);

        db.CharacterBlocks.Add(new CharacterBlock
        {
            BlockerId = blockerId,
            BlockedId = blockedId,
        });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    // ─── Unblock ─────────────────────────────────────────────
    public async Task<(bool, string)> UnblockAsync(long blockerId, long blockedId, string lang)
    {
        var deleted = await db.CharacterBlocks
            .Where(b => b.BlockerId == blockerId && b.BlockedId == blockedId)
            .ExecuteDeleteAsync();
        return deleted > 0
            ? (true, "OK")
            : (false, loc.Get("error.not_found", lang));
    }

    // ─── Is blocked ──────────────────────────────────────────
    public async Task<bool> IsBlockedAsync(long a, long b)
    {
        return await db.CharacterBlocks.AnyAsync(bl =>
            (bl.BlockerId == a && bl.BlockedId == b) ||
            (bl.BlockerId == b && bl.BlockedId == a));
    }
}
