// client/UnityClient/Scripts/Network/NetworkManager.cs
// Unity C# — attach to a persistent GameObject in your scene
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MagicOnion.Client;
using Grpc.Core;
using Grpc.Net.Client;
using FantasyWorld.Shared.Interfaces;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Client.Network
{
    /// <summary>
    /// Singleton NetworkManager — quản lý kết nối MagicOnion tới game server
    /// Attach vào GameObject "NetworkManager" trong scene đầu tiên, DontDestroyOnLoad
    /// </summary>
    public class NetworkManager : MonoBehaviour, IGameHubReceiver
    {
        // ─── Singleton ───────────────────────────────────────
        public static NetworkManager Instance { get; private set; }

        // ─── Inspector settings ──────────────────────────────
        [Header("Server")]
        [SerializeField] private string serverUrl    = "http://localhost:3000";
        [SerializeField] private string apiBaseUrl   = "http://localhost:3000/api";
        [SerializeField] private int    reconnectDelay = 3;   // seconds
        [SerializeField] private int    maxReconnects  = 5;

        [Header("Debug")]
        [SerializeField] private bool   logPackets = true;

        // ─── State ───────────────────────────────────────────
        public  bool   IsConnected  { get; private set; }
        public  bool   IsLoggedIn   { get; private set; }
        public  long   MyCharId     { get; private set; }
        public  string Language     { get; private set; } = "vi";

        private GrpcChannel    _channel;
        private IGameHub       _hub;
        private string         _accessToken;
        private string         _refreshToken;
        private int            _reconnectCount;
        private bool           _intentionalDisconnect;
        private ApiClient      _api;

        // ─── Events (subscribe từ GameManager, UIManager...) ─
        public event Action<GameReadyDto>       OnGameReady;
        public event Action<PlayerMoveDto>      OnPlayerMoved;
        public event Action<PlayerEnterDto>     OnPlayerEntered;
        public event Action<long, string>       OnPlayerLeft;
        public event Action<MapChangedDto>      OnMapChanged;
        public event Action<ChatReceiveDto>     OnChatReceived;
        public event Action<string, string, string> OnSystemMessage;
        public event Action<WorldTimeDto>       OnWorldTimeUpdated;
        public event Action<SeasonChangeDto>    OnSeasonChanged;
        public event Action<int>                OnFullMoon;
        public event Action<string, string, string> OnWorldEvent;
        public event Action<int, string, string>    OnLevelUp;
        public event Action<long, long>             OnGoldChanged;
        public event Action<int, int>               OnHpChanged;
        public event Action<byte, byte, byte>       OnLifeStatsChanged;
        public event Action<long, string>           OnPartyInvite;
        public event Action<List<long>>             OnPartyUpdate;
        public event Action<long, string>           OnFriendOnline;
        public event Action<ErrorDto>               OnError;
        public event Action<bool>                   OnConnectionChanged;

        // ─── Unity lifecycle ─────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _api = new ApiClient(apiBaseUrl);
        }

        private void OnDestroy()
        {
            _intentionalDisconnect = true;
            _ = DisconnectAsync();
        }

        // ─── Auth ────────────────────────────────────────────

        /// <summary>Đăng ký tài khoản mới</summary>
        public Task<ApiResponse<object>> RegisterAsync(
            string username, string password, string email = null)
            => _api.PostAsync<object>("auth/register",
                new { username, password, email });

        /// <summary>Đăng nhập và lấy token</summary>
        public async Task<bool> LoginAsync(string username, string password)
        {
            var resp = await _api.PostAsync<LoginResponseData>("auth/login",
                new { username, password });

            if (!resp.Success || resp.Data == null) return false;

            _accessToken  = resp.Data.AccessToken;
            _refreshToken = resp.Data.RefreshToken;
            _api.SetToken(_accessToken);
            IsLoggedIn = true;

            // Lưu token vào PlayerPrefs (encrypted trong production)
            PlayerPrefs.SetString("access_token",  _accessToken);
            PlayerPrefs.SetString("refresh_token", _refreshToken);
            PlayerPrefs.Save();

            return true;
        }

        /// <summary>Khôi phục session từ saved token</summary>
        public async Task<bool> RestoreSessionAsync()
        {
            var saved = PlayerPrefs.GetString("access_token", "");
            if (string.IsNullOrEmpty(saved)) return false;

            _accessToken = saved;
            _api.SetToken(_accessToken);

            var me = await _api.GetAsync<object>("auth/me");
            if (!me.Success)
            {
                // Thử refresh
                var refresh = PlayerPrefs.GetString("refresh_token", "");
                if (!string.IsNullOrEmpty(refresh))
                    return await RefreshTokenAsync(refresh);
                return false;
            }

            IsLoggedIn = true;
            return true;
        }

        private async Task<bool> RefreshTokenAsync(string refreshToken)
        {
            var resp = await _api.PostAsync<RefreshResponseData>("auth/refresh",
                new { refreshToken });

            if (!resp.Success || resp.Data == null) return false;

            _accessToken = resp.Data.AccessToken;
            _api.SetToken(_accessToken);
            PlayerPrefs.SetString("access_token", _accessToken);
            PlayerPrefs.Save();

            IsLoggedIn = true;
            return true;
        }

        public async Task LogoutAsync()
        {
            await _api.PostAsync<object>("auth/logout", null);
            PlayerPrefs.DeleteKey("access_token");
            PlayerPrefs.DeleteKey("refresh_token");
            IsLoggedIn = false;
            _intentionalDisconnect = true;
            await DisconnectAsync();
        }

        // ─── MagicOnion connection ───────────────────────────

        /// <summary>Kết nối tới game hub và chọn nhân vật</summary>
        public async Task<bool> ConnectAndSelectCharAsync(long charId)
        {
            try
            {
                await ConnectHubAsync();
                var gameReady = await _hub.SelectCharacterAsync(charId);
                MyCharId = charId;
                OnGameReady?.Invoke(gameReady);

                if (logPackets)
                    Debug.Log($"[Net] Game ready — {gameReady.MapPlayers.Count} players on map");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Net] ConnectAndSelect failed: {ex.Message}");
                return false;
            }
        }

        private async Task ConnectHubAsync()
        {
            _channel?.Dispose();

            var options = GrpcChannelOptions.Empty;

            // Gắn JWT vào mỗi request
            var callCredentials = CallCredentials.FromInterceptor(
                (context, metadata) =>
                {
                    if (!string.IsNullOrEmpty(_accessToken))
                        metadata.Add("authorization", $"Bearer {_accessToken}");
                    metadata.Add("lang", Language);
                    return Task.CompletedTask;
                });

            _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Create(
                    ChannelCredentials.Insecure, callCredentials)
            });

            _hub = await StreamingHubClient.ConnectAsync<IGameHub, IGameHubReceiver>(
                _channel, this);

            IsConnected      = true;
            _reconnectCount  = 0;
            _intentionalDisconnect = false;
            OnConnectionChanged?.Invoke(true);

            // Start heartbeat
            StartCoroutine(HeartbeatCoroutine());

            if (logPackets) Debug.Log("[Net] Hub connected");
        }

        private async Task DisconnectAsync()
        {
            IsConnected = false;
            OnConnectionChanged?.Invoke(false);
            if (_hub != null) { await _hub.DisposeAsync(); _hub = null; }
            _channel?.Dispose();
            _channel = null;
        }

        // ─── Auto-reconnect ──────────────────────────────────

        private IEnumerator HeartbeatCoroutine()
        {
            while (IsConnected && !_intentionalDisconnect)
            {
                yield return new WaitForSeconds(20f);

                // Ping check
                Task pingTask = null;
                try { pingTask = _hub?.PingAsync(); }
                catch { /* hub disposed */ }

                if (pingTask == null) break;

                var timeout = Task.Delay(5000);
                yield return new WaitUntil(() => pingTask.IsCompleted || timeout.IsCompleted);

                if (!pingTask.IsCompleted)
                {
                    Debug.LogWarning("[Net] Heartbeat timeout — attempting reconnect");
                    _ = ReconnectAsync();
                    break;
                }
            }
        }

        private async Task ReconnectAsync()
        {
            if (_intentionalDisconnect) return;
            if (_reconnectCount >= maxReconnects)
            {
                Debug.LogError("[Net] Max reconnects reached");
                OnConnectionChanged?.Invoke(false);
                return;
            }

            _reconnectCount++;
            IsConnected = false;
            Debug.Log($"[Net] Reconnecting... ({_reconnectCount}/{maxReconnects})");

            await Task.Delay(reconnectDelay * 1000);

            try
            {
                await ConnectAndSelectCharAsync(MyCharId);
                Debug.Log("[Net] Reconnected!");
            }
            catch
            {
                _ = ReconnectAsync();
            }
        }

        // ─── Game actions ────────────────────────────────────

        public async Task MoveAsync(float x, float y, int mapId, MoveDir dir = MoveDir.Down)
        {
            if (!IsConnected) return;
            try { await _hub.MoveAsync(new MoveDto(x, y, mapId, dir)); }
            catch (Exception ex) { Debug.LogWarning($"[Net] Move error: {ex.Message}"); }
        }

        public async Task UsePortalAsync(int portalId)
        {
            if (!IsConnected) return;
            try { await _hub.UsePortalAsync(portalId); }
            catch (Exception ex) { Debug.LogWarning($"[Net] Portal error: {ex.Message}"); }
        }

        public async Task SendChatAsync(ChatChannel channel, string content, long? targetId = null)
        {
            if (!IsConnected) return;
            try { await _hub.SendChatAsync(new ChatSendDto(channel, content, targetId)); }
            catch (Exception ex) { Debug.LogWarning($"[Net] Chat error: {ex.Message}"); }
        }

        public async Task CreatePartyAsync()
        {
            if (!IsConnected) return;
            try { await _hub.CreatePartyAsync(); }
            catch (Exception ex) { Debug.LogWarning($"[Net] Party error: {ex.Message}"); }
        }

        public async Task InviteToPartyAsync(long targetCharId)
        {
            if (!IsConnected) return;
            try { await _hub.InviteToPartyAsync(targetCharId); }
            catch (Exception ex) { Debug.LogWarning($"[Net] Invite error: {ex.Message}"); }
        }

        // ─── IGameHubReceiver implementation ─────────────────

        void IGameHubReceiver.OnPlayerMove(PlayerMoveDto data)
        {
            if (data.CharId == MyCharId) return;
            MainThread.Enqueue(() => OnPlayerMoved?.Invoke(data));
        }

        void IGameHubReceiver.OnPlayerEnter(PlayerEnterDto data)
        {
            if (data.CharId == MyCharId) return;
            MainThread.Enqueue(() => OnPlayerEntered?.Invoke(data));
        }

        void IGameHubReceiver.OnPlayerLeave(long charId, string name)
            => MainThread.Enqueue(() => OnPlayerLeft?.Invoke(charId, name));

        void IGameHubReceiver.OnMapChanged(MapChangedDto data)
            => MainThread.Enqueue(() => OnMapChanged?.Invoke(data));

        void IGameHubReceiver.OnTeleportResult(bool success, string error, int mapId, float x, float y)
        {
            if (!success) MainThread.Enqueue(() =>
                OnError?.Invoke(new ErrorDto("TELEPORT_FAIL", error ?? "", error ?? "")));
        }

        void IGameHubReceiver.OnChatReceive(ChatReceiveDto data)
            => MainThread.Enqueue(() => OnChatReceived?.Invoke(data));

        void IGameHubReceiver.OnSystemMessage(string type, string vi, string en)
            => MainThread.Enqueue(() => OnSystemMessage?.Invoke(type, vi, en));

        void IGameHubReceiver.OnWorldTime(WorldTimeDto data)
            => MainThread.Enqueue(() => OnWorldTimeUpdated?.Invoke(data));

        void IGameHubReceiver.OnNewDay(int day, SeasonCode season, int moon)
        {
            if (logPackets) Debug.Log($"[World] Day {day}, {season}, moon={moon}");
        }

        void IGameHubReceiver.OnSeasonChange(SeasonChangeDto data)
            => MainThread.Enqueue(() => OnSeasonChanged?.Invoke(data));

        void IGameHubReceiver.OnFullMoon(int day)
            => MainThread.Enqueue(() => OnFullMoon?.Invoke(day));

        void IGameHubReceiver.OnWorldEvent(string name, string type, string vi, string en)
            => MainThread.Enqueue(() => OnWorldEvent?.Invoke(name, vi, en));

        void IGameHubReceiver.OnLevelUp(int lv, string vi, string en)
        {
            MainThread.Enqueue(() => OnLevelUp?.Invoke(lv, vi, en));
            if (logPackets) Debug.Log($"[Game] Level up → {lv}");
        }

        void IGameHubReceiver.OnGoldChanged(long newGold, long delta)
            => MainThread.Enqueue(() => OnGoldChanged?.Invoke(newGold, delta));

        void IGameHubReceiver.OnHpChanged(int hp, int hpMax)
            => MainThread.Enqueue(() => OnHpChanged?.Invoke(hp, hpMax));

        void IGameHubReceiver.OnLifeStatsChanged(byte hunger, byte energy, byte mood)
            => MainThread.Enqueue(() => OnLifeStatsChanged?.Invoke(hunger, energy, mood));

        void IGameHubReceiver.OnPartyInvite(long charId, string name)
            => MainThread.Enqueue(() => OnPartyInvite?.Invoke(charId, name));

        void IGameHubReceiver.OnPartyUpdate(List<long> memberIds)
            => MainThread.Enqueue(() => OnPartyUpdate?.Invoke(memberIds));

        void IGameHubReceiver.OnFriendOnline(long charId, string name)
            => MainThread.Enqueue(() => OnFriendOnline?.Invoke(charId, name));

        void IGameHubReceiver.OnError(ErrorDto error)
        {
            MainThread.Enqueue(() => OnError?.Invoke(error));
            Debug.LogWarning($"[Net] Server error: {error.Code} — {error.MessageVi}");
        }

        // ─── API proxy ───────────────────────────────────────
        public ApiClient Api => _api;
    }

    // ─── Helper: run callbacks on Unity main thread ──────────
    public static class MainThread
    {
        private static readonly Queue<Action> _queue = new();
        private static readonly object _lock = new();

        public static void Enqueue(Action action)
        {
            lock (_lock) _queue.Enqueue(action);
        }

        // Call this from a MonoBehaviour.Update()
        public static void Tick()
        {
            lock (_lock)
            {
                while (_queue.Count > 0)
                    _queue.Dequeue()?.Invoke();
            }
        }
    }

    // ─── Serialization helpers ───────────────────────────────
    public class LoginResponseData
    {
        public string AccessToken  { get; set; }
        public string RefreshToken { get; set; }
    }
    public class RefreshResponseData
    {
        public string AccessToken { get; set; }
    }
}
