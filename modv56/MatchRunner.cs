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
                Plugin.Log.LogError("[LCRanked] Not server/host - can't drive match start.");
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

                            if (obj.TryGetValue("mode", out var modeToken))
                            {
                                string mode = modeToken.ToString();
                                if (mode == "solo_2p")
                                {
                                    if (startOfRound.connectedPlayersAmount > 0)
                                    {
                                        startOfRound.KickPlayer(1);
                                        startOfRound.KickPlayer(2);
                                        startOfRound.KickPlayer(3);
                                    }
                                }
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
                yield break;
            }

            var terminal = FindObjectOfType<Terminal>();
            if (terminal != null)
            {
                terminal.groupCredits = match.ruleset.startingCredits;
            }

            yield return new WaitForSeconds(0.5f);

            if (match.ruleset.spawnCruiser)
            {
                SpawnCruiser(true);
            }
            if (pendingUtilities.Length > 0)
            {
                SpawnUtilities(pendingUtilities);
                yield return new WaitForSeconds(1f);
            }

            if (startOfRound.currentLevelID != targetLevelIndex)
            {
                startOfRound.ChangeLevelServerRpc(targetLevelIndex, terminal != null ? terminal.groupCredits : match.ruleset.startingCredits);

                yield return new WaitUntil(() => !startOfRound.travellingToNewLevel);
            }
            var targetLevel = startOfRound.levels[targetLevelIndex];
            ApplyForcedWeather(startOfRound, match.ruleset.weatherMS, targetLevel);
            SeedManager.Apply(startOfRound, match.ruleset.seed);
            //startOfRound.LocalPlayerDieEvent.AddListener((_, __) => RunTracker.OnLocalPlayerDied()); //v81
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
                    Plugin.Log.LogWarning($"[LCRanked] Error spawning utility '{utilityName}': {ex.Message}");
                }
            }
        }

        public static GameObject _spawnedCruiserMain;
        public static GameObject _spawnedCruiserSecondary;

        private void SpawnCruiser(bool spawnCruiser)
        {
            var terminal = FindObjectOfType<Terminal>();
            var startOfRound = StartOfRound.Instance;
            if (terminal == null || startOfRound == null) return;
            if (terminal.buyableVehicles == null || terminal.buyableVehicles.Length == 0)
            {
                return;
            }
            var vehicle = terminal.buyableVehicles[0];
            Transform hangarShipTransform = null;
            var hangarShip = GameObject.Find("HangarShip");
            if (hangarShip != null)
            {
                hangarShipTransform = hangarShip.transform;
            }
            Vector3 spawnPos = (hangarShipTransform != null ? hangarShipTransform.position : Vector3.zero) + new Vector3(8f, 0.5f, -10f);
            try
            {
                var mainObj = UnityEngine.Object.Instantiate(vehicle.vehiclePrefab, spawnPos, Quaternion.identity, RoundManager.Instance.VehiclesContainer);
                var mainNetObj = mainObj.GetComponent<NetworkObject>();
                if (mainNetObj != null)
                {
                    mainNetObj.Spawn();
                }
                _spawnedCruiserMain = mainObj;

                if (vehicle.secondaryPrefab != null)
                {
                    var secondaryObj = UnityEngine.Object.Instantiate(vehicle.secondaryPrefab, spawnPos, Quaternion.identity, RoundManager.Instance.VehiclesContainer);
                    var secondaryNetObj = secondaryObj.GetComponent<NetworkObject>();
                    if (secondaryNetObj != null)
                    {
                        secondaryNetObj.Spawn();
                    }
                    _spawnedCruiserSecondary = secondaryObj;
                }
                Plugin.Log.LogInfo($"[LCRanked] Spawned cruiser.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[LCRanked] Error spawning cruiser: {ex.Message}");
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
            for (int i = 0; i < RoundManager.Instance.currentLevel.randomWeathers.Length; i++)
            {
                TimeOfDay.Instance.currentWeatherVariable = RoundManager.Instance.currentLevel.randomWeathers[i].weatherVariable;
                TimeOfDay.Instance.currentWeatherVariable2 = RoundManager.Instance.currentLevel.randomWeathers[i].weatherVariable2;
            }
        }


        public static void DespawnCruiser()
        {
            try
            {
                DespawnCruiserObject(ref _spawnedCruiserMain);
                DespawnCruiserObject(ref _spawnedCruiserSecondary);
                Plugin.Log.LogInfo("[LCRanked] Cruiser despawned.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[LCRanked] Error despawning cruiser: {ex.Message}");
            }
        }

        private static void DespawnCruiserObject(ref GameObject obj)
        {
            if (obj == null) return;

            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                UnityEngine.Object.Destroy(obj);
            }

            obj = null;
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
