using HarmonyLib;
using BepInEx;
using System.Reflection;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

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

