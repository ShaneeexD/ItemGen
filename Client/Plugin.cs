using System;
using System.Collections;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace ItemGen.Client
{
    [BepInPlugin("com.serenity.itemgen", "ItemGen Client", "1.2.5")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            try
            {
                BundleInjector.Init(Log);
                DoorKeyPatch.Load();
                var harmony = new Harmony("com.serenity.itemgen");
                harmony.PatchAll();
                StartCoroutine(InjectWhenReady());
                Log.LogInfo("ItemGen client loaded.");
            }
            catch (Exception ex)
            {
                Log.LogError($"ItemGen client failed to load: {ex}");
            }
        }

        private IEnumerator InjectWhenReady()
        {
            yield return new WaitUntil(() => Singleton<IEasyAssets>.Instance != null);
            BundleInjector.InjectAll();
            Log.LogInfo("All ItemGen bundles fully loaded.");
        }
    }

    // If a custom prefab path is requested before the bundle is injected, inject it on demand.
    [HarmonyPatch(typeof(DependencyGraphClass<IEasyBundle>), "GetNode")]
    internal static class GetNodePatch
    {
        static void Prefix(DependencyGraphClass<IEasyBundle> __instance, string key)
        {
            try
            {
                BundleInjector.InjectSingle(__instance, key);
            }
            catch (Exception ex)
            {
                // Silently ignore reflection failures; the game will report missing assets normally.
                Debug.LogError($"[ItemGen] On-demand bundle injection failed: {ex}");
            }
        }
    }
}
