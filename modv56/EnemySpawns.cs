using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace LCRanked
{
    public static class DeterministicEnemyPlanner
    {
        private class PlannedSpawn
        {
            public int enemyTypeIndex;
            public float spawnTime;
        }

        private static List<PlannedSpawn> plannedSpawns = new List<PlannedSpawn>();
        private static List<int> batchCumulativeCounts = new List<int>();
        private static int nextPlannedIndex = 0;
        private static int currentBatchIndex = -1;
        public static bool ranplan = false;
        public static System.Random planningRandom;

        //private static readonly AccessTools.FieldRef<RoundManager, int> enemyRushIndexRef =
        //  AccessTools.FieldRefAccess<RoundManager, int>("enemyRushIndex");             V81 only
        private static readonly AccessTools.FieldRef<RoundManager, int> currentHourRef =
            AccessTools.FieldRefAccess<RoundManager, int>("currentHour");

        private static readonly Action<RoundManager> spawnDaytimeOutside =
            AccessTools.MethodDelegate<Action<RoundManager>>(AccessTools.Method(typeof(RoundManager), "SpawnDaytimeEnemiesOutside"));
        private static readonly Action<RoundManager> spawnOutside =
            AccessTools.MethodDelegate<Action<RoundManager>>(AccessTools.Method(typeof(RoundManager), "SpawnEnemiesOutside"));
        private static readonly Action<RoundManager> spawnWeed =
            AccessTools.MethodDelegate<Action<RoundManager>>(AccessTools.Method(typeof(RoundManager), "SpawnWeedEnemies"));


        public static void PlanEnemySpawnsForWholeDay(RoundManager rm)
        {
            if (ranplan == true)
            {
                return;
            }
            ranplan = true;
            plannedSpawns.Clear();
            batchCumulativeCounts.Clear();
            nextPlannedIndex = 0;
            currentBatchIndex = -1;

            planningRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 918273); //thanks ak

            int enemyRushIndex = -1;//enemyRushIndexRef(rm);
            var shadowNumberSpawned = new int[rm.currentLevel.Enemies.Count];

            int totalHours = rm.timeScript.numberOfHours;
            for (int hour = 0; hour < totalHours; hour += rm.hourTimeBetweenEnemySpawnBatches)
            {
                float hourFraction = rm.timeScript.lengthOfHours * (float)hour / rm.timeScript.totalTime;
                float chance = rm.currentLevel.enemySpawnChanceThroughoutDay.Evaluate(hourFraction) - 1f;
                if (StartOfRound.Instance.isChallengeFile) chance += 1f;

                int countThisBatch = Mathf.RoundToInt(Mathf.Clamp(
                    Mathf.Lerp(
                        chance + (float)Mathf.Abs(TimeOfDay.Instance.daysUntilDeadline - 3) / 1.6f - rm.currentLevel.spawnProbabilityRange,
                        chance + rm.currentLevel.spawnProbabilityRange,
                        (float)planningRandom.NextDouble()), 
                    0, 20f));

                if (enemyRushIndex != -1) countThisBatch += 2;

                float batchStartTime = rm.timeScript.lengthOfHours * (float)hour;
                float batchWindow = rm.timeScript.lengthOfHours * (float)rm.hourTimeBetweenEnemySpawnBatches;

                for (int n = 0; n < countThisBatch; n++)
                {
                    int chosen = ChooseWeightedEnemyIndex(rm, shadowNumberSpawned, hourFraction, enemyRushIndex, planningRandom);
                    if (chosen == -1) break;

                    shadowNumberSpawned[chosen]++;

                    float spawnTime = planningRandom.Next( 
                        (int)(10f + batchStartTime),
                        (int)(batchWindow + batchStartTime));

                    plannedSpawns.Add(new PlannedSpawn { enemyTypeIndex = chosen, spawnTime = spawnTime });
                }

                batchCumulativeCounts.Add(plannedSpawns.Count);
            }

            Debug.Log($"[LCRanked] Deterministic enemy plan built: {plannedSpawns.Count} enemies planned across {batchCumulativeCounts.Count} batches.");
            LogFullDayPlan(rm);
        }

        private static void LogFullDayPlan(RoundManager rm)
        {
            int batch = 0;
            for (int i = 0; i < plannedSpawns.Count; i++)
            {
                while (batch < batchCumulativeCounts.Count && i >= batchCumulativeCounts[batch])
                {
                    batch++;
                }
                var planned = plannedSpawns[i];
                var enemyType = rm.currentLevel.Enemies[planned.enemyTypeIndex].enemyType;
                Debug.Log($"[LCRanked] Plan #{i} (batch {batch}): {enemyType.enemyName} @ t={planned.spawnTime:F1}");
            }
        }

        private static int PickWeightedIndexDeterministic(List<int> weights, System.Random random)
        {
            int total = weights.Sum();
            if (total <= 0) return -1;

            int roll = random.Next(0, total);
            int cumulative = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                cumulative += weights[i];
                if (roll < cumulative) return i;
            }
            return weights.Count - 1;
        }




        private static int ChooseWeightedEnemyIndex(RoundManager rm, int[] shadowNumberSpawned, float hourFraction, int enemyRushIndex, System.Random planningRandom)
        {
            var weights = new List<int>();

            for (int j = 0; j < rm.currentLevel.Enemies.Count; j++)
            {
                var enemyType = rm.currentLevel.Enemies[j].enemyType;

                if (enemyType.spawningDisabled || shadowNumberSpawned[j] >= enemyType.MaxCount)
                {
                    weights.Add(0);
                    continue;
                }

                int weight;
                if (enemyRushIndex == j) weight = 100;
                else if (enemyType.useNumberSpawnedFalloff)
                {
                    weight = (int)((float)rm.currentLevel.Enemies[j].rarity *
                        (enemyType.probabilityCurve.Evaluate(hourFraction) *
                         enemyType.numberSpawnedFalloff.Evaluate(Mathf.Clamp((float)shadowNumberSpawned[j] / 10f, 0f, 1f))));
                }
                else
                {
                    weight = (int)((float)rm.currentLevel.Enemies[j].rarity * enemyType.probabilityCurve.Evaluate(hourFraction));
                }

                if (enemyRushIndex != -1 && enemyRushIndex != j)
                {
                    weight = Mathf.RoundToInt((float)weight * 0.075f);
                }

                weights.Add(Mathf.Max(weight, 0));
            }

            // if (rm.currentLevel.specialEnemyRarity.overrideEnemy != null)
            // {
            //     int overrideIdx = rm.currentLevel.Enemies.FindIndex(e => e.enemyType == rm.currentLevel.specialEnemyRarity.overrideEnemy);
            //     if (overrideIdx != -1 && weights[overrideIdx] != 0)
            //     {
            //         if (rm.currentLevel.specialEnemyRarity.percentageChance >= 1f) return overrideIdx;
            //         float others = weights.Where((w, i) => i != overrideIdx).Sum();
            //         if (rm.currentLevel.specialEnemyRarity.percentageChance > 0f)
            //         {
            //             weights[overrideIdx] = (int)(rm.currentLevel.specialEnemyRarity.percentageChance * others / (1f - rm.currentLevel.specialEnemyRarity.percentageChance));
            //         }
            //     }
            // }

            if (weights.Sum() <= 0) return -1;
            Debug.Log($"[LCRanked] weights=[{string.Join(",", weights)}] sum={weights.Sum()}");
            return PickWeightedIndexDeterministic(weights, planningRandom);
        }

        public static void TryAdvancePlannedEnemySpawns(RoundManager rm)
        {
            if (currentBatchIndex < 0 || currentBatchIndex >= batchCumulativeCounts.Count) return;
            int allowedCount = batchCumulativeCounts[currentBatchIndex];

            while (nextPlannedIndex < allowedCount)
            {
                var planned = plannedSpawns[nextPlannedIndex];
                var enemyType = rm.currentLevel.Enemies[planned.enemyTypeIndex].enemyType;

                if (rm.currentEnemyPower >= rm.currentMaxInsidePower && (rm.currentEnemyPower + enemyType.PowerLevel) > rm.currentMaxInsidePower) //thanks ak
                {
                    rm.cannotSpawnMoreInsideEnemies = true;
                    Debug.Log($"[LCRanked] Deferring planned spawn ({enemyType.enemyName}) - power cap reached.");
                    break;
                }

                var freeVents = rm.allEnemyVents.Where(v => !v.occupied).ToList();
                if (freeVents.Count == 0)
                {
                    Debug.Log($"[LCRanked] Deferring planned spawn ({enemyType.enemyName}) - no free vent.");
                    break;
                }
                EnemyVent freeVent = freeVents[planningRandom.Next(0, freeVents.Count)];

                freeVent.enemyType = enemyType;
                freeVent.enemyTypeIndex = planned.enemyTypeIndex;
                freeVent.occupied = true;
                freeVent.spawnTime = planned.spawnTime;
                freeVent.SyncVentSpawnTimeClientRpc((int)planned.spawnTime, planned.enemyTypeIndex);

                rm.currentEnemyPower += enemyType.PowerLevel;
                //rm.currentEnemyPowerNoDeaths += enemyType.PowerLevel;
                //if (!enemyType.hasSpawnedAtLeastOne)
                //{
                //    rm.currentInsideEnemyDiversityLevel += enemyType.DiversityPowerLevel;
                //}
                enemyType.numberSpawned++;
                //enemyType.hasSpawnedAtLeastOne = true;

                Debug.Log($"[LCRanked] Committed planned spawn: {enemyType.enemyName}.");
                nextPlannedIndex++;
            }
        }


        public static void SpawnReadyVents(RoundManager rm)
        {
            for (int i = 0; i < rm.allEnemyVents.Length; i++)
            {
                if (rm.allEnemyVents[i].occupied && rm.timeScript.currentDayTime > rm.allEnemyVents[i].spawnTime)
                {
                    Debug.Log("Found enemy vent which has its time up: " + rm.allEnemyVents[i].gameObject.name + ". Spawning " + rm.allEnemyVents[i].enemyType.enemyName + " from vent.");
                    rm.SpawnEnemyFromVent(rm.allEnemyVents[i]);
                }
            }
        }

        public static void BeginSpawning(RoundManager rm)
        {
            currentBatchIndex = 0;
            TryAdvancePlannedEnemySpawns(rm);
        }

        public static void AdvanceHour(RoundManager rm)
        {
            currentHourRef(rm) += rm.hourTimeBetweenEnemySpawnBatches;
            spawnDaytimeOutside(rm);
            spawnOutside(rm);
            spawnWeed(rm);

            currentBatchIndex++;

            if (rm.allEnemyVents.Length != 0 && !rm.cannotSpawnMoreInsideEnemies)
            {
                TryAdvancePlannedEnemySpawns(rm);
            }
            else
            {
                Debug.Log($"Could not spawn more enemies; vents #: {rm.allEnemyVents.Length}. CannotSpawnMoreInsideEnemies: {rm.cannotSpawnMoreInsideEnemies}");
            }
        }
    }

    [HarmonyPatch(typeof(RoundManager), "SpawnInsideEnemiesFromVentsIfReady")]
    public static class SpawnInsideEnemiesFromVentsIfReady_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(RoundManager __instance)
        {
            DeterministicEnemyPlanner.SpawnReadyVents(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(RoundManager), "AdvanceHourAndSpawnNewBatchOfEnemies")]
    public static class AdvanceHourAndSpawnNewBatchOfEnemies_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(RoundManager __instance)
        {
            DeterministicEnemyPlanner.AdvanceHour(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.BeginEnemySpawning))]
    public static class BeginEnemySpawning_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(RoundManager __instance)
        {
            if (!__instance.IsServer) return false;

            if (__instance.allEnemyVents.Length != 0 && __instance.currentLevel.maxEnemyPowerCount > 0)
            {
                DeterministicEnemyPlanner.BeginSpawning(__instance);
                __instance.isSpawningEnemies = true;
            }
            else
            {
                Debug.Log("Not able to spawn enemies on map; no vents were detected or maxEnemyPowerCount is 0.");
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
    public static class FinishGeneratingNewLevelClientRpc_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(RoundManager __instance)
        {
            if (!__instance.IsServer) return;
            DeterministicEnemyPlanner.PlanEnemySpawnsForWholeDay(__instance);
        }
    }
}