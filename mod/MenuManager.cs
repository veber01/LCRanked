using System.Collections;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace LCRanked
{
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.ClickHostButton))]
    class HostButtonPatch
    {
        static bool Prefix(MenuManager __instance)
        {
            void PrepareRankedSave()
            {
                string activeSave = "LCSaveFile1";
                string backupSave = "Save1Temp";

                if (ES3.FileExists(activeSave))
                {
                    if (ES3.FileExists(backupSave))
                        ES3.DeleteFile(backupSave);

                    ES3.CopyFile(activeSave, backupSave);
                }

                if (ES3.FileExists(activeSave))
                    ES3.DeleteFile(activeSave);

                GameNetworkManager.Instance.currentSaveFileName = activeSave;
                GameNetworkManager.Instance.saveFileNum = 0;
            }
            PrepareRankedSave();
            GameNetworkManager.Instance.lobbyHostSettings = new HostSettings("Ranked", false, "");
            LeaderboardMenuLink.Hide();
            GameNetworkManager.Instance.StartHost();

            return false;
        }

        public static IEnumerator DelayedRestore()
        {
            yield return new WaitForSeconds(5f);
            RestoreNormalSave();
        }

        public static void RestoreNormalSave()
        {
            string activeSave = "LCSaveFile1";
            string backupSave = "Save1Temp";

            if (ES3.FileExists(activeSave))
                ES3.DeleteFile(activeSave);

            if (ES3.FileExists(backupSave))
            {
                ES3.CopyFile(backupSave, activeSave);
                ES3.DeleteFile(backupSave);
            }
        }

        public static void PrepareRankedSave()
        {
            string activeSave = "LCSaveFile1";
            string backupSave = "Save1Temp";

            if (ES3.FileExists(activeSave))
            {
                if (ES3.FileExists(backupSave))
                    ES3.DeleteFile(backupSave);

                ES3.CopyFile(activeSave, backupSave);
            }

            if (ES3.FileExists(activeSave))
                ES3.DeleteFile(activeSave);

            GameNetworkManager.Instance.currentSaveFileName = activeSave;
            GameNetworkManager.Instance.saveFileNum = 0;
        }

        [HarmonyPatch(typeof(MenuManager), "Start")]
        public class DisableJoinPatch
        {
            static void Postfix(MenuManager __instance)
            {
                if (__instance.joinCrewButtonContainer != null)
                {
                    __instance.joinCrewButtonContainer.SetActive(false);
                }

                if (__instance.versionNumberText != null)
                {
                    __instance.versionNumberText.text = "v81 LCR 0.3.1";
                }
                else
                {
                    Plugin.Log.LogWarning("[LCRanked] versionNumberText was null on MenuManager.Start() - skipping version text override.");
                }
            }
        }

        [HarmonyPatch(typeof(MenuManager), "Start")]
        public class CreditsReplacePatch
        {
            static void Postfix()
            {
                GameObject creditsTextObj = null;

                foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (obj.name == "CreditsText")
                    {
                        creditsTextObj = obj;
                        break;
                    }
                }

                if (creditsTextObj == null)
                {
                    Plugin.Log.LogWarning("[LCRanked] Could not find 'CreditsText' - skipping");
                    return;
                }

                var text = creditsTextObj.GetComponent<TMPro.TextMeshProUGUI>();
                if (text == null)
                {
                    Plugin.Log.LogWarning("[LCRanked] 'CreditsText' object found but no TextMeshProUGUI - skipping.");
                    return;
                }

                text.text =
                @"Lethal Company Ranked 

        Created by:
        Happyness 

        Special Thanks:
        Jyro (for ideas and inspiration)
        Walfrody (for ideas and inspiration)
        Resonance (for ideas and inspiration)
        Crew Finder discord community

        and YOU

        ";
            }
        }
    }
}



