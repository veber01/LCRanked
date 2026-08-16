using System.Collections;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using LCRanked.UI;

namespace LCRanked
{
    public class Queue
    {
        public static Plugin Plugin => Plugin.Instance;
        public static string id11 = null;
        public static string name11 = null;
        public static void ToggleQueueFromMenu()
        {
            ToggleQueue();
            RequestQueueStatus();
        }

        public async static void RequestQueueStatus()
        {
            if (Plugin.Instance.Network == null)
            {
                return;
            }
            if (!Plugin.Network.IsConnected)
            {
                await Plugin.Network.ConnectAsync();
                return;
            }
            await Plugin.Network.SendAsync(new { type = "queue_status", playerId = Plugin.LocalPlayerId });
        }

        public static void ToggleQueue()
        {
            if (Plugin.Network == null)
            {
                return;
            }
            if (!Plugin.Network.IsConnected)
            {
                if (Plugin.Network.IsConnecting)
                {
                    Plugin._pendingQueueToggle = !Plugin._inQueue;
                    return;
                }
                Plugin._pendingQueueToggle = !Plugin._inQueue;
                _ = Plugin.Network.ConnectAsync();
                return;
            }
            ApplyQueueToggle(!Plugin._inQueue);
        }

        public static void HandleNetworkConnected()
        {
            Plugin.Log.LogInfo("[LCRanked] Network connected");
            Plugin.StartCoroutine(DelayedQueueFlush());
            Plugin.RequestPlayerStats();
        }

        public static IEnumerator DelayedQueueFlush()
        {
            yield return new WaitForSeconds(0.25f);
            FlushPendingQueueToggle();
        }

        public static void FlushPendingQueueToggle()
        {
            if (Plugin.Network == null || !Plugin._pendingQueueToggle.HasValue)
            {
                return;
            }
            if (!Plugin.Network.IsConnected)
            {
                return;
            }
            var pending = Plugin._pendingQueueToggle.Value;
            Plugin._pendingQueueToggle = null;
            ApplyQueueToggle(pending);
        }

        public static void ApplyQueueToggle(bool joinQueue)
        {
            if (joinQueue)
            {
                if (Plugin.SelectedQueueMode == QueueModeSelection.Duo)
                {
                    var partner = GetDuoPartnerFromLobby();
                    if (partner == null)
                    {
                        Display.DisplayTip("LC Ranked", "Duo mode requires a teammate, bruh.", isWarning: true);
                        return;
                    }
                    Plugin.Network.JoinQueue(Plugin.LocalPlayerId, Plugin.LocalPlayerName, Plugin.LocalPlayerId, "duo_4p", partner.Value.partnerId, partner.Value.partnerName);
                }
                else
                {
                    Plugin.Network.JoinQueue(Plugin.LocalPlayerId, Plugin.LocalPlayerName, Plugin.LocalPlayerId, "solo_2p");
                }
                Plugin._inQueue = true;
            }
            else
            {
                Plugin.Network.LeaveQueue(Plugin.LocalPlayerId);
                Plugin._inQueue = false;
            }
        }

        [HarmonyPatch(typeof(QuickMenuManager))]
        public static class MatchLeverPatch
        {
            [HarmonyPatch("AddUserToPlayerList")]
            [HarmonyPostfix]
            public static void GetID(ulong steamId, string playerName)
            {
                if (!NetworkManager.Singleton.IsHost)
                {
                    return;
                }
                id11 = steamId.ToString();
                name11 = playerName;
            }
        }
        public static (string partnerId, string partnerName)? GetDuoPartnerFromLobby()
        {
            if (!NetworkManager.Singleton.IsHost)
            {
                return null;
            }

        Plugin.Log.LogWarning(id11+name11);
            return (id11, name11);
        }

