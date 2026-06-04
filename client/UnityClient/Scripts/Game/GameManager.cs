// client/UnityClient/Scripts/Game/GameManager.cs
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using FantasyWorld.Client.Network;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Client.Game
{
    /// <summary>
    /// Central game state manager
    /// Bridges NetworkManager events → scene-level game logic
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ─── Current player state ────────────────────────────
        public CharacterFullDto   MyCharacter   { get; private set; }
        public WorldTimeDto       WorldTime     { get; private set; }
        public int                CurrentMapId  { get; private set; }
        public bool               IsDay         => WorldTime?.IsDay ?? true;
        public SeasonCode         Season        => WorldTime?.Season ?? SeasonCode.Spring;

        // ─── Other players on map ────────────────────────────
        private readonly Dictionary<long, RemotePlayer> _players = new();
        public IReadOnlyDictionary<long, RemotePlayer> Players => _players;

        // ─── Events ──────────────────────────────────────────
        public event System.Action<CharacterFullDto>  OnCharacterLoaded;
        public event System.Action<WorldTimeDto>      OnWorldTimeChanged;
        public event System.Action<RemotePlayer>      OnPlayerSpawned;
        public event System.Action<long>              OnPlayerDespawned;
        public event System.Action<bool>              OnDayNightChanged;
        public event System.Action<SeasonCode>        OnSeasonChanged;
        public event System.Action<int>               OnFullMoon;
        public event System.Action<string, string>    OnNotification;    // (titleVi, msgVi)

        private bool _prevIsDay = true;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (NetworkManager.Instance == null) return;

            // Subscribe to network events
            NetworkManager.Instance.OnGameReady       += HandleGameReady;
            NetworkManager.Instance.OnPlayerMoved     += HandlePlayerMove;
            NetworkManager.Instance.OnPlayerEntered   += HandlePlayerEnter;
            NetworkManager.Instance.OnPlayerLeft      += HandlePlayerLeave;
            NetworkManager.Instance.OnMapChanged      += HandleMapChange;
            NetworkManager.Instance.OnWorldTimeUpdated += HandleWorldTime;
            NetworkManager.Instance.OnSeasonChanged   += HandleSeasonChange;
            NetworkManager.Instance.OnFullMoon        += d => OnFullMoon?.Invoke(d);
            NetworkManager.Instance.OnLevelUp         += HandleLevelUp;
            NetworkManager.Instance.OnGoldChanged     += HandleGoldChange;
            NetworkManager.Instance.OnHpChanged       += HandleHpChange;
            NetworkManager.Instance.OnSystemMessage   += HandleSystemMessage;
            NetworkManager.Instance.OnWorldEvent      += HandleWorldEvent;
            NetworkManager.Instance.OnError           += HandleError;
        }

        private void Update()
        {
            // Tick main thread queue
            MainThread.Tick();

            // Day/night change detection
            if (WorldTime != null && WorldTime.IsDay != _prevIsDay)
            {
                _prevIsDay = WorldTime.IsDay;
                OnDayNightChanged?.Invoke(WorldTime.IsDay);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.OnGameReady       -= HandleGameReady;
            NetworkManager.Instance.OnPlayerMoved     -= HandlePlayerMove;
            NetworkManager.Instance.OnPlayerEntered   -= HandlePlayerEnter;
            NetworkManager.Instance.OnPlayerLeft      -= HandlePlayerLeave;
            NetworkManager.Instance.OnMapChanged      -= HandleMapChange;
            NetworkManager.Instance.OnWorldTimeUpdated -= HandleWorldTime;
            NetworkManager.Instance.OnLevelUp         -= HandleLevelUp;
            NetworkManager.Instance.OnGoldChanged     -= HandleGoldChange;
        }

        // ─── Load character data ─────────────────────────────
        public async Task LoadCharacterAsync(long charId)
        {
            var resp = await NetworkManager.Instance.Api
                .GetAsync<CharacterFullDto>(ApiEndpoints.Character(charId));

            if (resp.Success && resp.Data != null)
            {
                MyCharacter = resp.Data;
                CurrentMapId = MyCharacter.MapId;
                OnCharacterLoaded?.Invoke(MyCharacter);
            }
        }

        // ─── Network event handlers ──────────────────────────

        private void HandleGameReady(GameReadyDto data)
        {
            WorldTime    = data.WorldTime;
            CurrentMapId = data.Character?.MapId ?? 1;
            _prevIsDay   = WorldTime.IsDay;

            // Spawn existing players
            _players.Clear();
            foreach (var p in data.MapPlayers)
                SpawnRemotePlayer(p);

            OnWorldTimeChanged?.Invoke(WorldTime);
        }

        private void HandlePlayerMove(PlayerMoveDto data)
        {
            if (_players.TryGetValue(data.CharId, out var player))
                player.UpdatePosition(data.X, data.Y, data.Dir);
        }

        private void HandlePlayerEnter(PlayerEnterDto data)
        {
            if (!_players.ContainsKey(data.CharId))
                SpawnRemotePlayer(data);
        }

        private void HandlePlayerLeave(long charId, string name)
        {
            if (_players.TryGetValue(charId, out var player))
            {
                _players.Remove(charId);
                OnPlayerDespawned?.Invoke(charId);
                player.Despawn();
            }
        }

        private void HandleMapChange(MapChangedDto data)
        {
            CurrentMapId = data.MapId;
            _players.Clear();
            foreach (var p in data.Players)
                SpawnRemotePlayer(p);
        }

        private void HandleWorldTime(WorldTimeDto data)
        {
            WorldTime = data;
            OnWorldTimeChanged?.Invoke(data);
        }

        private void HandleSeasonChange(SeasonChangeDto data)
        {
            var name = Application.systemLanguage == SystemLanguage.Vietnamese
                ? data.NameVi : data.NameEn;
            OnNotification?.Invoke("🌸", name);
            OnSeasonChanged?.Invoke(data.Season);
        }

        private void HandleLevelUp(int level, string vi, string en)
        {
            if (MyCharacter != null)
                MyCharacter = MyCharacter with { Level = level };

            var msg = NetworkManager.Instance.Language == "vi" ? vi : en;
            OnNotification?.Invoke("⬆️ LEVEL UP!", msg);
        }

        private void HandleGoldChange(long newGold, long delta)
        {
            if (MyCharacter != null)
                MyCharacter = MyCharacter with { Gold = newGold };
        }

        private void HandleHpChange(int hp, int hpMax)
        {
            if (MyCharacter != null)
                MyCharacter = MyCharacter with { Hp = hp, HpMax = hpMax };
        }

        private void HandleSystemMessage(string type, string vi, string en)
        {
            var msg = NetworkManager.Instance.Language == "vi" ? vi : en;
            OnNotification?.Invoke(type, msg);
        }

        private void HandleWorldEvent(string name, string vi, string en)
        {
            var msg = NetworkManager.Instance.Language == "vi" ? vi : en;
            OnNotification?.Invoke($"⚡ {name}", msg);
        }

        private void HandleError(ErrorDto err)
        {
            var msg = NetworkManager.Instance.Language == "vi"
                ? err.MessageVi : err.MessageEn;
            OnNotification?.Invoke("❌", msg);
        }

        // ─── Helpers ─────────────────────────────────────────

        private void SpawnRemotePlayer(PlayerEnterDto data)
        {
            var player = new RemotePlayer(data);
            _players[data.CharId] = player;
            OnPlayerSpawned?.Invoke(player);
        }

        // ─── Action shortcuts ────────────────────────────────

        public Task MoveAsync(float x, float y, MoveDir dir = MoveDir.Down)
            => NetworkManager.Instance.MoveAsync(x, y, CurrentMapId, dir);

        public Task SendChatAsync(ChatChannel ch, string msg, long? target = null)
            => NetworkManager.Instance.SendChatAsync(ch, msg, target);

        public async Task UsePortalAsync(int portalId)
        {
            await NetworkManager.Instance.UsePortalAsync(portalId);
        }
    }

    // ─── Remote player data container ────────────────────────
    public class RemotePlayer
    {
        public long   CharId  { get; }
        public string Name    { get; }
        public int    Level   { get; }
        public float  X       { get; private set; }
        public float  Y       { get; private set; }
        public MoveDir Dir    { get; private set; }
        public Gender Gender  { get; }

        // Reference to Unity GameObject/prefab (set externally)
        public GameObject GameObject { get; set; }

        public RemotePlayer(PlayerEnterDto dto)
        {
            CharId = dto.CharId;
            Name   = dto.Name;
            Level  = dto.Level;
            X      = dto.X;
            Y      = dto.Y;
            Gender = dto.Gender;
        }

        public void UpdatePosition(float x, float y, MoveDir dir)
        {
            X = x; Y = y; Dir = dir;
            // Update GameObject transform if assigned
            if (GameObject != null)
                GameObject.transform.position = new Vector3(x, 0, y);
        }

        public void Despawn()
        {
            if (GameObject != null)
                Object.Destroy(GameObject);
        }
    }
}
