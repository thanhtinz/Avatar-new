// client/UnityClient/Scripts/UI/ChatUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FantasyWorld.Client.Network;
using FantasyWorld.Client.Game;
using FantasyWorld.Shared.DTOs;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Client.UI
{
    /// <summary>Chat box UI — supports 7 channels with colour coding</summary>
    public class ChatUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private ScrollRect   chatScroll;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button       sendButton;
        [SerializeField] private TMP_Dropdown channelDropdown;
        [SerializeField] private Transform    messageContainer;
        [SerializeField] private GameObject   messagePrefab;
        [SerializeField] private int          maxMessages = 100;

        // Channel colour map
        private static readonly Dictionary<ChatChannel, Color> ChannelColors = new()
        {
            [ChatChannel.Map]     = Color.white,
            [ChatChannel.World]   = new Color(1f, 0.8f, 0f),          // gold
            [ChatChannel.Clan]    = new Color(0.2f, 0.8f, 0.2f),      // green
            [ChatChannel.Party]   = new Color(0.4f, 0.7f, 1f),        // cyan
            [ChatChannel.Academy] = new Color(0.8f, 0.4f, 1f),        // purple
            [ChatChannel.Faction] = new Color(1f, 0.4f, 0.2f),        // orange
            [ChatChannel.Private] = new Color(1f, 0.6f, 0.8f),        // pink
        };

        private readonly Queue<GameObject> _messagePool = new();
        private ChatChannel _currentChannel = ChatChannel.Map;

        private void Start()
        {
            sendButton.onClick.AddListener(OnSend);
            inputField.onSubmit.AddListener(_ => OnSend());
            channelDropdown.onValueChanged.AddListener(OnChannelChange);

            if (NetworkManager.Instance != null)
                NetworkManager.Instance.OnChatReceived += AddMessage;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.OnChatReceived -= AddMessage;
        }

        private void OnSend()
        {
            var text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _ = GameManager.Instance.SendChatAsync(_currentChannel, text);
            inputField.text = "";
            inputField.ActivateInputField();
        }

        private void OnChannelChange(int idx)
        {
            _currentChannel = (ChatChannel)idx;
        }

        public void AddMessage(ChatReceiveDto data)
        {
            // Colour channel prefix
            ChannelColors.TryGetValue(data.Channel, out var col);
            var hex    = ColorUtility.ToHtmlStringRGB(col == default ? Color.white : col);
            var prefix = $"<color=#{hex}>[{data.Channel}]</color>";
            var name   = $"<b>{data.SenderName}</b>";
            var msg    = $"{prefix} {name}: {data.Content}";

            AddRaw(msg);
        }

        public void AddSystemMessage(string message, Color? colour = null)
        {
            var hex = ColorUtility.ToHtmlStringRGB(colour ?? Color.yellow);
            AddRaw($"<color=#{hex}>⚡ {message}</color>");
        }

        private void AddRaw(string text)
        {
            // Recycle old messages
            while (messageContainer.childCount >= maxMessages)
            {
                var oldest = messageContainer.GetChild(0).gameObject;
                oldest.transform.SetParent(null);
                _messagePool.Enqueue(oldest);
            }

            GameObject obj;
            if (_messagePool.Count > 0) { obj = _messagePool.Dequeue(); obj.transform.SetParent(messageContainer); }
            else obj = Instantiate(messagePrefab, messageContainer);

            obj.GetComponentInChildren<TMP_Text>().text = text;

            // Scroll to bottom
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    // ────────────────────────────────────────────────────────────

    /// <summary>Floating notification toasts (level up, system messages, etc.)</summary>
    public class NotificationUI : MonoBehaviour
    {
        public static NotificationUI Instance { get; private set; }

        [SerializeField] private GameObject toastPrefab;
        [SerializeField] private Transform  toastContainer;
        [SerializeField] private float      displayDuration = 3f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnNotification += ShowToast;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnNotification -= ShowToast;
        }

        public void ShowToast(string title, string message)
        {
            var obj   = Instantiate(toastPrefab, toastContainer);
            var texts = obj.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 2) { texts[0].text = title; texts[1].text = message; }
            Destroy(obj, displayDuration);
        }
    }

    // ────────────────────────────────────────────────────────────

    /// <summary>HUD — gold, HP, time, mini map indicator</summary>
    public class HudUI : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Slider   hpBar;
        [SerializeField] private TMP_Text mpText;
        [SerializeField] private Slider   mpBar;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Slider   expBar;

        [Header("Life Stats")]
        [SerializeField] private Slider hungerBar;
        [SerializeField] private Slider energyBar;
        [SerializeField] private Slider moodBar;

        [Header("World")]
        [SerializeField] private TMP_Text  timeText;
        [SerializeField] private GameObject sunIcon;
        [SerializeField] private GameObject moonIcon;
        [SerializeField] private TMP_Text  seasonText;

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnHpChanged       += UpdateHp;
                NetworkManager.Instance.OnGoldChanged     += UpdateGold;
                NetworkManager.Instance.OnLevelUp         += UpdateLevel;
                NetworkManager.Instance.OnLifeStatsChanged += UpdateLifeStats;
                NetworkManager.Instance.OnWorldTimeUpdated += UpdateTime;
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.OnHpChanged       -= UpdateHp;
            NetworkManager.Instance.OnGoldChanged     -= UpdateGold;
            NetworkManager.Instance.OnLevelUp         -= UpdateLevel;
            NetworkManager.Instance.OnLifeStatsChanged -= UpdateLifeStats;
            NetworkManager.Instance.OnWorldTimeUpdated -= UpdateTime;
        }

        private void RefreshAll()
        {
            var c = GameManager.Instance?.MyCharacter;
            if (c == null) return;

            goldText.text  = $"💰 {c.Gold:N0}";
            levelText.text = $"Lv.{c.Level}";

            UpdateHp(c.Hp, c.HpMax);
        }

        private void UpdateHp(int hp, int hpMax)
        {
            hpText.text = $"{hp}/{hpMax}";
            if (hpBar != null) hpBar.value = hpMax > 0 ? (float)hp / hpMax : 0;
        }

        private void UpdateGold(long gold, long _)
            => goldText.text = $"💰 {gold:N0}";

        private void UpdateLevel(int lv, string vi, string en)
            => levelText.text = $"Lv.{lv}";

        private void UpdateLifeStats(byte hunger, byte energy, byte mood)
        {
            if (hungerBar) hungerBar.value = hunger / 100f;
            if (energyBar) energyBar.value = energy / 100f;
            if (moodBar)   moodBar.value   = mood   / 100f;
        }

        private void UpdateTime(WorldTimeDto wt)
        {
            timeText.text  = $"{wt.GameHour:D2}:{wt.GameMinute:D2}";
            sunIcon?.SetActive(wt.IsDay);
            moonIcon?.SetActive(!wt.IsDay);

            var seasonNames = new Dictionary<SeasonCode, string>
            {
                [SeasonCode.Spring] = "🌸",
                [SeasonCode.Summer] = "☀️",
                [SeasonCode.Autumn] = "🍂",
                [SeasonCode.Winter] = "❄️",
            };
            if (seasonText != null && seasonNames.TryGetValue(wt.Season, out var icon))
                seasonText.text = icon;
        }
    }

    // ────────────────────────────────────────────────────────────

    /// <summary>Login / Register screen controller</summary>
    public class LoginUI : MonoBehaviour
    {
        [Header("Login Panel")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button         loginButton;
        [SerializeField] private Button         registerButton;
        [SerializeField] private TMP_Text       errorText;
        [SerializeField] private GameObject     loadingPanel;

        [Header("Language")]
        [SerializeField] private Button viBtn;
        [SerializeField] private Button enBtn;

        private void Start()
        {
            loginButton.onClick.AddListener(OnLogin);
            registerButton.onClick.AddListener(OnRegister);
            viBtn?.onClick.AddListener(() => SetLang("vi"));
            enBtn?.onClick.AddListener(() => SetLang("en"));

            // Try auto-login from saved token
            _ = TryAutoLogin();
        }

        private async System.Threading.Tasks.Task TryAutoLogin()
        {
            loadingPanel.SetActive(true);
            var ok = await NetworkManager.Instance.RestoreSessionAsync();
            loadingPanel.SetActive(false);

            if (ok) LoadCharacterSelect();
        }

        private async void OnLogin()
        {
            var user = usernameInput.text.Trim();
            var pass = passwordInput.text;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                ShowError("Vui lòng nhập đầy đủ thông tin");
                return;
            }

            SetLoading(true);
            var ok = await NetworkManager.Instance.LoginAsync(user, pass);
            SetLoading(false);

            if (ok) LoadCharacterSelect();
            else    ShowError("Sai tên đăng nhập hoặc mật khẩu");
        }

        private async void OnRegister()
        {
            var user = usernameInput.text.Trim();
            var pass = passwordInput.text;

            SetLoading(true);
            var resp = await NetworkManager.Instance.RegisterAsync(user, pass);
            SetLoading(false);

            ShowError(resp.Success ? "✅ Đăng ký thành công! Hãy đăng nhập." : resp.Message);
        }

        private void SetLang(string lang)
        {
            NetworkManager.Instance.Api.SetLanguage(lang);
            PlayerPrefs.SetString("lang", lang);
        }

        private void SetLoading(bool on)
        {
            loadingPanel.SetActive(on);
            loginButton.interactable    = !on;
            registerButton.interactable = !on;
        }

        private void ShowError(string msg) => errorText.text = msg;

        private void LoadCharacterSelect()
        {
            UnityEngine.SceneManagement.SceneManager
                .LoadScene("CharacterSelect");
        }
    }
}
