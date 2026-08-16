using HarmonyLib;

namespace LCRanked
{

    [HarmonyPatch(typeof(RoundManager))]
    public static class DisableFox
    {
        [HarmonyPatch("SpawnRandomWeedEnemy")]
        [HarmonyPrefix]

        public static bool DisableWeedFox()
        {
            return false;
        }
    }
}

