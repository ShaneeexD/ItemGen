using System;
using System.Collections;
using BepInEx;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace ItemGen.Client
{
    [BepInPlugin("com.serenity.itemgen", "ItemGen Client", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            try
            {
                BundleInjector.Init(Logger);
                var harmony = new Harmony("com.serenity.itemgen");
                harmony.PatchAll();
                StartCoroutine(InjectWhenReady());
                Logger.LogInfo("ItemGen client loaded.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"ItemGen client failed to load: {ex}");
            }
        }

        private IEnumerator InjectWhenReady()
        {
            yield return new WaitUntil(() => Singleton<IEasyAssets>.Instance != null);
            BundleInjector.InjectAll();
            Logger.LogInfo("All ItemGen bundles fully loaded.");
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
