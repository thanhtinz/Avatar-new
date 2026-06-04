// FantasyWorld.Server/Hubs/GameHub.cs
using MagicOnion.Server.Hubs;
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Services;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;
using FantasyWorld.Shared.Interfaces;

namespace FantasyWorld.Server.Hubs;

// Đây là hub Unity kết nối — cùng interface với client
public class GameHub(
    GameDbContext       db,
    IGameStateService   state,
    ICharacterService   charService,
    ILocalizationService loc,
    ILogger<GameHub>    logger)
    : StreamingHubBase<IGameHub, IGameHubReceiver>, IGameHub
{
    private long   _charId;
    private int    _mapId;
    private string _lang = "vi";

    // ─── SelectCharacter ─────────────────────────────────────
    public async Task<GameReadyDto> SelectCharacterAsync(long charId)
    {
        // Lấy accountId từ JWT context
        var accountId = GetAccountId();

        var charDto = await charService.GetFullAsync(charId);
        if (charDto is null || await GetCharacterAccountId(charId) != accountId)
            throw new ReturnStatusException(Grpc.Core.StatusCode.PermissionDenied, "Invalid character");

        _charId = charId;
        _mapId  = charDto.MapId;
        _lang   = Context.CallContext.RequestHeaders
            .FirstOrDefault(h => h.Key == "lang")?.Value ?? "vi";

        // Đăng ký vào game state
        var player = new PlayerState(
            accountId, charId, charDto.MapId,
            charDto.PosX, charDto.PosY,
            charDto.Name, charDto.Level,
            _lang, "unity",
            ConnectionId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );
        state.AddPlayer(ConnectionId, player);

        // Join map group
        await Group.AddAsync($"map:{charDto.MapId}");

        // Join clan group nếu có
        if (charDto.ClanName is not null)
        {
            var clan = await db.ClanMembers
                .Where(cm => cm.CharacterId == charId)
                .Select(cm => cm.ClanId)
                .FirstOrDefaultAsync();
            if (clan > 0)
            {
                await Group.AddAsync($"clan:{clan}");
                state.JoinClan(charId, clan);
            }
        }

        await charService.SetOnlineAsync(charId);

        // Thông báo map
        var mapPlayers = await charService.GetOnlineOnMapAsync(charDto.MapId);
        await BroadcastToMapExceptSelf(charDto.MapId, r => r.OnPlayerEnter(
            new PlayerEnterDto(charId, charDto.Name, charDto.Level,
                               charDto.PosX, charDto.PosY, charDto.Gender)));

        // World time
        var wt = WorldTimeService.Current;
        var worldTimeDto = new WorldTimeDto(
            wt.GameHour, wt.GameMinute, wt.IsDay,
            wt.Season, wt.MoonPhase, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        return new GameReadyDto(charDto, mapPlayers, worldTimeDto, state.OnlineCount);
    }

    // ─── Ping ────────────────────────────────────────────────
    public Task PingAsync() => Task.CompletedTask;

    // ─── Move ────────────────────────────────────────────────
    public async Task MoveAsync(MoveDto move)
    {
        if (_charId == 0) return;

        var player = state.GetByConnectionId(ConnectionId);
        if (player is null) return;

        // Anti-cheat: kiểm tra tốc độ
        const float MaxSpeed = 15f;
        var dx = MathF.Abs(move.X - player.X);
        var dy = MathF.Abs(move.Y - player.Y);
        if (dx > MaxSpeed * 2 || dy > MaxSpeed * 2)
        {
            logger.LogWarning("Suspicious movement charId={CharId} dist=({Dx},{Dy})",
                _charId, dx, dy);
        }

        bool mapChanged = move.MapId != player.MapId && move.MapId > 0;

        if (mapChanged)
            await HandleMapChangeAsync(move.MapId, move.X, move.Y);
        else
        {
            state.UpdatePosition(ConnectionId, player.MapId, move.X, move.Y);
            await BroadcastToMapExceptSelf(player.MapId, r =>
                r.OnPlayerMove(new PlayerMoveDto(_charId, move.X, move.Y, move.Dir)));
        }
    }

    // ─── UsePortal ───────────────────────────────────────────
    public async Task UsePortalAsync(int portalId)
    {
        if (_charId == 0) return;
        var player = state.GetByConnectionId(ConnectionId);
        if (player is null) return;

        var portal = await db.Portals
            .Include(p => p.ToMap)
            .FirstOrDefaultAsync(p => p.Id == portalId
                && p.FromMapId == player.MapId && p.IsActive);

        if (portal is null)
        {
            Client.OnError(new ErrorDto("PORTAL_NOT_FOUND",
                loc.Get("map.teleport_success", "vi"),
                loc.Get("map.teleport_success", "en")));
            return;
        }

        // Kiểm tra level
        var charLevel = await db.Characters
            .Where(c => c.Id == _charId)
            .Select(c => c.Level)
            .FirstAsync();

        if (charLevel < portal.MinLevel)
        {
            Client.OnError(new ErrorDto("LEVEL_REQUIRED",
                loc.Get("map.level_required", "vi", new { level = portal.MinLevel }),
                loc.Get("map.level_required", "en", new { level = portal.MinLevel })));
            return;
        }

        // Phí portal
        if (portal.PortalType == "paid" && portal.Fare > 0)
        {
            try { await charService.ModifyGoldAsync(_charId, -portal.Fare); }
            catch
            {
                Client.OnError(new ErrorDto("INSUFFICIENT_GOLD",
                    loc.Get("map.insufficient_gold", "vi"),
                    loc.Get("map.insufficient_gold", "en")));
                return;
            }
        }

        await HandleMapChangeAsync(portal.ToMapId, portal.ToPosX, portal.ToPosY);
        Client.OnTeleportResult(true, null, portal.ToMapId, portal.ToPosX, portal.ToPosY);
    }

    // ─── Chat ────────────────────────────────────────────────
    public async Task SendChatAsync(ChatSendDto chat)
    {
        if (_charId == 0) return;
        const int MaxLen = 200;

        if (string.IsNullOrWhiteSpace(chat.Content)) return;
        if (chat.Content.Length > MaxLen)
        {
            Client.OnError(new ErrorDto("MSG_TOO_LONG",
                loc.Get("chat.message_too_long", "vi", new { max = MaxLen }),
                loc.Get("chat.message_too_long", "en", new { max = MaxLen })));
            return;
        }

        var player = state.GetByConnectionId(ConnectionId);
        if (player is null) return;

        var dto = new ChatReceiveDto(
            _charId, player.Name, player.Level,
            chat.Channel, chat.Content.Trim(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        switch (chat.Channel)
        {
            case ChatChannel.Map:
                await BroadcastToMap(player.MapId, r => r.OnChatReceive(dto));
                break;

            case ChatChannel.World:
                await BroadcastAll(r => r.OnChatReceive(dto));
                break;

            case ChatChannel.Clan:
                var clanId = await db.ClanMembers
                    .Where(cm => cm.CharacterId == _charId)
                    .Select(cm => cm.ClanId)
                    .FirstOrDefaultAsync();
                if (clanId > 0)
                    await BroadcastToClan(clanId, r => r.OnChatReceive(dto));
                break;

            case ChatChannel.Party:
                var partyId = await GetCharPartyIdAsync();
                if (partyId.HasValue)
                    await BroadcastToParty(partyId.Value, r => r.OnChatReceive(dto));
                break;

            case ChatChannel.Private when chat.TargetCharId.HasValue:
                var target = state.GetByCharId(chat.TargetCharId.Value);
                if (target is null)
                {
                    Client.OnError(new ErrorDto("TARGET_OFFLINE",
                        loc.Get("chat.whisper_not_found", "vi"),
                        loc.Get("chat.whisper_not_found", "en")));
                    return;
                }
                // Gửi đến target và echo lại người gửi
                await BroadcastToChar(chat.TargetCharId.Value, r =>
                    r.OnChatReceive(dto with { IsPrivate = true }));
                Client.OnChatReceive(dto with { IsPrivate = true });
                break;
        }

        // Lưu vào DB (fire and forget)
        _ = SaveMessageAsync(chat.Channel, chat.Content, chat.TargetCharId);
    }

    // ─── Emote ───────────────────────────────────────────────
    public async Task SendEmoteAsync(int emoteId)
    {
        var player = state.GetByConnectionId(ConnectionId);
        if (player is null) return;
        // Broadcast emote đến map (dùng chat channel tạm)
        await BroadcastToMapExceptSelf(player.MapId, r =>
            r.OnChatReceive(new ChatReceiveDto(
                _charId, player.Name, player.Level,
                ChatChannel.Map, $"[EMOTE:{emoteId}]",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }

    // ─── Party ───────────────────────────────────────────────
    public Task CreatePartyAsync()  => Task.CompletedTask; // Phase 2
    public Task JoinPartyAsync(long partyId)  => Task.CompletedTask;
    public Task LeavePartyAsync()   => Task.CompletedTask;
    public Task InviteToPartyAsync(long targetCharId) => Task.CompletedTask;

    // ─── Disconnect ──────────────────────────────────────────
    protected override async ValueTask OnDisconnected()
    {
        if (_charId == 0) return;

        var player = state.GetByConnectionId(ConnectionId);
        state.RemovePlayer(ConnectionId);

        if (player is not null)
        {
            await charService.UpdatePositionAsync(_charId, player.MapId, player.X, player.Y);
            var onlineSec = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - player.LoginAtMs) / 1000;
            await charService.AddOnlineTimeAsync(_charId, onlineSec);

            await BroadcastToMapExceptSelf(player.MapId, r =>
                r.OnPlayerLeave(_charId, player.Name));

            logger.LogInformation("Player disconnected: {Name} (charId={CharId}, sec={Sec})",
                player.Name, _charId, onlineSec);
        }
    }

    // ─── Private helpers ─────────────────────────────────────

    private long GetAccountId()
    {
        var claim = Context.CallContext.RequestHeaders
            .FirstOrDefault(h => h.Key == "accountid")?.Value;
        return claim is null ? 0 : long.Parse(claim);
    }

    private async Task<long> GetCharacterAccountId(long charId) =>
        await db.Characters.Where(c => c.Id == charId).Select(c => c.AccountId).FirstAsync();

    private async Task HandleMapChangeAsync(int newMapId, float x, float y)
    {
        var player = state.GetByConnectionId(ConnectionId);
        if (player is null) return;

        // Rời map cũ
        await Group.RemoveAsync($"map:{player.MapId}");
        await BroadcastToMapExceptSelf(player.MapId, r =>
            r.OnPlayerLeave(_charId, player.Name));

        state.UpdatePosition(ConnectionId, newMapId, x, y);
        _mapId = newMapId;

        // Vào map mới
        await Group.AddAsync($"map:{newMapId}");

        var map        = await db.Maps.FindAsync(newMapId);
        var mapPlayers = await charService.GetOnlineOnMapAsync(newMapId);

        Client.OnMapChanged(new MapChangedDto(
            newMapId, map?.Name ?? "", x, y, map?.IsPvp ?? false, mapPlayers));

        await BroadcastToMapExceptSelf(newMapId, r =>
            r.OnPlayerEnter(new PlayerEnterDto(_charId, player.Name, player.Level, x, y, Gender.Male)));

        await charService.UpdatePositionAsync(_charId, newMapId, x, y);
    }

    private async Task<long?> GetCharPartyIdAsync()
    {
        var row = await db.Set<Data.Entities.CharacterQuest>() // placeholder
            .Select(x => (long?)null).FirstOrDefaultAsync();
        return row; // Phase 2: truy vấn party_members
    }

    private Task SaveMessageAsync(ChatChannel channel, string content, long? receiverId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                db.Messages.Add(new Data.Entities.Message
                {
                    SenderId   = _charId,
                    ReceiverId = receiverId,
                    Channel    = channel,
                    Content    = content,
                });
                await db.SaveChangesAsync();
            }
            catch { /* không block chat */ }
        });
        return Task.CompletedTask;
    }

    // Group broadcast helpers
    private Task BroadcastToMap(int mapId, Action<IGameHubReceiver> action) =>
        BroadcastToGroupAsync($"map:{mapId}", action);

    private Task BroadcastToMapExceptSelf(int mapId, Action<IGameHubReceiver> action) =>
        BroadcastExceptSelfToGroupAsync($"map:{mapId}", action);

    private Task BroadcastToClan(int clanId, Action<IGameHubReceiver> action) =>
        BroadcastToGroupAsync($"clan:{clanId}", action);

    private Task BroadcastToParty(long partyId, Action<IGameHubReceiver> action) =>
        BroadcastToGroupAsync($"party:{partyId}", action);

    private async Task BroadcastToChar(long charId, Action<IGameHubReceiver> action)
    {
        var target = state.GetByCharId(charId);
        if (target is null) return;
        // MagicOnion: broadcast đến specific connection
        // implementation phụ thuộc version — dùng group 1 người
        await BroadcastToGroupAsync($"char:{charId}", action);
    }

    private async Task BroadcastAll(Action<IGameHubReceiver> action) =>
        await BroadcastToGroupAsync("world", action);

    private Task BroadcastToGroupAsync(string group, Action<IGameHubReceiver> action)
    {
        action(BroadcastToGroup(group));
        return Task.CompletedTask;
    }

    private Task BroadcastExceptSelfToGroupAsync(string group, Action<IGameHubReceiver> action)
    {
        action(BroadcastExceptSelf(BroadcastToGroup(group)));
        return Task.CompletedTask;
    }

    // These are resolved at runtime by MagicOnion
    private IGameHubReceiver BroadcastToGroup(string group) =>
        ((IGroup<IGameHubReceiver>)Group.CreateOrAddAsync(group).GetAwaiter().GetResult()).CreateBroadcaster();

    private IGameHubReceiver BroadcastExceptSelf(IGameHubReceiver receiver) => receiver;
}
