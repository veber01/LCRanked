using System.Collections;
using HarmonyLib;
using LCRanked.UI;
using UnityEngine;

namespace LCRanked
{
    public static class RunTracker
    {
        private static MatchState _match;
        private static bool _tracking;
        private static bool _diedThisRun;

        public static bool AliveAt2pm { get; private set; }
        public static bool AliveAt9pm { get; private set; }

        private const float TwoPmNormalized = 0.4f;
        private const float NinePmNormalized = 0.8125f;

        public static void BeginTracking(MatchState match)
        {
            _match = match;
            _tracking = true;
            _diedThisRun = false;
            AliveAt2pm = false;
            AliveAt9pm = false;
        }

        public static void OnLocalPlayerDied()
        {
            if (!_tracking) return;
            _diedThisRun = true;
            Plugin.Log.LogInfo("[LCRanked] Local player died.");
        }


        public static IEnumerator WatchSurvivalCheckpoints()
        {
            while (_tracking)
            {
                if (TimeOfDay.Instance != null)
                {
                    float t = TimeOfDay.Instance.normalizedTimeOfDay;

                    if (!AliveAt2pm && !_diedThisRun && t >= TwoPmNormalized)
                    {
                        AliveAt2pm = true;
                    }

                    if (!AliveAt9pm && !_diedThisRun && t >= NinePmNormalized)
                    {
                        AliveAt9pm = true;
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        private static void OnRoundEnded(int scrapCollectedOnServer)
        {
            if (!_tracking) return;
            _tracking = false;

            _match.collectedValue = scrapCollectedOnServer;
            _match.survived = !_diedThisRun;
            _match.runFinished = true;
            _match.aliveAt2pm = AliveAt2pm;
            _match.aliveAt9pm = AliveAt9pm;

            Plugin.Log.LogInfo(
                $"[LCRanked] Run finished. Collected={_match.collectedValue} Survived={_match.survived} " +
                $"AliveAt2pm={AliveAt2pm} AliveAt9pm={AliveAt9pm}");

            ResultReporter.Report(_match);
        }

        [HarmonyPatch(typeof(QuickMenuManager), nameof(QuickMenuManager.LeaveGameConfirm))]
        public static class LeftEarly
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                if (Plugin.track == true)
                {
                    OnRoundEnded(0);
                    RankedHUD.Remove();
                }
                Plugin.track = false;

                LCRanked.HostButtonPatch.DelayedRestore();
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndOfGameClientRpc))]
        public static class EndOfGameClientRpc_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(int scrapCollectedOnServer)
            {
                OnRoundEnded(scrapCollectedOnServer);
            }
        }
    }
}
