using System.Collections;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using LCRanked.UI;
using System;
using GameNetcodeStuff;
using HarmonyLib;

namespace LCRanked
{

    public class MatchRunner : MonoBehaviour
    {
        private string[] pendingUtilities = new string[0];


        public void BeginMatch(MatchState match)
        {
            StartCoroutine(RunMatchStartSequence(match));
        }

        public void SetPendingUtilities(string[] utilities)
        {
            pendingUtilities = utilities;
        }

        private IEnumerator RunMatchStartSequence(MatchState match)
        {
            var startOfRound = StartOfRound.Instance;
            if (startOfRound == null)
            {
                Plugin.Log.LogError("[LCRanked] StartOfRound.Instance is null, can't start match.");
                yield break;
            }

            if (!startOfRound.IsServer)
            {
                Plugin.Log.LogWarning("[LCRanked] Not server/host - can't drive match start.");
                yield break;
            }

            int? levelId = null;
            string[] utilities = new string[0];
            string rulesetJson = match?.rulesetJson;
            if (string.IsNullOrWhiteSpace(rulesetJson) && match?.ruleset != null)
            {
                rulesetJson = match.ruleset.ToString();
            }

            if (!string.IsNullOrWhiteSpace(rulesetJson))
            {
                try
                {
                    var token = JToken.Parse(rulesetJson);
                    if (token is JObject obj)
                    {
                        if (obj.TryGetValue("levelId", out var levelIdToken))
                        {
                            if (levelIdToken.Type == JTokenType.Integer)
                            {
                                levelId = levelIdToken.ToObject<int>();
                            }
                            else if (levelIdToken.Type == JTokenType.String && int.TryParse(levelIdToken.ToString(), out var parsedLevelId))
                            {
                                levelId = parsedLevelId;
                            }
                        }

                        if (obj.TryGetValue("utilities", out var utilitiesToken))
                        {
                            if (utilitiesToken is JArray utilitiesArray)
                            {
                                utilities = utilitiesArray.Select(t => t.ToString()).ToArray();
                                SetPendingUtilities(utilities);
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            int targetLevelIndex = FindLevelIndex(startOfRound, match.ruleset.moon, levelId);
            if (targetLevelIndex < 0)
            {
                Plugin.Log.LogError($"[LCRanked] Unknown moon in ruleset: {match.ruleset.moon} (levelId={levelId})");
                yield break;
            }

            var terminal = FindObjectOfType<Terminal>();
            if (terminal != null)
            {
                terminal.groupCredits = match.ruleset.startingCredits;
            }

            yield return new WaitForSeconds(0.5f);

            if (pendingUtilities.Length > 0)
            {
                SpawnUtilities(pendingUtilities);
                yield return new WaitForSeconds(1f);
            }

            if (startOfRound.currentLevelID != targetLevelIndex)
            {
                Plugin.Log.LogInfo($"[LCRanked] Routing to moon: {match.ruleset.moon} (index {targetLevelIndex})");
                startOfRound.ChangeLevelServerRpc(targetLevelIndex, terminal != null ? terminal.groupCredits : match.ruleset.startingCredits);

                yield return new WaitUntil(() => !startOfRound.travellingToNewLevel);
            }
            var targetLevel = startOfRound.levels[targetLevelIndex];
            ApplyForcedWeather(startOfRound, match.ruleset.weatherMS, targetLevel);

            SeedManager.Apply(startOfRound, match.ruleset.seed);
            //startOfRound.LocalPlayerDieEvent.AddListener((_, __) => RunTracker.OnLocalPlayerDied());
            RunTracker.BeginTracking(match);
            StartCoroutine(RunTracker.WatchSurvivalCheckpoints());
            startOfRound.StartGameServerRpc();
        }

        private void SpawnUtilities(string[] utilities)
        {
            var terminal = FindObjectOfType<Terminal>();
            var startOfRound = StartOfRound.Instance;

            if (terminal == null)
                return;


            if (startOfRound == null)
                return;

            Transform hangarShipTransform = null;
            var hangarShip = GameObject.Find("HangarShip");
            if (hangarShip != null)
            {
                hangarShipTransform = hangarShip.transform;
            }

            Vector3 spawnPos = new Vector3(5f, 0.5f, -14f);
            int spawnPosIndex = 0;

            foreach (var utilityName in utilities)
            {
                try
                {
                    int itemIndex = -1;
                    for (int i = 0; i < terminal.buyableItemsList.Length; i++)
                    {
                        if (terminal.buyableItemsList[i].itemName == utilityName)
                        {
                            itemIndex = i;
                            break;
                        }
                    }

                    if (itemIndex < 0)
                    {
                        continue;
                    }

                    var spawnPrefab = terminal.buyableItemsList[itemIndex].spawnPrefab;
                    if (spawnPrefab == null)
                    {
                        continue;
                    }

                    Transform parentTransform = hangarShipTransform ?? startOfRound.propsContainer;
                    GameObject spawnedItem = UnityEngine.Object.Instantiate(spawnPrefab, spawnPos, Quaternion.identity, parentTransform);

                    var grabbable = spawnedItem.GetComponent<GrabbableObject>();
                    if (grabbable != null)
                    {
                        grabbable.fallTime = 0f;
                    }

                    var networkObject = spawnedItem.GetComponent<NetworkObject>();
                    if (networkObject != null)
                    {
                        networkObject.Spawn();
                        networkObject.TrySetParent(spawnedItem.transform.parent);
                    }
                    spawnPos += Vector3.right * 1f;
                    spawnPosIndex++;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogWarning($"[LCRanked] Error spawning utility '{utilityName}': {ex.Message}");
                }
            }
        }

        private int FindLevelIndex(StartOfRound startOfRound, string moonName, int? levelId)
        {
            if (levelId.HasValue)
            {
                for (int i = 0; i < startOfRound.levels.Length; i++)
                {
                    var level = startOfRound.levels[i];
                    if (level == null)
                    {
                        continue;
                    }

                    if (i == levelId.Value)
                    {
                        return i;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(moonName))
            {
                return -1;
            }

            var searchTerms = GetSearchTerms(moonName);

            for (int i = 0; i < startOfRound.levels.Length; i++)
            {
                var level = startOfRound.levels[i];
                if (level == null)
                {
                    continue;
                }

                string candidate = NormalizeMoonName(level.PlanetName);
                foreach (var term in searchTerms)
                {
                    if (candidate == term)
                    {
                        return i;
                    }
                }
            }

            var bestMatch = -1;
            for (int i = 0; i < startOfRound.levels.Length; i++)
            {
                var level = startOfRound.levels[i];
                if (level == null)
                {
                    continue;
                }
                var levelName = level.PlanetName;
                if (string.IsNullOrWhiteSpace(levelName))
                {
                    continue;
                }
                var levelNameNormalized = NormalizeMoonName(levelName);
                foreach (var term in searchTerms)
                {
                    if (levelNameNormalized.Contains(term) || term.Contains(levelNameNormalized))
                    {
                        return i;
                    }

                    if (bestMatch < 0 && levelNameNormalized.StartsWith(term.Substring(0, System.Math.Min(3, term.Length))))
                    {
                        bestMatch = i;
                    }
                }
            }
            return bestMatch;
        }

        private string[] GetSearchTerms(string moonName)
        {
            if (string.IsNullOrWhiteSpace(moonName))
            {
                return new string[0];
            }

            string normalized = NormalizeMoonName(moonName);
            return new[]
            {
                normalized,
                normalized.Replace("level", string.Empty),
                normalized.Replace("moon", string.Empty),
                normalized.Replace("planet", string.Empty)
            }
            .Where(term => !string.IsNullOrEmpty(term))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
        }

        private string NormalizeMoonName(string moonName)
        {
            if (string.IsNullOrWhiteSpace(moonName))
            {
                return string.Empty;
            }

            string normalized = moonName.Trim();
            normalized = normalized.Replace(" ", "").Replace("-", "").Replace("_", "");
            normalized = normalized.ToLowerInvariant();

            return normalized;
        }

        private void ApplyForcedWeather(StartOfRound startOfRound, string weatherName, SelectableLevel targetLevel)
        {
            if (string.IsNullOrEmpty(weatherName))
            {
                Plugin.Log.LogError("weatherName is empty, what");
                return;
            }
            if (weatherName == "Clear")
            {
                weatherName = "None";
            }
            if (!Enum.TryParse<LevelWeatherType>(weatherName, ignoreCase: true, out var weatherType))
            {
                Plugin.Log.LogError($"[LCRanked] Unknown weather type from server: {weatherName}");
                return;
            }

            foreach (var level in startOfRound.levels)
            {
                level.currentWeather = LevelWeatherType.None;
            }

            targetLevel.currentWeather = weatherType;
            targetLevel.overrideWeather = true;
            targetLevel.overrideWeatherType = weatherType;

            TimeOfDay.Instance.currentLevelWeather = weatherType;

            Plugin.Log.LogInfo($"[LCRanked] Forced weather '{weatherType}' on {targetLevel.PlanetName}");
        }



        //KillPlayerEvent listener for v56

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
        public static class KillPlayerPatch
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerControllerB __instance)
            {
                if (__instance == StartOfRound.Instance.localPlayerController)
                {
                    RunTracker.OnLocalPlayerDied();
                }
            }
        }

    }
}
