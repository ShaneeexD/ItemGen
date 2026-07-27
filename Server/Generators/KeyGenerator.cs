using System.IO;
using IOPath = System.IO.Path;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ItemGen.Converters;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using ItemGen.Models;

namespace ItemGen.Generators;

public static class KeyGenerator
{
    private const string KeyMechanicalParentId = "5c99f98d86f7745c314214b3";

    public static readonly Dictionary<string, (string? Map, string? BackgroundColor)> RegisteredKeyColors = new();
    private static readonly Dictionary<string, string> MapNameToFile = new()
    {
        ["Customs"] = "Customs",
        ["Factory"] = "Factory",
        ["Woods"] = "Woods",
        ["Shoreline"] = "Shoreline",
        ["Interchange"] = "Interchange",
        ["The Lab"] = "TheLab",
        ["Reserve"] = "Reserve",
        ["Lighthouse"] = "Lighthouse",
        ["Streets of Tarkov"] = "StreetsOfTarkov",
        ["Ground Zero"] = "GroundZero",
        ["The Labyrinth"] = "TheLabyrinth",
    };

    public static int RegisterAll(
        CustomItemService customItemService,
        DatabaseService databaseService,
        IReadOnlyList<KeyDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        var registered = 0;
        var registeredKeysWithMap = new List<(KeyDefinition def, bool success)>();

        foreach (var def in definitions)
        {
            try
            {
                if (RegisterKey(def, customItemService, databaseService, logger))
                {
                    registered++;
                    registeredKeysWithMap.Add((def, true));
                }
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register key '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }

        // Store for late re-application after BetterKeys runs
        foreach (var (def, success) in registeredKeysWithMap)
        {
            if (success)
            {
                RegisteredKeyColors[def.Id] = (def.Map, def.BackgroundColor);
            }
        }

        var mapKeys = registeredKeysWithMap
            .Where(x => x.success && !string.IsNullOrWhiteSpace(x.def.Map) && x.def.Map != "Junk")
            .Select(x => x.def)
            .ToList();
        if (mapKeys.Count > 0)
        {
            PatchBetterKeysDb(mapKeys, databaseService, logger);
        }

        return registered;
    }

    private static bool RegisterKey(
        KeyDefinition def,
        CustomItemService customItemService,
        DatabaseService databaseService,
        ISptLogger<ItemGenPlugin> logger)
    {
        var parentId = ResolveParentId(databaseService, def.BaseTpl);
        var handbookParentId = ResolveHandbookParent(databaseService, def.BaseTpl);

        TemplateItemProperties? overrides = null;
        if (def.Properties.ValueKind != JsonValueKind.Undefined && def.Properties.ValueKind != JsonValueKind.Null)
        {
            overrides = JsonSerializer.Deserialize<TemplateItemProperties>(def.Properties.GetRawText(), new JsonSerializerOptions
            {
                Converters = { new MongoIdJsonConverter(), new JsonStringEnumConverter() },
            });
        }

        overrides ??= new TemplateItemProperties();

        overrides.Name = def.ShortName;
        overrides.ShortName = def.ShortName;
        overrides.Description = def.Description;
        overrides.Weight = def.Weight;

        if (!string.IsNullOrWhiteSpace(def.BackgroundColor))
        {
            overrides.BackgroundColor = def.BackgroundColor;
        }
        else if (!string.IsNullOrWhiteSpace(def.Map))
        {
            var mapColor = ResolveMapColor(def.Map);
            if (!string.IsNullOrWhiteSpace(mapColor))
            {
                overrides.BackgroundColor = mapColor;
            }
        }

        overrides.MaximumNumberOfUsage = def.Uses;
        overrides.KeyIds = def.DoorIds.Count == 0 ? null : def.DoorIds;
        overrides.CanSellOnRagfair = def.CanSellOnRagfair;
        overrides.RarityPvE = def.RarityPvE;

        // Do not override the model via clone properties; custom bundle paths are injected after creation (see VPOAmmo pattern).
        var customPrefabPath = GetPropertyPath(def.Properties, "Prefab");
        var customUsePrefabPath = GetPropertyPath(def.Properties, "UsePrefab");
        overrides.Prefab = null;
        overrides.UsePrefab = null;

        var details = new NewItemFromCloneDetails
        {
            NewId = def.Id,
            ItemTplToClone = def.BaseTpl,
            ParentId = parentId,
            HandbookParentId = handbookParentId,
            HandbookPriceRoubles = def.HandbookPriceRoubles,
            FleaPriceRoubles = def.FleaPriceRoubles,
            OverrideProperties = overrides,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = def.Name,
                    ShortName = def.ShortName,
                    Description = def.Description,
                }
            },
        };

        var result = customItemService.CreateItemFromClone(details);

        if (result.Success == true)
        {
            var items = databaseService.GetItems();
            if (items.TryGetValue(def.Id, out var tpl) && tpl.Properties != null)
            {
                // Re-apply BackgroundColor after creation to ensure it isn't overridden by other mods
                if (!string.IsNullOrWhiteSpace(overrides.BackgroundColor))
                {
                    tpl.Properties.BackgroundColor = overrides.BackgroundColor;
                }

                if (!string.IsNullOrWhiteSpace(customPrefabPath) && tpl.Properties.Prefab != null)
                {
                    tpl.Properties.Prefab.Path = customPrefabPath;
                }

                if (!string.IsNullOrWhiteSpace(customUsePrefabPath) && tpl.Properties.UsePrefab != null)
                {
                    tpl.Properties.UsePrefab.Path = customUsePrefabPath;
                }

            }
            else
            {
                logger.LogWithColor(
                    $"[ItemGen] Could not inject bundle path for key '{def.Name}' - item not found after clone.",
                    LogTextColor.Yellow);
            }

            return true;
        }

        logger.LogWithColor(
            $"[ItemGen] CreateItemFromClone reported failure for key '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
            LogTextColor.Yellow);
        return false;
    }

