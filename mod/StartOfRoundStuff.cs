using HarmonyLib;
using BepInEx;
using System.Reflection;
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
                Plugin.Log?.LogWarning($"[LCRanked] Failed to set magnet on: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound))]
    public static class EndOfDayStuff
    {
        [HarmonyPatch("SetPlanetsWeather")]
        [HarmonyPostfix]
        private static void tbhitsalotofstuff()
        {
            StartOfRound sor = StartOfRound.Instance;
            Plugin.collected = sor.scrapCollectedLastRound;
            RoundManager.Instance.DespawnPropsAtEndOfRound(true);
            RoundManager.Instance.scrapDroppedInShip.Clear();
            DeterministicEnemyPlanner.ranplan = false;
            return;
        }

    }


}