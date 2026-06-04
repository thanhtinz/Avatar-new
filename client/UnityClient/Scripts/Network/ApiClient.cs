// client/UnityClient/Scripts/Network/ApiClient.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace FantasyWorld.Client.Network
{
    /// <summary>
    /// HTTP client cho tất cả REST API calls
    /// Tự động retry 1 lần khi token expired (refresh + retry)
    /// </summary>
    public class ApiClient
    {
        private readonly HttpClient _http;
        private readonly string     _baseUrl;
        private string              _lang = "vi";

        public ApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http    = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            _http.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.Add("X-Client-Type", "unity");
        }

        public void SetToken(string token)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public void SetLanguage(string lang)
        {
            _lang = lang;
            _http.DefaultRequestHeaders.Remove("Accept-Language");
            _http.DefaultRequestHeaders.Add("Accept-Language", lang);
        }

        // ─── GET ─────────────────────────────────────────────
        public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/{endpoint}");
                return await ParseAsync<T>(resp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] GET {endpoint}: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
        }

        // ─── POST ────────────────────────────────────────────
        public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object body)
        {
            try
            {
                var json    = body != null ? JsonConvert.SerializeObject(body) : "{}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp    = await _http.PostAsync($"{_baseUrl}/{endpoint}", content);
                return await ParseAsync<T>(resp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] POST {endpoint}: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
        }

        // ─── PATCH ───────────────────────────────────────────
        public async Task<ApiResponse<T>> PatchAsync<T>(string endpoint, object body = null)
        {
            try
            {
                var json    = body != null ? JsonConvert.SerializeObject(body) : "{}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var req     = new HttpRequestMessage(new HttpMethod("PATCH"),
                    $"{_baseUrl}/{endpoint}") { Content = content };
                var resp    = await _http.SendAsync(req);
                return await ParseAsync<T>(resp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] PATCH {endpoint}: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
        }

        // ─── DELETE ──────────────────────────────────────────
        public async Task<ApiResponse<T>> DeleteAsync<T>(string endpoint)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{_baseUrl}/{endpoint}");
                return await ParseAsync<T>(resp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] DELETE {endpoint}: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
        }

        // ─── Parse ───────────────────────────────────────────
        private static async Task<ApiResponse<T>> ParseAsync<T>(HttpResponseMessage resp)
        {
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                var err = TryDeserialize<ApiResponse<T>>(body);
                return err ?? ApiResponse<T>.Fail($"HTTP {(int)resp.StatusCode}");
            }

            var result = TryDeserialize<ApiResponse<T>>(body);
            return result ?? ApiResponse<T>.Fail("Parse error");
        }

        private static T TryDeserialize<T>(string json)
        {
            try { return JsonConvert.DeserializeObject<T>(json); }
            catch { return default; }
        }
    }

    // ─── Generic API response wrapper ────────────────────────
    public class ApiResponse<T>
    {
        [JsonProperty("success")] public bool    Success { get; set; }
        [JsonProperty("message")] public string  Message { get; set; }
        [JsonProperty("data")]    public T       Data    { get; set; }

        public static ApiResponse<T> Fail(string msg) =>
            new() { Success = false, Message = msg };
    }

    // ─── Typed response helpers ──────────────────────────────
    public static class ApiEndpoints
    {
        // Auth
        public const string Login            = "auth/login";
        public const string Register         = "auth/register";
        public const string Logout           = "auth/logout";
        public const string Refresh          = "auth/refresh";
        public const string Me               = "auth/me";

        // Character
        public const string Characters       = "characters";
        public static string Character(long id)      => $"characters/{id}";
        public static string CharInventory(long id)  => $"characters/{id}/inventory";
        public static string CitizenCard(long id)    => $"characters/{id}/citizen-card";

        // Shop
        public static string NpcShop(int shopId)     => $"shops/npc/{shopId}";
        public const string  NpcBuy                  = "shops/npc/buy";
        public const string  NpcSell                 = "shops/npc/sell";
        public const string  PlayerShopMy            = "shops/player/my";
        public static string PlayerShopListings(long shopId) => $"shops/player/{shopId}/listings";
        public const string  PlayerShopListing       = "shops/player/listing";

        // Auction
        public const string Auction                  = "auction";
        public static string AuctionById(long id)    => $"auction/{id}";
        public static string AuctionBid(long id)     => $"auction/{id}/bid";
        public static string AuctionBuyout(long id)  => $"auction/{id}/buyout";

        // Market
        public const string Market                   = "market";
        public static string MarketItem(int id)      => $"market/{id}";
        public static string MarketHistory(int id)   => $"market/{id}/history";

        // Pets
        public static string WildPets(int mapId)     => $"pets/wild/{mapId}";
        public const string  PetCatch                = "pets/catch";
        public const string  MyPets                  = "pets/my";
        public static string PetActive(long id)      => $"pets/{id}/active";
        public static string PetFeed(long id)        => $"pets/{id}/feed";

        // Fishing
        public static string FishCast(int mapId)     => $"fishing/cast/{mapId}";
        public const string  FishRecord              = "fishing/record";

        // Farm
        public const string FarmPlots                = "farm/plots";
        public const string FarmPlant                = "farm/plant";
        public static string FarmWater(long id)      => $"farm/plots/{id}/water";
        public static string FarmHarvest(long id)    => $"farm/plots/{id}/harvest";

        // Quest
        public const string QuestsAvailable          = "quests/available";
        public const string QuestsActive             = "quests/active";
        public const string QuestAccept              = "quests/accept";
        public static string QuestComplete(int id)   => $"quests/{id}/complete";

        // Academy
        public const string Academies                = "academies";
        public static string AcademyEnroll(int id)   => $"academies/{id}/enroll";
        public const string AcademyExams             = "academies/exams";
        public const string AcademySubmitExam        = "academies/exams/submit";

        // Dungeon
        public const string Dungeons                 = "dungeons";
        public const string DungeonEnter             = "dungeons/enter";
        public const string DungeonCombat            = "dungeons/combat";
        public static string DungeonAdvance(long r)  => $"dungeons/runs/{r}/advance";
        public static string DungeonResult(long r)   => $"dungeons/runs/{r}/result";

        // Story
        public const string StoryProgress            = "story/progress";
        public const string StoryChoice              = "story/choice";

        // Fashion
        public const string FashionShop              = "fashion/shop";
        public const string FashionWardrobe          = "fashion/wardrobe";
        public const string FashionOutfit            = "fashion/outfit";
        public const string FashionWear              = "fashion/wear";
        public const string FashionPresets           = "fashion/presets";

        // Casino
        public const string CasinoGames              = "casino/games";
        public const string CasinoPlay               = "casino/play";

        // Performance
        public const string PerfStart                = "performances/start";
        public const string PerfTip                  = "performances/tip";

        // Social
        public const string Friends                  = "friends";
        public const string FriendRequests           = "friends/requests";
        public static string SendFriendRequest(long id) => $"friends/request/{id}";
        public static string AcceptFriend(long id)   => $"friends/request/{id}/accept";
        public const string CreateClan               = "clans";
        public const string CreateParty              = "parties";
    }
}