    private static string? GetPropertyPath(JsonElement properties, string propertyName)
    {
        if (properties.ValueKind == JsonValueKind.Undefined || properties.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (properties.TryGetProperty(propertyName, out var prefab)
            && prefab.ValueKind == JsonValueKind.Object
            && prefab.TryGetProperty("path", out var path)
            && path.ValueKind == JsonValueKind.String)
        {
            return path.GetString();
        }

        return null;
    }

    private static string ResolveParentId(DatabaseService databaseService, string baseTpl)
    {
        var items = databaseService.GetItems();
        if (items.TryGetValue(baseTpl, out var baseItem) && !string.IsNullOrWhiteSpace(baseItem.Parent))
        {
            return baseItem.Parent;
        }
        return KeyMechanicalParentId;
    }

    private static string ResolveHandbookParent(DatabaseService databaseService, string baseTpl)
    {
        var items = databaseService.GetItems();
        if (items.TryGetValue(baseTpl, out var baseItem))
        {
            var handbook = databaseService.GetHandbook().Items.FirstOrDefault(h => h.Id == baseTpl);
            if (handbook != null && !string.IsNullOrWhiteSpace(handbook.ParentId))
            {
                return handbook.ParentId;
            }
        }
        return "5b47574386f77428ca22b33f"; // Keys
    }

    private static readonly HashSet<string> ValidBkLootTypes = new()
    {
        "Jacket", "Toolbox", "Drawer", "Crate", "SportBag", "Medbag",
        "PC", "Grenade", "DeadBody", "Ammo", "Weapon", "CashReg",
        "Safe", "LooseVals", "LooseCash", "LooseLoot", "LooseGear"
    };

    private static void PatchBetterKeysDb(List<KeyDefinition> mapKeys, DatabaseService databaseService, ISptLogger<ItemGenPlugin> logger)
    {
        var modsDir = IOPath.Combine(Directory.GetCurrentDirectory(), "user", "mods");
        if (!Directory.Exists(modsDir))
            return;

        string? bkDbDir = null;
        foreach (var modDir in Directory.GetDirectories(modsDir))
        {
            var constantsPath = IOPath.Combine(modDir, "db", "_constants.json");
            if (File.Exists(constantsPath))
            {
                bkDbDir = IOPath.Combine(modDir, "db");
                break;
            }
        }

        if (bkDbDir == null)
            return;

        foreach (var def in mapKeys)
        {
            if (!MapNameToFile.TryGetValue(def.Map!, out var fileName))
                continue;

            var dbFilePath = IOPath.Combine(bkDbDir, $"{fileName}.json");
            try
            {
                var json = File.Exists(dbFilePath) ? File.ReadAllText(dbFilePath) : "{\"Keys\":{}}";
                var root = JsonNode.Parse(json) ?? new JsonObject();
                var keysObj = root["Keys"] as JsonObject ?? new JsonObject();
                root["Keys"] = keysObj;

                // Filter loot types to only valid BetterKeys locale keys
                var validLoot = def.BkLoot.Where(l => ValidBkLootTypes.Contains(l)).ToList();

                // Filter quest IDs to only those that exist in the database
                // (BetterKeys does locale[$"{q} name"] which throws if the quest is unknown)
                var quests = databaseService.GetQuests();
                var validQuests = def.BkQuests.Where(q => quests.ContainsKey(q)).ToList();
                if (validQuests.Count != def.BkQuests.Count)
                {
                    var skipped = def.BkQuests.Except(validQuests);
                    logger.LogWithColor(
                        $"[ItemGen] Skipped invalid quest ID(s) for key '{def.Name}': {string.Join(", ", skipped)}",
                        LogTextColor.Yellow);
                }

                // Always update (handles both new and existing keys)
                keysObj[def.Id] = new JsonObject
                {
                    ["Tips"] = new JsonArray(def.BkTips.Select(t => (JsonNode)t).ToArray()),
                    ["Extract"] = new JsonArray(def.BkExtracts.Select(t => (JsonNode)t).ToArray()),
                    ["Quests"] = new JsonArray(validQuests.Select(t => (JsonNode)t).ToArray()),
                    ["Loot"] = new JsonArray(validLoot.Select(t => (JsonNode)t).ToArray()),
                };

                var newJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dbFilePath, newJson);
            }
            catch
            {
            }
        }
    }

