// FantasyWorld.Server/Services/GameStateService.cs
using System.Collections.Concurrent;

namespace FantasyWorld.Server.Services;

public record PlayerState(
    long   AccountId,
    long   CharId,
    int    MapId,
    float  X,
    float  Y,
    string Name,
    int    Level,
    string Lang,
    string ClientType,
    string ConnectionId,   // MagicOnion / SignalR connection id
    long   LoginAtMs
);

public interface IGameStateService
{
    void   AddPlayer(string connectionId, PlayerState state);
    void   RemovePlayer(string connectionId);
    PlayerState? GetByConnectionId(string connectionId);
    PlayerState? GetByCharId(long charId);
    bool   IsOnline(long charId);
    void   UpdatePosition(string connectionId, int mapId, float x, float y);
    IReadOnlyList<long> GetOnMap(int mapId);
    int    OnlineCount { get; }
    void   JoinClan(long charId, int clanId);
    void   LeaveClan(long charId, int clanId);
    IReadOnlyList<long> GetClanOnline(int clanId);
    void   JoinParty(long charId, long partyId);
    void   LeaveParty(long charId, long partyId);
    IReadOnlyList<long> GetPartyOnline(long partyId);
}

public class GameStateService : IGameStateService
{
    // connectionId → PlayerState
    private readonly ConcurrentDictionary<string, PlayerState> _players = new();
    // charId → connectionId
    private readonly ConcurrentDictionary<long, string>  _charConn  = new();
    // mapId  → Set<charId>
    private readonly ConcurrentDictionary<int,  HashSet<long>> _mapChars = new();
    // clanId → Set<charId>
    private readonly ConcurrentDictionary<int,  HashSet<long>> _clanChars = new();
    // partyId → Set<charId>
    private readonly ConcurrentDictionary<long, HashSet<long>> _partyChars = new();

    private readonly object _mapLock   = new();
    private readonly object _clanLock  = new();
    private readonly object _partyLock = new();

    public int OnlineCount => _players.Count;

    // ─── Player ──────────────────────────────────────────────
    public void AddPlayer(string connectionId, PlayerState state)
    {
        _players[connectionId] = state;
        _charConn[state.CharId] = connectionId;
        AddToMap(state.MapId, state.CharId);
    }

    public void RemovePlayer(string connectionId)
    {
        if (!_players.TryRemove(connectionId, out var state)) return;
        _charConn.TryRemove(state.CharId, out _);
        RemoveFromMap(state.MapId, state.CharId);
    }

    public PlayerState? GetByConnectionId(string connectionId) =>
        _players.GetValueOrDefault(connectionId);

    public PlayerState? GetByCharId(long charId)
    {
        if (!_charConn.TryGetValue(charId, out var connId)) return null;
        return _players.GetValueOrDefault(connId);
    }

    public bool IsOnline(long charId) => _charConn.ContainsKey(charId);

    public void UpdatePosition(string connectionId, int mapId, float x, float y)
    {
        if (!_players.TryGetValue(connectionId, out var old)) return;

        if (old.MapId != mapId)
        {
            RemoveFromMap(old.MapId, old.CharId);
            AddToMap(mapId, old.CharId);
        }

        _players[connectionId] = old with { MapId = mapId, X = x, Y = y };
    }

    public IReadOnlyList<long> GetOnMap(int mapId)
    {
        lock (_mapLock)
        {
            return _mapChars.TryGetValue(mapId, out var set)
                ? [.. set] : [];
        }
    }

    // ─── Map ─────────────────────────────────────────────────
    private void AddToMap(int mapId, long charId)
    {
        lock (_mapLock)
        {
            _mapChars.GetOrAdd(mapId, _ => []).Add(charId);
        }
    }

    private void RemoveFromMap(int mapId, long charId)
    {
        lock (_mapLock)
        {
            if (_mapChars.TryGetValue(mapId, out var set))
                set.Remove(charId);
        }
    }

    // ─── Clan ────────────────────────────────────────────────
    public void JoinClan(long charId, int clanId)
    {
        lock (_clanLock)
            _clanChars.GetOrAdd(clanId, _ => []).Add(charId);
    }

    public void LeaveClan(long charId, int clanId)
    {
        lock (_clanLock)
        {
            if (_clanChars.TryGetValue(clanId, out var set))
                set.Remove(charId);
        }
    }

    public IReadOnlyList<long> GetClanOnline(int clanId)
    {
        lock (_clanLock)
            return _clanChars.TryGetValue(clanId, out var set) ? [.. set] : [];
    }

    // ─── Party ───────────────────────────────────────────────
    public void JoinParty(long charId, long partyId)
    {
        lock (_partyLock)
            _partyChars.GetOrAdd(partyId, _ => []).Add(charId);
    }

    public void LeaveParty(long charId, long partyId)
    {
        lock (_partyLock)
        {
            if (_partyChars.TryGetValue(partyId, out var set))
                set.Remove(charId);
        }
    }

    public IReadOnlyList<long> GetPartyOnline(long partyId)
    {
        lock (_partyLock)
            return _partyChars.TryGetValue(partyId, out var set) ? [.. set] : [];
    }
}
