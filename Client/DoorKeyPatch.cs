using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace ItemGen.Client
{
    internal static class DoorKeyPatch
    {
        private static readonly Dictionary<string, List<string>> DoorToKeys = new Dictionary<string, List<string>>();

        internal static void Load()
        {
            string path = FindDoorsJsonPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Plugin.Log?.LogInfo("No doors.json found; door key patching skipped.");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                if (data != null)
                {
                    DoorToKeys.Clear();
                    foreach (var kvp in data)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value != null)
                            DoorToKeys[kvp.Key] = kvp.Value
                                .Where(k => !string.IsNullOrWhiteSpace(k))
                                .Distinct()
                                .ToList();
                    }
                }

                Plugin.Log?.LogInfo($"Loaded {DoorToKeys.Count} door-key mapping(s) from {path}.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to load doors.json: {ex.Message}");
            }
        }

        private static string FindDoorsJsonPath()
        {
            string gameDir = BepInEx.Paths.GameRootPath;
            if (!string.IsNullOrEmpty(gameDir))
            {
                string sptPath = Path.Combine(gameDir, "SPT", "user", "mods", "ItemGen", "doors.json");
                if (File.Exists(sptPath))
                    return sptPath;

                string rootPath = Path.Combine(gameDir, "user", "mods", "ItemGen", "doors.json");
                if (File.Exists(rootPath))
                    return rootPath;
            }

            // Fallback relative to the plugin directory
            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? "";
            if (!string.IsNullOrEmpty(pluginDir))
            {
                string bepInExRoot = Path.GetFullPath(Path.Combine(pluginDir, "..", ".."));
                string sptFallback = Path.Combine(bepInExRoot, "..", "SPT", "user", "mods", "ItemGen", "doors.json");
                if (File.Exists(sptFallback))
                    return sptFallback;

                string rootFallback = Path.Combine(bepInExRoot, "..", "user", "mods", "ItemGen", "doors.json");
                if (File.Exists(rootFallback))
                    return rootFallback;
            }

            return null;
        }

        private static List<string> GetKeyIds(string doorId)
        {
            if (DoorToKeys.TryGetValue(doorId, out var keyIds))
                return keyIds;
            return null;
        }

        private static bool IsDoorKey(WorldInteractiveObject worldInteractiveObject, string keyId)
        {
            var keyIds = GetKeyIds(worldInteractiveObject?.Id);
            return keyIds != null && keyIds.Contains(keyId);
        }

        [HarmonyPatch(typeof(World), "RegisterWorldInteractionObject")]
        private static class WorldRegisterWorldInteractionObjectPatch
        {
            static void Postfix(World __instance, WorldInteractiveObject worldInteractiveObject)
            {
                if (worldInteractiveObject == null)
                    return;

                var keyIds = GetKeyIds(worldInteractiveObject.Id);
                if (keyIds == null || keyIds.Count == 0)
                    return;

                // Add any existing KeyId from the map editor so it keeps working.
                if (!string.IsNullOrEmpty(worldInteractiveObject.KeyId) && !keyIds.Contains(worldInteractiveObject.KeyId))
                    keyIds.Add(worldInteractiveObject.KeyId);

                // Ensure the door has a non-empty KeyId so the Unlock action is shown.
                if (string.IsNullOrEmpty(worldInteractiveObject.KeyId))
                    worldInteractiveObject.KeyId = keyIds[0];

                worldInteractiveObject.DoorState = EDoorState.Locked;
                worldInteractiveObject.InitialDoorState = EDoorState.Locked;
                worldInteractiveObject.FallbackState = EDoorState.Locked;

                if (worldInteractiveObject is Door || worldInteractiveObject.GetType().Name == "Door")
                {
                    worldInteractiveObject.CurrentAngle = worldInteractiveObject.GetAngle(EDoorState.Locked);
                }

                Plugin.Log?.LogInfo($"[ItemGen] Registered door '{worldInteractiveObject.Id}' with {keyIds.Count} key(s).");
            }
        }

        [HarmonyPatch(typeof(PlayerOwner), "GetKey")]
        private static class PlayerOwnerGetKeyPatch
        {
            static void Postfix(PlayerOwner __instance, WorldInteractiveObject worldInteractiveObject, ref KeyComponent __result)
            {
                if (__result != null || worldInteractiveObject == null)
                    return;

                var keyIds = GetKeyIds(worldInteractiveObject.Id);
                if (keyIds == null || keyIds.Count == 0)
                    return;

                try
                {
                    var keys = __instance.Player.InventoryController.Inventory.Equipment.GetItemComponentsInChildren<KeyComponent>(false);
                    __result = keys.FirstOrDefault(k => keyIds.Contains(k.Template.KeyId));
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ItemGen] Failed to find key for door '{worldInteractiveObject.Id}': {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(WorldInteractiveObject), "UnlockOperation")]
        private static class WorldInteractiveObjectUnlockOperationPatch
        {
            static void Prefix(WorldInteractiveObject __instance, KeyComponent key)
            {
                if (key == null || __instance == null)
                    return;

                if (IsDoorKey(__instance, key.Template.KeyId))
                {
                    // Make the single KeyId match the key being used so the vanilla unlock logic succeeds.
                    __instance.KeyId = key.Template.KeyId;
                }
            }
        }
    }
}