    private static readonly Dictionary<string, string> DefaultMapColors = new()
    {
        ["Junk"] = "black",
        ["Factory"] = "green",
        ["Customs"] = "blue",
        ["Woods"] = "tracerGreen",
        ["Shoreline"] = "orange",
        ["Interchange"] = "tracerRed",
        ["The Lab"] = "tracerYellow",
        ["Reserve"] = "violet",
        ["Lighthouse"] = "red",
        ["Streets of Tarkov"] = "green",
        ["Ground Zero"] = "blue",
        ["The Labyrinth"] = "orange",
    };

    public static Dictionary<string, string> GetDefaultMapColors() => new(DefaultMapColors);

    private static Dictionary<string, string>? _cachedBetterKeysColors;

 
    private static string? ResolveMapColor(string mapName)
    {
        if (_cachedBetterKeysColors == null)
        {
            _cachedBetterKeysColors = LoadBetterKeysColors();
        }

        return _cachedBetterKeysColors.TryGetValue(mapName, out var color) ? color : null;
    }

    private static Dictionary<string, string> LoadBetterKeysColors()
    {
        // Try to read BetterKeys' config from user/mods/ directories
        var modsDir = IOPath.Combine(Directory.GetCurrentDirectory(), "user", "mods");
        if (Directory.Exists(modsDir))
        {
            foreach (var modDir in Directory.GetDirectories(modsDir))
            {
                // Look for config/config.jsonc or config/configuser.jsonc
                var configPath = IOPath.Combine(modDir, "config", "configuser.jsonc");
                if (!File.Exists(configPath))
                    configPath = IOPath.Combine(modDir, "config", "config.jsonc");

                if (!File.Exists(configPath))
                    continue;

                try
                {
                    var json = File.ReadAllText(configPath);
                    // Strip JSONC comments before parsing
                    json = Regex.Replace(json, @"//.*?$|/\*.*?\*/", "", RegexOptions.Singleline);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("BackgroundColors", out var bgColors))
                    {
                        var result = new Dictionary<string, string>();
                        foreach (var prop in bgColors.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                result[prop.Name] = prop.Value.GetString() ?? "";
                            }
                        }
                        if (result.Count > 0)
                        {
                            return result;
                        }
                    }
                }
                catch
                {

                }
            }
        }

        // Fall back to defaults
        return new Dictionary<string, string>(DefaultMapColors);
    }
}
