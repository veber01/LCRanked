using System.Collections;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using System;
using LCRanked.UI;
using Steamworks;


namespace LCRanked
{
    public enum QueueModeSelection { Solo, Duo }
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.happyness.LCRanked";
        public const string PluginName = "LC Ranked";
        public const string PluginVersion = "0.3.952";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        public NetworkClient Network;

        public static int collected;
        public QuickMenuManager qmm;
        public MatchState CurrentMatch = new MatchState();
        public MatchRunner Runner;
        internal DebugUI DebugUi { get; set; }


        public string LocalPlayerId = SystemInfo.deviceUniqueIdentifier;  //remove comment before shipping this is only for testing stuff
        //public string LocalPlayerId { get; } = Guid.NewGuid().ToString();
        public string LocalPlayerName = "Player";


        private Harmony _harmony;
        public bool? _pendingQueueToggle;
        public bool _inQueue;
        public bool _initialized;
        public static bool track = false;
        public bool IsInQueue => _inQueue;
        public int QueuePlayerCount;
        public int PlayersInGameCount;
        public QueueModeSelection SelectedQueueMode = QueueModeSelection.Solo;
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

        public PlayerStats LocalStats;
        public bool HasStatsRecord;

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
                if (ES3.FileExists("LCSaveFile99"))
                {
                    ES3.CopyFile("LCSaveFile99", "LCSaveFile98");
                    Logger.LogInfo("Extremely edge case: Renamed lc99 to lc98 before restore.");
                }
                if (ES3.FileExists(saveFile))
                {
                    ES3.CopyFile(saveFile, "LCSaveFile99");
                    Logger.LogInfo("Renamed what is supposed to be a backup file to LCSaveFile99 (safety measure, instead of deletion).");
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
                Network = new NetworkClient("https://lcrankedserver-production.up.railway.app", Log);
                Network.SetPlayerId(LocalPlayerId);
                Network.Connected += Queue.HandleNetworkConnected;
                _ = Network.ConnectAsync();
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(Plugin).Assembly);
                Runner = gameObject.AddComponent<MatchRunner>();
                EnsureDebugUI();

                _initialized = true;
                Log.LogInfo("[LCRanked] Plugin initialization complete.");
            }
            catch (System.Exception ex)
            {
                Log.LogError($"[LCRanked] Plugin initialization failed: {ex.Message}");
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
                    LocalPlayerId = Steamworks.SteamClient.SteamId.ToString(); //remove comment before shipping this is only for testing stuff
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
            MyProfileMenuLink.Create();
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
                Queue.HandleServerMessage(msg);
            }
            Queue.FlushPendingQueueToggle();

        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Network?.Connected -= Queue.HandleNetworkConnected;
            Network?.Dispose();
        }
    }

    public class DebugUI : MonoBehaviour
    {
        public static DebugUI Instance;
        public static bool _menuOpen;
        public Coroutine _queueStatusPoller;
        public bool _pollingActive;

        public Rect _windowRect = new Rect(700, 20, 500, 350);
        public Plugin _plugin;

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
                Queue.StartQueueStatusPolling();
            }
            else if (!value && ui != null)
            {
                Queue.StopQueueStatusPolling();
            }
        }
        public void HideMenu()
        {
            _menuOpen = false;
            Queue.StopQueueStatusPolling();
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
            if (Plugin.track == false && !plugin.IsInQueue)
            {
                GUILayout.BeginHorizontal();
                bool soloSelected = GUILayout.Toggle(plugin.SelectedQueueMode == QueueModeSelection.Solo, "Solo");
                bool duoSelected = GUILayout.Toggle(plugin.SelectedQueueMode == QueueModeSelection.Duo, "Duo");
                GUILayout.EndHorizontal();

                if (soloSelected && plugin.SelectedQueueMode != QueueModeSelection.Solo)
                {
                    plugin.SelectedQueueMode = QueueModeSelection.Solo;
                }
                else if (duoSelected && plugin.SelectedQueueMode != QueueModeSelection.Duo)
                {
                    plugin.SelectedQueueMode = QueueModeSelection.Duo;
                }
            }
            else
            {
                GUILayout.Label($"Mode: {plugin.SelectedQueueMode}");
            }

            GUILayout.Space(6f);
            if (Plugin.track == false)
            {
                if (GUILayout.Button(plugin.IsInQueue ? "Leave queue" : "Join queue"))
                {
                    Queue.ToggleQueueFromMenu();
                    Plugin.Log.LogError("Button pressed");
                }
            }
            GUILayout.EndVertical();
            GUILayout.Space(20f);
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
    }
    public class QuickmenuPatches
    {
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
    }
    public class Display
    {
        public static void DisplayTip(string title, string msg, bool isWarning = false)
        {
            HUDManager.Instance.DisplayTip(title, msg, isWarning, false, "LC_Tip1");
        }


    }
}


