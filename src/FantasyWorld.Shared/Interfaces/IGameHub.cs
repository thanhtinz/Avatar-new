// FantasyWorld.Shared/Interfaces/IGameHub.cs
// Unity client dùng interface này để gọi server — CÙNG 1 FILE
using MagicOnion;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Shared.Interfaces;

/// <summary>
/// Methods client gọi lên server (client → server)
/// </summary>
public interface IGameHub : IStreamingHub<IGameHub, IGameHubReceiver>
{
    // Connection
    Task<GameReadyDto> SelectCharacterAsync(long charId);
    Task PingAsync();

    // Movement
    Task MoveAsync(MoveDto move);
    Task UsePortalAsync(int portalId);

    // Chat
    Task SendChatAsync(ChatSendDto chat);
    Task SendEmoteAsync(int emoteId);

    // Party
    Task CreatePartyAsync();
    Task JoinPartyAsync(long partyId);
    Task LeavePartyAsync();
    Task InviteToPartyAsync(long targetCharId);
}

/// <summary>
/// Methods server gửi xuống client (server → client)
/// </summary>
public interface IGameHubReceiver
{
    // Movement broadcasts
    void OnPlayerMove(PlayerMoveDto data);
    void OnPlayerEnter(PlayerEnterDto data);
    void OnPlayerLeave(long charId, string name);
    void OnMapChanged(MapChangedDto data);
    void OnTeleportResult(bool success, string? error, int mapId, float x, float y);

    // Chat
    void OnChatReceive(ChatReceiveDto data);
    void OnSystemMessage(string type, string contentVi, string contentEn);

    // World
    void OnWorldTime(WorldTimeDto data);
    void OnNewDay(int day, SeasonCode season, int moonPhase);
    void OnSeasonChange(SeasonChangeDto data);
    void OnFullMoon(int day);
    void OnWorldEvent(string name, string type, string descVi, string descEn);

    // Character
    void OnLevelUp(int newLevel, string messageVi, string messageEn);
    void OnGoldChanged(long newGold, long delta);
    void OnHpChanged(int hp, int hpMax);
    void OnLifeStatsChanged(byte hunger, byte energy, byte mood);

    // Social
    void OnPartyInvite(long fromCharId, string fromName);
    void OnPartyUpdate(List<long> memberIds);
    void OnFriendOnline(long charId, string name);

    // Error
    void OnError(ErrorDto error);
}
