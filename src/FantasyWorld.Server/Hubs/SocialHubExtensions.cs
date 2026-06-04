// FantasyWorld.Server/Hubs/SocialHubExtensions.cs
// Extension methods bổ sung vào GameHub cho social features
using MagicOnion.Server.Hubs;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Services;
using FantasyWorld.Server.Services.Social;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Interfaces;

namespace FantasyWorld.Server.Hubs;

/// <summary>
/// Social event broadcaster — dùng từ services để push realtime đến client
/// </summary>
public interface ISocialEventPusher
{
    /// <summary>Thông báo có lời mời kết bạn</summary>
    Task PushFriendRequestAsync(long toCharId, FriendRequestDto req);

    /// <summary>Thông báo bạn bè online/offline</summary>
    Task PushFriendOnlineStatusAsync(long charId, bool isOnline);

    /// <summary>Thông báo party invite</summary>
    Task PushPartyInviteAsync(long toCharId, PartyInviteDto invite);

    /// <summary>Cập nhật party state cho tất cả thành viên</summary>
    Task PushPartyUpdateAsync(long partyId, PartyDto party);

    /// <summary>Thông báo clan event (ai đó join/leave)</summary>
    Task PushClanEventAsync(int clanId, string eventType, string charName, string lang);

    /// <summary>Thông báo cá nhân (loot, exp, system)</summary>
    Task PushPersonalNotifAsync(long charId, string type, string msgVi, string msgEn);
}

public class SocialEventPusher(
    IGameStateService    state,
    ILocalizationService loc) : ISocialEventPusher
{
    // Cache broadcaster theo connectionId
    // Trong thực tế dùng IHubContext của MagicOnion
    // Đây là wrapper để gọi từ services

    private static readonly Dictionary<string, IGameHubReceiver> _connections = new();

    public static void Register(string connectionId, IGameHubReceiver receiver)
        => _connections[connectionId] = receiver;

    public static void Unregister(string connectionId)
        => _connections.Remove(connectionId);

    // ─── Friend request ──────────────────────────────────────
    public Task PushFriendRequestAsync(long toCharId, FriendRequestDto req)
    {
        var player = state.GetByCharId(toCharId);
        if (player is null) return Task.CompletedTask;
        if (_connections.TryGetValue(player.ConnectionId, out var receiver))
            receiver.OnFriendOnline(req.FromCharId, req.FromName);
        return Task.CompletedTask;
    }

    // ─── Friend online ───────────────────────────────────────
    public Task PushFriendOnlineStatusAsync(long charId, bool isOnline)
    {
        // Notify tất cả friends của charId
        var player = state.GetByCharId(charId);
        if (player is null) return Task.CompletedTask;

        // Broadcast đơn giản — friend list đầy đủ sẽ query ở client
        return Task.CompletedTask;
    }

    // ─── Party invite ────────────────────────────────────────
    public Task PushPartyInviteAsync(long toCharId, PartyInviteDto invite)
    {
        var player = state.GetByCharId(toCharId);
        if (player is null) return Task.CompletedTask;
        if (_connections.TryGetValue(player.ConnectionId, out var receiver))
            receiver.OnPartyInvite(invite.InviterCharId, invite.InviterName);
        return Task.CompletedTask;
    }

    // ─── Party update ────────────────────────────────────────
    public Task PushPartyUpdateAsync(long partyId, PartyDto party)
    {
        var memberIds = party.Members.Select(m => m.CharId).ToList();
        foreach (var memberId in memberIds)
        {
            var p = state.GetByCharId(memberId);
            if (p is null) continue;
            if (_connections.TryGetValue(p.ConnectionId, out var receiver))
                receiver.OnPartyUpdate(memberIds);
        }
        return Task.CompletedTask;
    }

    // ─── Clan event ──────────────────────────────────────────
    public Task PushClanEventAsync(int clanId, string eventType, string charName, string lang)
    {
        var online = state.GetClanOnline(clanId);
        foreach (var memberId in online)
        {
            var p = state.GetByCharId(memberId);
            if (p is null) continue;
            if (_connections.TryGetValue(p.ConnectionId, out var receiver))
            {
                var msgVi = eventType switch
                {
                    "join"  => $"{charName} đã gia nhập gia tộc!",
                    "leave" => $"{charName} đã rời khỏi gia tộc",
                    "kick"  => $"{charName} bị trục xuất khỏi gia tộc",
                    _       => $"Gia tộc: {eventType}"
                };
                var msgEn = eventType switch
                {
                    "join"  => $"{charName} joined the clan!",
                    "leave" => $"{charName} left the clan",
                    "kick"  => $"{charName} was kicked from the clan",
                    _       => $"Clan: {eventType}"
                };
                receiver.OnSystemMessage($"clan_{eventType}", msgVi, msgEn);
            }
        }
        return Task.CompletedTask;
    }

    // ─── Personal notification ───────────────────────────────
    public Task PushPersonalNotifAsync(long charId, string type, string msgVi, string msgEn)
    {
        var p = state.GetByCharId(charId);
        if (p is null) return Task.CompletedTask;
        if (_connections.TryGetValue(p.ConnectionId, out var receiver))
            receiver.OnSystemMessage(type, msgVi, msgEn);
        return Task.CompletedTask;
    }
}
