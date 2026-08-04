using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using System;
using LCRanked.UI;
using System.Security.Cryptography;
using System.Text;
using Steamworks;
using Steamworks.Data;


namespace LCRanked
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.happyness.LCRanked";
        public const string PluginName = "LC Ranked";
        public const string PluginVersion = "0.2.88";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        public NetworkClient Network;

        public static int collected;
        public QuickMenuManager qmm;
        public MatchState CurrentMatch = new MatchState();
        public MatchRunner Runner;
        internal DebugUI DebugUi { get; set; }


        //public string LocalPlayerId = SystemInfo.deviceUniqueIdentifier;  //remove comment before shipping this is only for testing stuff
        public string LocalPlayerId { get; } = Guid.NewGuid().ToString();
        public string LocalPlayerName = "Player";


        private Harmony _harmony;
        private bool? _pendingQueueToggle;
        private bool _inQueue;
        private bool _initialized;
        public static bool track = false;
        public bool IsInQueue => _inQueue;
        public int QueuePlayerCount { get; private set; }
        public int PlayersInGameCount { get; private set; }
        public class PlayerStats
        {
            public string playerId;
            public string playerName;
            public int wins;
            public int losses;
            public int matchesPlayed;
            public int rating = -1;
            public double winRate;
            public int bestCollectedValue;
            public int avgCollectedValue;
            public double survivalRate;
            public int currentStreak;
            public int bestWinStreak;
            public int rankedPlaytimeSeconds;
            public int? leaderboardRank;
            public int leaderboardTotal;
        }

        public PlayerStats LocalStats { get; private set; }
        public bool HasStatsRecord { get; private set; }

        public async void RequestPlayerStats()
        {
            if (Network == null || !Network.IsConnected)
            {
                return;
            }
            await Network.RequestPlayerStatsAsync(LocalPlayerId);
        }


        private void Awake()
        {
            RestoreBackupSave();
            Instance = this;
            Log = Logger;
            SceneManager.sceneLoaded += StaticOnSceneLoaded;
            DontDestroyOnLoad(gameObject);
            InitializePlugin();

        }
        private void RestoreBackupSave()
        {
            string backupFile = "Save1Temp";
            string saveFile = "LCSaveFile1";

            if (!ES3.FileExists(backupFile))
            {
                Logger.LogInfo("No backup found.");
                return;
            }

            Logger.LogWarning("Backup save detected! Restoring original save...");

            try
            {
                if (ES3.FileExists(saveFile))
                {
                    ES3.CopyFile(saveFile, "LCSaveFile99");
                    Logger.LogInfo("Renamed what is supposed to be a ranked file (safety measure, instead of deletion).");
                }

                ES3.CopyFile(backupFile, saveFile);

                ES3.DeleteFile(backupFile);

                Logger.LogInfo("Save restored.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed restoring save: {ex}");
            }
        }


        private static void StaticOnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance == null)
            {
                var go = new GameObject("LCRankedRecovered");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<Plugin>();
                return;
            }

            Instance.OnSceneLoaded(scene, mode);
        }
        private void InitializePlugin()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                Network = new NetworkClient("https://discordbot-production-7184.up.railway.app", Log);
                Network.SetPlayerId(LocalPlayerId);
                Network.Connected += HandleNetworkConnected;
                _ = Network.ConnectAsync();
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(Plugin).Assembly);
                Runner = gameObject.AddComponent<MatchRunner>();
                EnsureDebugUI();

                _initialized = true;
                Log?.LogInfo("[LCRanked] Plugin initialization complete.");
            }
            catch (System.Exception ex)
            {
                Log?.LogError($"[LCRanked] Plugin initialization failed: {ex.Message}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu" || scene.name == "InitScene" || scene.name == "InitSceneLaunchOptions")
            {
                if (DebugUI.Instance != null)
                {
                    DebugUI.Instance.HideMenu();
                }
                if (scene.name == "MainMenu")
                {
                    RequestPlayerStats();
                    StartCoroutine(CreateLeaderboardLinkNextFrame());
                    //LocalPlayerId = Steamworks.SteamClient.SteamId.ToString();
                }
                return;
            }
            LocalPlayerName = GameNetworkManager.Instance.username;
            EnsureDebugUI();
        }

        private IEnumerator CreateLeaderboardLinkNextFrame()
        {
            yield return null;
            LeaderboardMenuLink.Create();
        }


        private void EnsureDebugUI()
        {
            if (DebugUi != null)
            {
                DebugUi.SetPlugin(this);
                return;
            }

            if (DebugUI.Instance != null)
            {
                DebugUi = DebugUI.Instance;
                DebugUi.SetPlugin(this);
                return;
            }
            GameObject ui = new GameObject("LCRankedQueueMenu");
            DontDestroyOnLoad(ui);
            var debugUi = ui.AddComponent<DebugUI>();
            DebugUi = debugUi;
            debugUi.SetPlugin(this);
        }

        private void Update()
        {
            while (Network != null && Network.IncomingMessages.TryDequeue(out var msg))
            {
                HandleServerMessage(msg);
            }
            FlushPendingQueueToggle();

        }

        public static void ToggleQueueFromMenu()
        {
            Plugin.Instance.ToggleQueue();

            Plugin.Instance.RequestQueueStatus();

        }

        public async void RequestQueueStatus()
        {
            if (Network == null)
            {
                return;
            }

            if (!Network.IsConnected)
            {
                await Network.ConnectAsync();
                return;
            }

            await Network.SendAsync(new { type = "queue_status", playerId = LocalPlayerId });
        }

        private void ToggleQueue()
        {
            if (Network == null)
            {
                return;
            }

            if (!Network.IsConnected)
            {
                if (Network.IsConnecting)
                {
                    _pendingQueueToggle = !_inQueue;
                    return;
                }

                _pendingQueueToggle = !_inQueue;
                _ = Network.ConnectAsync();
                return;
            }
            ApplyQueueToggle(!_inQueue);
        }

        private void HandleNetworkConnected()
        {
            Log.LogInfo("[LCRanked] Network connected; processing pending queue action.");
            StartCoroutine(DelayedQueueFlush());
            RequestPlayerStats();
        }

        private IEnumerator DelayedQueueFlush()
        {
            yield return new WaitForSeconds(0.25f);
            FlushPendingQueueToggle();
        }

        private void FlushPendingQueueToggle()
        {
            if (Network == null || !_pendingQueueToggle.HasValue)
            {
                return;
            }
            if (!Network.IsConnected)
            {
                return;
            }

            var pending = _pendingQueueToggle.Value;
            _pendingQueueToggle = null;
            ApplyQueueToggle(pending);
        }

        private void ApplyQueueToggle(bool joinQueue)
        {
            if (joinQueue)
            {
                Network.JoinQueue(LocalPlayerId, LocalPlayerName, LocalPlayerId);
                _inQueue = true;
                Log.LogInfo("[LCRanked] Sent join_queue request.");
            }
            else
            {
                Network.LeaveQueue(LocalPlayerId);
                _inQueue = false;
                Log.LogInfo("[LCRanked] Sent leave_queue request.");
            }
        }

        private void HandleServerMessage(JObject msg)
        {

            string type = msg["type"]?.ToString();
            switch (type)
            {
                case "queue_joined":
                    _inQueue = true;
                    QueuePlayerCount = msg["queueSize"]?.ToObject<int>() ?? QueuePlayerCount;
                    break;

                case "queue_left":
                    _inQueue = false;
                    QueuePlayerCount = msg["queueSize"]?.ToObject<int>() ?? QueuePlayerCount;
                    break;

                case "queue_status":
                    QueuePlayerCount = msg["queueSize"]?.ToObject<int>() ?? QueuePlayerCount;
                    PlayersInGameCount = msg["playersInGame"]?.ToObject<int>() ?? PlayersInGameCount;
                    _inQueue = msg["inQueue"]?.ToObject<bool>() ?? _inQueue;
                    break;

                case "match_found":
                    _inQueue = false;
                    OnMatchFound(msg);
                    track = true;
                    DebugUI.SetMenuForAll(false);
                    break;

                case "match_start":
                    OnMatchStart(msg);
                    track = true;
                    DebugUI.SetMenuForAll(false);
                    break;

                case "opponent_result_in":
                    Log.LogInfo("[LCRanked] Opponent has finished their run.");
                    RankedHUD.Instance.SetOpponent("Finished their run!");
                    break;

                case "match_result":
                    OnMatchResult(msg);
                    track = false;
                    RankedHUD.Remove();
                    HUDManager.Instance.HideHUD(hide: false);
                    StartOfRound.Instance.displayedLevelResults = false;

                    break;

                case "match_aborted": // make match abortable : it only accounts for if a player is in a match so change me later please happy i beg
                    DebugUI.DisplayTip("LC Ranked", "Match aborted!");
                    Log.LogWarning($"[LCRanked] Match aborted: {msg["reason"]}");
                    CurrentMatch.Reset();
                    track = false;
                    OnMatchAborted(msg);
                    RankedHUD.Remove();
                    break;

                case "error":
                    Log.LogError($"[LCRanked] Server error: {msg["message"]}");
                    break;

                default:
                    Log.LogWarning($"[LCRanked] Unknown message type from server: {type}");
                    break;

                case "player_stats":
                    if (msg["playerId"]?.ToString() == LocalPlayerId)
                    {
                        bool noRecord = msg["noRecord"]?.ToObject<bool>() ?? false;
                        HasStatsRecord = !noRecord;
                        LocalStats = noRecord ? null : msg.ToObject<PlayerStats>();

                        bool nameSet = !noRecord && (msg["nameSet"]?.ToObject<bool>() ?? false);
                        if (!nameSet)
                        {
                            NamePromptUI.Create();
                        }
                    }
                    break;

                case "set_display_name_result":
                    NamePromptUI.Instance?.HandleResult(
                        msg["success"]?.ToObject<bool>() ?? false,
                        msg["error"]?.ToString());
                    break;

                case "leaderboard_page":
                    var entries = msg["entries"]?.ToObject<LeaderboardWindowUI.JsonEntry[]>() ?? new LeaderboardWindowUI.JsonEntry[0];
                    int page = msg["page"]?.ToObject<int>() ?? 1;
                    int totalPages = msg["totalPages"]?.ToObject<int>() ?? 1;
                    LeaderboardWindowUI.Instance?.HandlePageResult(page, totalPages, entries);
                    break;
            }
        }

        private void HandleNamePromptCheck(JObject msg)
        {
            if (msg["playerId"]?.ToString() != LocalPlayerId) return;
            bool noRecord = msg["noRecord"]?.ToObject<bool>() ?? false;
            bool nameSet = !noRecord && (msg["nameSet"]?.ToObject<bool>() ?? false);
            if (!nameSet) NamePromptUI.Create();
        }

        private void OnMatchAborted(JObject msg)
        {
            RankedHUD.Remove();
        }

        private void OnMatchFound(JObject msg)
        {
            DebugUI.DisplayTip("LC Ranked", "Match Found!\nMatch Starting in 15 seconds!");
            CurrentMatch.Reset();
            CurrentMatch.matchId = msg["matchId"]?.ToString();
            CurrentMatch.mode = msg["mode"]?.ToString();
            CurrentMatch.ruleset = msg["ruleset"]?.ToObject<Ruleset>();
            CurrentMatch.rulesetJson = msg["ruleset"]?.ToString();
            CurrentMatch.ruleset.weatherMS = msg["ruleset"]?["weather"]?.ToString();
            TimeOfDay.Instance.currentLevelWeather = LevelWeatherType.None;

            foreach (var p in msg["participants"])
            {
                CurrentMatch.participants.Add(p.ToObject<ParticipantInfo>());
            }

            Log.LogInfo($"[LCRanked] Match found! Moon={CurrentMatch.ruleset.moon} Seed={CurrentMatch.ruleset.seed} Weather={CurrentMatch.ruleset.weatherMS}  " +
                        $"Opponent(s)={string.Join(", ", CurrentMatch.participants.ConvertAll(p => p.playerName))}");
        }

        private void OnMatchStart(JObject msg)
        {
            DebugUI.DisplayTip("LC Ranked", "Match starts now!");
            CurrentMatch.startTimestampMs = msg["startTimestamp"]?.ToObject<long>() ?? 0;
            Log.LogInfo("[LCRanked] Match starting now.");
            Runner.BeginMatch(CurrentMatch);
            RankedHUD.Create();
            string holder = CurrentMatch.ruleset.moon.ToString();
            holder.Replace("Level", string.Empty);
            RankedHUD.Instance.SetMoon(holder);
            RankedHUD.Instance.SetSeed(CurrentMatch.ruleset.seed);
            RankedHUD.Instance.SetMatchId(CurrentMatch.matchId);
            RankedHUD.Instance.SetWeather(CurrentMatch.ruleset.weatherMS);
            if (LocalStats.rating == -1)
            {
                RankedHUD.Instance.mmrText.text = "Unranked";
            }
            else
            {
                RankedHUD.Instance.SetMMR(LocalStats.rating);
            }
        }

        private void OnMatchResult(JObject msg)
        {
            Log.LogInfo($"[LCRanked] Match result: winner={msg["winnerName"]}");
            DebugUI.DisplayTip("LC Ranked", $"Match finished! Winner: {msg["winnerName"]}");
            foreach (var placement in msg["placements"])
            {
                Log.LogInfo($"  #{placement["placement"]} {placement["playerName"]}");
            }
            CurrentMatch.Reset();
            RankedHUD.Remove();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Network?.Connected -= HandleNetworkConnected;
            Network?.Dispose();
        }
    }

    public class DebugUI : MonoBehaviour
    {
        public static DebugUI Instance;
        private static bool _menuOpen;
        private Coroutine _queueStatusPoller;
        private bool _pollingActive;

        private Rect _windowRect = new Rect(700, 20, 500, 350);
        private Plugin _plugin;

        public static void SetMenuForAll(bool value)
        {
            if (GameNetworkManager.Instance != null && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
            {
                _menuOpen = false;
                return;
            }

            var plugin = ResolvePluginInstance();
            var ui = plugin != null ? plugin.DebugUi : null;
            if (ui == null)
            {
                if (Instance == null)
                {
                    var obj = new GameObject("LCRankedQueueMenu");
                    DontDestroyOnLoad(obj);
                    Instance = obj.AddComponent<DebugUI>();
                }

                ui = Instance;
            }

            if (plugin != null)
            {
                plugin.DebugUi = ui;
                ui.SetPlugin(plugin);
            }

            _menuOpen = value;
            if (value && ui != null)
            {
                ui.StartQueueStatusPolling();
            }
            else if (!value && ui != null)
            {
                ui.StopQueueStatusPolling();
            }
        }
        public void HideMenu()
        {
            _menuOpen = false;
            StopQueueStatusPolling();
        }

        private void Awake()
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            var plugin = ResolvePluginInstance();
            if (plugin != null)
            {
                SetPlugin(plugin);
            }
            enabled = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (Plugin.Instance != null && Plugin.Instance.DebugUi == this)
            {
                Plugin.Instance.DebugUi = null;
            }
        }

        public void SetPlugin(Plugin plugin)
        {
            _plugin = plugin;
            if (plugin != null)
            {
                Plugin.Instance ??= plugin;
                plugin.DebugUi = this;
            }
        }

        private void StartQueueStatusPolling()
        {
            if (_pollingActive)
            {
                return;
            }

            StopQueueStatusPolling();
            _pollingActive = true;
            _queueStatusPoller = StartCoroutine(PollQueueStatusCoroutine());
        }

        private void StopQueueStatusPolling()
        {
            if (_queueStatusPoller != null)
            {
                StopCoroutine(_queueStatusPoller);
                _queueStatusPoller = null;
            }

            _pollingActive = false;
        }

        private IEnumerator PollQueueStatusCoroutine()
        {
            if (_plugin != null && _plugin.Network != null)
            {
                _plugin.RequestQueueStatus();
                _plugin.RequestPlayerStats();
            }

            yield return new WaitForSeconds(15f);

            while (_menuOpen)
            {
                if (_plugin != null && _plugin.Network != null && _plugin.Network.IsConnected)
                {
                    _plugin.RequestQueueStatus();
                    _plugin.RequestPlayerStats();
                }

                yield return new WaitForSeconds(30f);
            }
        }

        private static Plugin ResolvePluginInstance()
        {
            if (Plugin.Instance != null)
            {
                return Plugin.Instance;
            }

            var resolved = UnityEngine.Object.FindObjectOfType<Plugin>();
            if (resolved != null)
            {
                Plugin.Instance = resolved;
            }

            return resolved;
        }

        private void OnGUI()
        {
            if (!_menuOpen) return;

            _windowRect = GUILayout.Window(
                0,
                _windowRect,
                DrawWindow,
                "LC Ranked"
            );
        }

        private void DrawWindow(int windowID)
        {
            var plugin = _plugin != null ? _plugin : ResolvePluginInstance();
            if (plugin == null)
            {
                GUILayout.Label("Initializing queue UI...");
                GUI.DragWindow();
                return;
            }

            if (_plugin != plugin)
            {
                SetPlugin(plugin);
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(180));
            GUILayout.Label($"In queue: {plugin.IsInQueue}");
            GUILayout.Label($"Players in queue: {plugin.QueuePlayerCount}");
            GUILayout.Label($"Players in game: {plugin.PlayersInGameCount}");
            GUILayout.Space(6f);
            if (Plugin.track == false)
            {
                if (GUILayout.Button(plugin.IsInQueue ? "Leave queue" : "Join queue"))
                {
                    Plugin.ToggleQueueFromMenu();
                }
            }
            else
            {
                GUILayout.Label("Match in progress!");
            }
            GUILayout.EndVertical();

            GUILayout.Space(20f);

            GUILayout.BeginVertical();
            GUILayout.Label("Stats", GUI_HeaderStyle());

            if (!plugin.HasStatsRecord)
            {
                GUILayout.Label("Unranked - finish a ranked match to get rated.");
            }
            else
            {
                var s = plugin.LocalStats;
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label($"Name: {s.playerName}");
                GUILayout.Label($"Leaderboard # {(s.leaderboardRank?.ToString() ?? "-")} / {s.leaderboardTotal}");
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                GUILayout.Label($"Elo: {s.rating}");
                GUILayout.Label($"Wins: {s.wins}");
                GUILayout.Label($"Losses: {s.losses}");
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.Space(6f);
                GUILayout.Label($"Win rate: {s.winRate:P0}");
                GUILayout.Label($"Best collected: {s.bestCollectedValue}");
                GUILayout.Label($"Avg collected: {s.avgCollectedValue}");
                GUILayout.Label($"Survival rate: {s.survivalRate:P0}");
                GUILayout.Label($"Streak: {(s.currentStreak >= 0 ? "W" + s.currentStreak : "L" + (-s.currentStreak))}");
                GUILayout.Label($"Playtime: {s.rankedPlaytimeSeconds / 3600f:F1} hrs");
                GUILayout.Space(6f);
                GUILayout.Label($"ID: {s.playerId}");
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }


        private GUIStyle _headerStyle;
        private GUIStyle GUI_HeaderStyle()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };
            }
            return _headerStyle;
        }

        [HarmonyPatch(typeof(QuickMenuManager))]
        public class QuickMenuPatch
        {
            [HarmonyPatch(nameof(QuickMenuManager.OpenQuickMenu))]
            [HarmonyPostfix]
            public static void OnOpenQuickMenu()
            {
                if (Plugin.track == false)
                {
                    DebugUI.SetMenuForAll(true);
                }

            }

            [HarmonyPatch(nameof(QuickMenuManager.CloseQuickMenu))]
            [HarmonyPostfix]
            public static void OnCloseQuickMenu()
            {
                DebugUI.SetMenuForAll(false);
            }
        }

        public static void DisplayTip(string title, string msg, bool isWarning = false)
        {
            HUDManager.Instance.DisplayTip(title, msg, isWarning, false, "LC_Tip1");
        }


    }


}
