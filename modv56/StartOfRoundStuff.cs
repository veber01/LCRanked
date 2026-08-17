using HarmonyLib;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace LCRanked
{
    [HarmonyPatch(typeof(StartOfRound))]
    public static class MatchLeverPatch
    {
        [HarmonyPatch("LoadAttachedVehicle")]
        [HarmonyPostfix]
        public static void SetLeverDisable(StartOfRound __instance)
        {
            GameNetworkManager.Instance.maxAllowedPlayers = 2;
            DuoHudRelay.EnsureHandlersRegistered();
            __instance.StartCoroutine(PreStuff(__instance));
        }

        private static IEnumerator PreStuff(StartOfRound instance)
        {
            yield return new WaitForSeconds(1f);

            var startMatchLeverType = Object.FindObjectOfType<StartMatchLever>();
            startMatchLeverType.triggerScript.interactable = false;
            startMatchLeverType.triggerScript.hoverTip = "[ Que up!. ]";
            DeterministicEnemyPlanner.ranplan = false;
            try
            {
                instance.SetMagnetOn(true);
                SelectableLevel SL = SelectableLevel.FindObjectOfType<SelectableLevel>();
                SL.currentWeather = SL.randomWeathers[0].weatherType;
                TimeOfDay.Instance.currentLevelWeather = LevelWeatherType.None;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[LCRanked] Failed to set magnet on: {ex.Message}");
            }
            GameNetworkManager.Instance.maxAllowedPlayers = 2;
        }
    }

    [HarmonyPatch(typeof(StartOfRound))]
    public static class EndOfDayStuff
    {
        [HarmonyPatch("SetPlanetsWeather")]
        [HarmonyPostfix]
        private static void tbhitsalotofstuff()
        {
            if (!Plugin.track)
            {
                return;
            }
            try
            {
                StartOfRound sor = StartOfRound.Instance;
                if (sor == null) return;

                Plugin.collected = sor.scrapCollectedLastRound;
                RoundManager.Instance?.DespawnPropsAtEndOfRound(true);
                DeterministicEnemyPlanner.ranplan = false;
                MatchRunner.DespawnCruiser();
                TimeOfDay.Instance.daysUntilDeadline = 2;
                TimeOfDay.Instance.quotaVariables.deadlineDaysAmount = 2;
                TimeOfDay.Instance.timeUntilDeadline = 2;

                var startMatchLeverType = Object.FindObjectOfType<StartMatchLever>();
                if (startMatchLeverType != null)
                {
                    startMatchLeverType.triggerScript.interactable = false;
                    startMatchLeverType.triggerScript.hoverTip = "[ Que up!. ]";
                }

                sor.SetMagnetOn(true);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[LCRanked] EndOfDayStuff failed: {ex.Message}");
            }

            // if (NetworkManager.Singleton.IsHost)
            // {
            //     RoundManager.Instance.DespawnPropsAtEndOfRound(true);
            //     MatchRunner.DespawnCruiser();
            // }
            // StartOfRound sor = StartOfRound.Instance;
            // Plugin.collected = sor.scrapCollectedLastRound;

            // DeterministicEnemyPlanner.ranplan = false;
            // TimeOfDay.Instance.daysUntilDeadline = 2;
            // TimeOfDay.Instance.quotaVariables.deadlineDaysAmount = 2;
            // TimeOfDay.Instance.timeUntilDeadline = 2;
            // var startMatchLeverType = Object.FindObjectOfType<StartMatchLever>();
            // startMatchLeverType.triggerScript.interactable = false;
            // startMatchLeverType.triggerScript.hoverTip = "[ Que up!. ]";
            // if (NetworkManager.Singleton.IsHost)
            // {

            //     StartOfRound.Instance.SetMagnetOn(true);
            // }
            // DeterministicEnemyPlanner.ranplan = false;
            // return;
        }
    }
}