        public static void HandleServerMessage(JObject msg)
        {

            string type = msg["type"]?.ToString();
            switch (type)
            {
                case "queue_joined":
                    Plugin._inQueue = true;
                    Plugin.QueuePlayerCount = msg["queueSize"]?.ToObject<int>() ?? Plugin.QueuePlayerCount;
                    break;

                case "queue_left":
                    Plugin._inQueue = false;
                    Plugin.QueuePlayerCount = msg["queueSize"]?.ToObject<int>() ?? Plugin.QueuePlayerCount;
                    break;

                case "queue_status":
                    Plugin.QueuePlayerCount = msg["queueSize"]?.ToObject<int>() ?? Plugin.QueuePlayerCount;
                    Plugin.PlayersInGameCount = msg["playersInGame"]?.ToObject<int>() ?? Plugin.PlayersInGameCount;
                    Plugin._inQueue = msg["inQueue"]?.ToObject<bool>() ?? Plugin._inQueue;
                    break;

                case "match_found":
                    Plugin._inQueue = false;
                    OnMatchFound(msg);
                    Plugin.track = true;
                    DebugUI.SetMenuForAll(false);
                    break;

                case "match_start":
                    OnMatchStart(msg);
                    Plugin.track = true;
                    DebugUI.SetMenuForAll(false);
                    break;

                case "opponent_result_in":
                    Plugin.Log.LogInfo("[LCRanked] Opponent has finished their run.");
                    RankedHUD.Instance.SetOpponent("Finished their run!");
                    break;

                case "match_result":
                    OnMatchResult(msg);
                    Plugin.track = false;
                    RankedHUD.Remove();
                    HUDManager.Instance.HideHUD(hide: false);
                    StartOfRound.Instance.displayedLevelResults = false;

                    break;

                case "match_aborted": // make match abortable : it only accounts for if a player is in a match so change me later please happy i beg
                    Display.DisplayTip("LC Ranked", "Match aborted!");
                    Plugin.Log.LogWarning($"[LCRanked] Match aborted: {msg["reason"]}");
                    Plugin.CurrentMatch.Reset();
                    Plugin.track = false;
                    OnMatchAborted(msg);
                    RankedHUD.Remove();
                    break;

                case "error":
                    Plugin.Log.LogError($"[LCRanked] Server error: {msg["message"]}");
                    break;

                default:
                    Plugin.Log.LogWarning($"[LCRanked] Unknown message type from server: {type}");
                    break;

                case "player_stats":
                    if (msg["playerId"]?.ToString() == Plugin.LocalPlayerId)
                    {
                        bool noRecord = msg["noRecord"]?.ToObject<bool>() ?? false;
                        Plugin.HasStatsRecord = !noRecord;
                        Plugin.LocalStats = noRecord ? null : msg.ToObject<Plugin.PlayerStats>();

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
                case "profile_stats":
                    ProfileWindowUI.Instance?.HandleStatsResult(msg);
                    break;

                case "profile_history":
                    ProfileWindowUI.Instance?.HandleHistoryResult(msg);
                    break;

                case "profile_search_result":
                    ProfileWindowUI.Instance?.HandleSearchResult(msg);
                    break;
            }
        }

        public void HandleNamePromptCheck(JObject msg)
        {
            if (msg["playerId"]?.ToString() != Plugin.LocalPlayerId) return;
            bool noRecord = msg["noRecord"]?.ToObject<bool>() ?? false;
            bool nameSet = !noRecord && (msg["nameSet"]?.ToObject<bool>() ?? false);
            if (!nameSet) NamePromptUI.Create();
        }

        public static void OnMatchAborted(JObject msg)
        {
            RankedHUD.Remove();
        }

        public static void OnMatchFound(JObject msg)
        {
            Display.DisplayTip("LC Ranked", "Match Found!\nMatch Starting in 15 seconds!");
            Plugin.CurrentMatch.Reset();
            Plugin.CurrentMatch.matchId = msg["matchId"]?.ToString();
            Plugin.CurrentMatch.mode = msg["mode"]?.ToString();
            Plugin.CurrentMatch.ruleset = msg["ruleset"]?.ToObject<Ruleset>();
            Plugin.CurrentMatch.rulesetJson = msg["ruleset"]?.ToString();
            Plugin.CurrentMatch.ruleset.weatherMS = msg["ruleset"]?["weather"]?.ToString();
            Plugin.CurrentMatch.ruleset.spawnCruiser = msg["ruleset"]?["spawnCruiser"]?.ToObject<bool>() ?? false;
            TimeOfDay.Instance.currentLevelWeather = LevelWeatherType.None;

            foreach (var p in msg["participants"])
            {
                Plugin.CurrentMatch.participants.Add(p.ToObject<ParticipantInfo>());
            }
        }

        public static void OnMatchStart(JObject msg)
        {
            Display.DisplayTip("LC Ranked", "Match starts now!");
            Plugin.CurrentMatch.startTimestampMs = msg["startTimestamp"]?.ToObject<long>() ?? 0;
            Plugin.Log.LogInfo("[LCRanked] Match starting now.");
            Plugin.Runner.BeginMatch(Plugin.CurrentMatch);
            RankedHUD.Create();
            string holder = Plugin.CurrentMatch.ruleset.moon.ToString();
            holder.Replace("Level", string.Empty);
            RankedHUD.Instance.SetMoon(holder);
            RankedHUD.Instance.SetSeed(Plugin.CurrentMatch.ruleset.seed);
            RankedHUD.Instance.SetMatchId(Plugin.CurrentMatch.matchId);
            RankedHUD.Instance.SetWeather(Plugin.CurrentMatch.ruleset.weatherMS);
            if (Plugin.LocalStats.rating == -1)
            {
                RankedHUD.Instance.mmrText.text = "Unranked";
            }
            else
            {
                RankedHUD.Instance.SetMMR(Plugin.LocalStats.rating);
            }
        }

        public static void OnMatchResult(JObject msg)
        {
            Plugin.Log.LogInfo($"[LCRanked] Match result: winner={msg["winnerName"]}");
            if (HUDManager.Instance != null)
            {
                Display.DisplayTip("LC Ranked", $"Match finished! Winner: {msg["winnerName"]}");
            }
            foreach (var placement in msg["placements"])
            {
                Plugin.Log.LogInfo($"  #{placement["placement"]} {placement["playerName"]}");
            }
            Plugin.CurrentMatch.Reset();
            if (RankedHUD.Instance != null)
            {
                RankedHUD.Remove();
            }
        }

        public static void StartQueueStatusPolling()
        {
            if (Plugin.DebugUi._pollingActive)
            {
                return;
            }

            StopQueueStatusPolling();
            Plugin.DebugUi._pollingActive = true;
            Plugin.DebugUi._queueStatusPoller = Plugin.StartCoroutine(PollQueueStatusCoroutine());
        }

        public static void StopQueueStatusPolling()
        {
            if (Plugin.DebugUi._queueStatusPoller != null)
            {
                Plugin.StopCoroutine(Plugin.DebugUi._queueStatusPoller);
                Plugin.DebugUi._queueStatusPoller = null;
            }

            Plugin.DebugUi._pollingActive = false;
        }

        public static IEnumerator PollQueueStatusCoroutine()
        {
            if (Plugin.DebugUi._plugin != null && Plugin.DebugUi._plugin.Network != null)
            {
                Queue.RequestQueueStatus();
                Plugin.DebugUi._plugin.RequestPlayerStats();
            }

            yield return new WaitForSeconds(15f);

            while (DebugUI._menuOpen)
            {
                if (Plugin.DebugUi._plugin != null && Plugin.DebugUi._plugin.Network != null && Plugin.DebugUi._plugin.Network.IsConnected)
                {
                    Queue.RequestQueueStatus();
                    Plugin.DebugUi._plugin.RequestPlayerStats();
                }

                yield return new WaitForSeconds(30f);
            }
        }

    }
}