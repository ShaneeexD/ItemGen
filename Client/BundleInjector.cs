using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Logging;
using Comfort.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ItemGen.Client
{
    // Injects custom asset bundles into EFT's IEasyAssets so server-side prefab paths resolve.
    // Reads the same item pack JSON files used by the ItemGen server to find the prefab paths,
    // then loads matching bundle files from BepInEx\plugins\Serenity-ItemGen\bundles.
    // Vanilla paths are left alone; only paths that have a matching custom bundle file are injected.
    internal static class BundleInjector
    {
        private static readonly Dictionary<string, string> _bundleFileByAssetPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static ManualLogSource _log;

        internal static void Init(ManualLogSource log)
        {
            _log = log;
        }

        internal static void InjectAll()
        {
            var easyAssets = Singleton<IEasyAssets>.Instance;
            if (easyAssets == null)
            {
                _log?.LogError("IEasyAssets singleton not ready");
                return;
            }

            DiscoverBundles();
            InjectIntoSystem(easyAssets.System);
        }

        internal static void InjectSingle(DependencyGraphClass<IEasyBundle> system, string assetPath)
        {
            if (!_bundleFileByAssetPath.ContainsKey(assetPath))
                return;

            InjectIntoSystem(system, assetPath);
        }

        private static void DiscoverBundles()
        {
            _bundleFileByAssetPath.Clear();

            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string bundlesDir = Path.Combine(pluginDir, "bundles");
            if (!Directory.Exists(bundlesDir))
            {
                _log?.LogInfo("No bundles folder found, skipping custom bundle discovery.");
                return;
            }

            var assetPaths = CollectAssetPathsFromItemPacks();
            if (assetPaths.Count == 0)
            {
                _log?.LogInfo("No prefab paths found in ItemGen item packs; bundles will be auto-loaded by filename.");
            }

            // Pre-load manifest for explicit overrides
            string manifestPath = Path.Combine(pluginDir, "bundles.json");
            var manifestEntries = new List<BundleManifestEntry>();
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    manifestEntries = JsonConvert.DeserializeObject<List<BundleManifestEntry>>(json) ?? new List<BundleManifestEntry>();
                }
                catch (Exception ex)
                {
                    _log?.LogError($"Failed to read bundles.json: {ex}");
                }
            }

            // Discover every file in the bundles folder. Manifest overrides take precedence,
            // then item-pack prefab path matches, then the filename itself is used as the asset path.
            string[] bundleFiles = Directory.GetFiles(bundlesDir, "*.*", SearchOption.AllDirectories);
            foreach (string filePath in bundleFiles)
            {
                string fileName = Path.GetFileName(filePath);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);

                // Skip the manifest itself
                if (string.Equals(fileName, "bundles.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Manifest override
                var manifestEntry = manifestEntries.FirstOrDefault(e =>
                    string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.FileName, fileNameNoExt, StringComparison.OrdinalIgnoreCase));
                if (manifestEntry != null)
                {
                    _bundleFileByAssetPath[manifestEntry.AssetPath] = filePath;
                    _log?.LogInfo($"Manifest bundle: {fileName} -> {manifestEntry.AssetPath}");
                    continue;
                }

                // Match against prefab paths found in ItemGen item packs
                string matchingPath = assetPaths.FirstOrDefault(p =>
                    string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileNameWithoutExtension(p), fileNameNoExt, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchingPath))
                {
                    _bundleFileByAssetPath[matchingPath] = filePath;
                    _log?.LogInfo($"Discovered custom bundle: {fileName} -> {matchingPath}");
                    continue;
                }

                // Fall back to auto-load by filename
                _bundleFileByAssetPath[fileName] = filePath;
                _log?.LogInfo($"Auto-loaded bundle: {fileName} -> {fileName}");
            }
        }

        private static HashSet<string> CollectAssetPathsFromItemPacks()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] packDirs = FindItemPackDirectories();

            foreach (string dir in packDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var token = JObject.Parse(json);
                        CollectPaths(token, paths);
                    }
                    catch (Exception ex)
                    {
                        _log?.LogWarning($"Could not read item pack {file}: {ex.Message}");
                    }
                }
            }

            return paths;
        }

        private static string[] FindItemPackDirectories()
        {
            string gameDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? "");
            var candidates = new List<string>();

            if (!string.IsNullOrEmpty(gameDir))
            {
                candidates.Add(Path.Combine(gameDir, "SPT", "user", "mods", "ItemGen", "items"));
                candidates.Add(Path.Combine(gameDir, "user", "mods", "ItemGen", "items"));
            }

            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string bepInExRoot = Path.GetFullPath(Path.Combine(pluginDir, "..", ".."));
            candidates.Add(Path.Combine(bepInExRoot, "..", "SPT", "user", "mods", "ItemGen", "items"));
            candidates.Add(Path.Combine(bepInExRoot, "..", "user", "mods", "ItemGen", "items"));

            return candidates.ToArray();
        }

        private static void CollectPaths(JToken token, HashSet<string> paths)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if ((prop.Name == "path" || prop.Name == "Prefab" || prop.Name == "UsePrefab" || prop.Name == "customModel")
                        && prop.Value?.Type == JTokenType.String)
                    {
                        paths.Add(prop.Value.ToString());
                    }
                    else if (prop.Value != null)
                    {
                        CollectPaths(prop.Value, paths);
                    }
                }
            }
            else if (token is JArray arr)
            {
                foreach (var child in arr)
                    CollectPaths(child, paths);
            }
        }

        private static void InjectIntoSystem(DependencyGraphClass<IEasyBundle> system, string onlyKey = null)
        {
            if (system == null)
            {
                _log?.LogError("IEasyAssets.System is null");
                return;
            }

            var nodes = system.Nodes;
            object existingNode = null;
            foreach (var kv in nodes)
            {
                existingNode = kv.Value;
                break;
            }

            if (existingNode == null)
            {
                _log?.LogError("No existing nodes to use as template");
                return;
            }

            var nodeType = existingNode.GetType();
            var dataField = nodeType.GetField("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (dataField == null)
            {
                _log?.LogError("GClass1662.Data field not found");
                return;
            }

            var nodeCtor = nodeType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { dataField.FieldType }, null);
            if (nodeCtor == null)
            {
                _log?.LogError("GClass1662 ctor(T) not found");
                return;
            }

            var depsField = nodeType.GetField("Dependencies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var existingData = dataField.GetValue(existingNode);
            var bundleDataType = existingData.GetType();

            var existingLoadState = GetProp(bundleDataType, existingData, "LoadState");
            if (existingLoadState == null)
            {
                _log?.LogWarning($"LoadState property not found on {bundleDataType.Name}");
                return;
            }

            var lsType = existingLoadState.GetType();
            var valueProp = lsType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var loadedVal = valueProp != null ? Enum.Parse(valueProp.PropertyType, "Loaded") : null;

            foreach (var kvp in _bundleFileByAssetPath)
            {
                string assetPath = kvp.Key;
                string filePath = kvp.Value;

                if (onlyKey != null && assetPath != onlyKey)
                    continue;

                if (nodes.ContainsKey(assetPath))
                    continue;

                var bundle = AssetBundle.LoadFromFile(filePath);
                if (bundle == null)
                {
                    _log?.LogError($"Failed to load bundle: {filePath}");
                    continue;
                }

                var allAssets = bundle.LoadAllAssets();
                _log?.LogInfo($"Bundle {Path.GetFileName(filePath)} loaded {allAssets.Length} asset(s)");

                var newBundleData = FormatterServices.GetUninitializedObject(bundleDataType);
                SetProp(bundleDataType, newBundleData, "Key", assetPath);
                SetProp(bundleDataType, newBundleData, "Assets", allAssets);
                SetProp(bundleDataType, newBundleData, "SameNameAsset", allAssets.Length > 0 ? allAssets[0] : null);
                SetField(bundleDataType, newBundleData, "Bool_0", true);
                SetProp(bundleDataType, newBundleData, "Progress", 1f);

                var newLs = Activator.CreateInstance(lsType);
                if (valueProp != null && loadedVal != null)
                    valueProp.SetValue(newLs, loadedVal);
                SetProp(bundleDataType, newBundleData, "LoadState", newLs);

                var newNode = nodeCtor.Invoke(new object[] { newBundleData });
                depsField?.SetValue(newNode, Array.CreateInstance(nodeType, 0));

                nodes.Add(assetPath, (GClass1662<IEasyBundle>)newNode);
                _log?.LogInfo($"Injected IEasyBundle node for {assetPath}");
            }
        }

        private static void SetProp(Type type, object obj, string name, object value)
        {
            var p = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            p?.SetValue(obj, value);
        }

        private static void SetField(Type type, object obj, string name, object value)
        {
            var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            f?.SetValue(obj, value);
        }

        private static object GetProp(Type type, object obj, string name)
        {
            var p = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return p?.GetValue(obj);
        }

        private class BundleManifestEntry
        {
            public string FileName { get; set; }
            public string AssetPath { get; set; }
        }
    }
}
