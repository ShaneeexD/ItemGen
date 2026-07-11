using System.Reflection;
using System.Text.Json;
using ItemGen.Converters;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using ItemGen.Models;

namespace ItemGen.Generators;

public static class StimGenerator
{
    private const string StimParentId = "5448f3a64bdc2d60728b456a";

    public static void RegisterAll(
        CustomItemService customItemService,
        DatabaseService databaseService,
        IReadOnlyList<StimDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        foreach (var def in definitions)
        {
            try
            {
                RegisterStim(def, customItemService, databaseService, logger);
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register stim '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }
    }

    private static void RegisterStim(
        StimDefinition def,
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
                Converters = { new MongoIdJsonConverter() },
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
        var buffSetKey = !string.IsNullOrWhiteSpace(def.StimulatorBuffs)
            ? def.StimulatorBuffs
            : $"Buffs_ItemGen_{def.Id}";

        if (def.MaxBodyPartsToHeal > 0)
        {
            buffSetKey = $"{buffSetKey}_MaxBodyParts_{def.MaxBodyPartsToHeal}";
        }

        overrides.ItemSound = def.ItemSound;
        overrides.StimulatorBuffs = buffSetKey;
        overrides.EffectsHealth = ConvertEffectsHealth(def.EffectsHealth);
        overrides.EffectsDamage = ConvertEffectsDamage(def.EffectsDamage);
        overrides.BodyPartPriority = null;
        overrides.FoodEffectType = null;
        overrides.MedEffectType = def.MedEffectType;
        overrides.MedUseTime = def.MedUseTime;
        overrides.MaxHpResource = def.MaxHpResource;
        overrides.HpResourceRate = def.HpResourceRate;
        overrides.StackMaxSize = def.StackMaxSize;
        overrides.Width = def.Width;
        overrides.Height = def.Height;
        overrides.CanSellOnRagfair = def.CanSellOnRagfair;
        overrides.RarityPvE = def.RarityPvE;

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
            logger.LogWithColor($"[ItemGen] Registered stim: {def.Name} ({def.Id})", LogTextColor.Green);

            PatchStimulatorBuffs(databaseService, def, buffSetKey, logger);

            if (!string.IsNullOrWhiteSpace(customPrefabPath) || !string.IsNullOrWhiteSpace(customUsePrefabPath))
            {
                var items = databaseService.GetItems();
                if (items.TryGetValue(def.Id, out var tpl) && tpl.Properties != null)
                {
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
                        $"[ItemGen] Could not inject bundle path for stim '{def.Name}' - item not found after clone.",
                        LogTextColor.Yellow);
                }
            }
        }
        else
        {
            logger.LogWithColor(
                $"[ItemGen] CreateItemFromClone reported failure for stim '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
                LogTextColor.Yellow);
        }
    }

    private static void PatchStimulatorBuffs(
        DatabaseService databaseService,
        StimDefinition def,
        string buffSetKey,
        ISptLogger<ItemGenPlugin> logger)
    {
        var needsBuffKey = def.CustomBuffs.Count > 0 || def.MaxBodyPartsToHeal > 0;
        if (!needsBuffKey)
        {
            return;
        }

        try
        {
            var globals = databaseService.GetGlobals();
            var stimulatorBuffs = FindStimulatorBuffs(globals, logger, def.Name);

            if (stimulatorBuffs == null)
            {
                logger.LogWithColor($"[ItemGen] Could not patch stimulator buffs for '{def.Name}' - globals structure not found.", LogTextColor.Yellow);
                return;
            }

            List<Buff> buffs;
            if (def.CustomBuffs.Count > 0)
            {
                buffs = def.CustomBuffs.Select(b => new Buff
                {
                    BuffType = b.BuffType,
                    Chance = b.Chance,
                    Delay = b.Delay,
                    Duration = b.Duration,
                    Value = b.Value,
                    AbsoluteValue = b.AbsoluteValue,
                    SkillName = b.BuffType == "SkillRate" ? b.SkillName! : null!,
                }).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(def.StimulatorBuffs) && TryGetExistingBuffs(stimulatorBuffs, def.StimulatorBuffs, out var existingBuffs))
            {
                buffs = existingBuffs;
            }
            else
            {
                buffs = new List<Buff>();
            }

            SetDictionaryValue(stimulatorBuffs, buffSetKey, buffs);

            var items = databaseService.GetItems();
            if (items.TryGetValue(def.Id, out var tpl))
            {
                tpl.Properties ??= new TemplateItemProperties();
                tpl.Properties.StimulatorBuffs = buffSetKey;
                tpl.Properties.EffectsHealth = null;
                tpl.Properties.EffectsDamage = null;
                tpl.Properties.BodyPartPriority = null;
                tpl.Properties.FoodEffectType = null;
            }

        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[ItemGen] Failed to patch stimulator buffs for '{def.Name}': {ex.Message}", LogTextColor.Yellow);
        }
    }

    private static object? FindStimulatorBuffs(object globals, ISptLogger<ItemGenPlugin> logger, string stimName)
    {
        var globalType = globals.GetType();

        var config = globalType.GetProperty("Config")?.GetValue(globals)
                     ?? globalType.GetProperty("Configuration")?.GetValue(globals)
                     ?? globalType.GetProperty("config")?.GetValue(globals);

        var candidates = new (string path, object? value)[]
        {
            ("Config.Health.Effects.Stimulator.Buffs", TryGetPath(config, "Health", "Effects", "Stimulator", "Buffs")),
            ("Config.Health.Effects.Stimulator.BuffSets", TryGetPath(config, "Health", "Effects", "Stimulator", "BuffSets")),
            ("Config.HealthEffects.StimulatorBuffs", TryGetPath(config, "HealthEffects", "StimulatorBuffs")),
            ("Globals.Health.Effects.Stimulator.Buffs", TryGetPath(globals, "Health", "Effects", "Stimulator", "Buffs")),
            ("Globals.Health.Effects.Stimulator.BuffSets", TryGetPath(globals, "Health", "Effects", "Stimulator", "BuffSets")),
            ("Globals.HealthEffects.StimulatorBuffs", TryGetPath(globals, "HealthEffects", "StimulatorBuffs")),
        };

        foreach (var (path, value) in candidates)
        {
            if (value != null && IsDictionaryLike(value))
                return value;
        }

        // Try ExtensionData["HealthEffects"]["StimulatorBuffs"] if available
        var extensionData = globalType.GetProperty("ExtensionData")?.GetValue(globals)
            ?? globalType.GetProperty("extensionData")?.GetValue(globals);
        if (extensionData != null)
        {
            var healthEffects = GetIndexerValue(extensionData, "HealthEffects");
            if (healthEffects != null)
            {
                var stimBuffs = GetIndexerValue(healthEffects, "StimulatorBuffs");
                if (stimBuffs != null && IsDictionaryLike(stimBuffs))
                    return stimBuffs;
            }
        }

        // Last resort: recursive search by property name
        var found = FindByPropertyName(globals, "StimulatorBuffs", 8);
        if (found != null && IsDictionaryLike(found))
            return found;

        return null;
    }

    private static object? GetIndexerValue(object dictionary, string key)
    {
        var type = dictionary.GetType();
        var indexer = type.GetProperties()
            .FirstOrDefault(p => p.GetIndexParameters().Length == 1 && p.GetIndexParameters()[0].ParameterType == typeof(string));
        if (indexer != null)
            return indexer.GetValue(dictionary, new object[] { key });

        var getMethod = type.GetMethod("get_Item", new[] { typeof(string) })
            ?? type.GetMethod("GetValueOrDefault", new[] { typeof(string) });
        return getMethod?.Invoke(dictionary, new object[] { key });
    }

    private static bool IsDictionaryLike(object value)
    {
        var type = value.GetType();
        if (value is System.Collections.IDictionary) return true;
        if (type.IsGenericType)
        {
            var gtd = type.GetGenericTypeDefinition();
            if (gtd == typeof(Dictionary<,>) || gtd == typeof(System.Collections.Generic.IDictionary<,>))
                return true;
        }
        return false;
    }

    private static object? FindByPropertyName(object root, string name, int maxDepth)
    {
        var visited = new HashSet<object>();
        return FindByPropertyNameRecursive(root, name, maxDepth, visited);
    }

    private static object? FindByPropertyNameRecursive(object? current, string name, int depth, HashSet<object> visited)
    {
        if (current == null || depth <= 0) return null;
        if (!visited.Add(current)) return null;

        var type = current.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (prop.Name == name)
            {
                try { return prop.GetValue(current); } catch { continue; }
            }
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
            object? child;
            try { child = prop.GetValue(current); } catch { continue; }
            if (child == null || child is string || child.GetType().IsPrimitive) continue;

            var found = FindByPropertyNameRecursive(child, name, depth - 1, visited);
            if (found != null) return found;
        }

        return null;
    }

    private static object? TryGetPath(object? root, params string[] path)
    {
        var current = root;
        foreach (var name in path)
        {
            if (current == null) return null;
            var prop = current.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            current = prop?.GetValue(current);
        }
        return current;
    }

    private static bool TryGetExistingBuffs(object dictionary, string key, out List<Buff> existingBuffs)
    {
        existingBuffs = new List<Buff>();
        try
        {
            if (dictionary is System.Collections.IDictionary idict && idict[key] is not null)
            {
                var value = idict[key];
                if (value is IEnumerable<Buff> buffEnumerable)
                {
                    existingBuffs = buffEnumerable.ToList();
                    return true;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is Buff buff)
                        {
                            existingBuffs.Add(buff);
                        }
                    }
                    return existingBuffs.Count > 0;
                }
            }
        }
        catch { }
        return false;
    }

    private static void SetDictionaryValue(object dictionary, string key, object value)
    {
        if (dictionary is System.Collections.IDictionary idict)
        {
            idict[key] = value;
            return;
        }

        var type = dictionary.GetType();
        var indexer = type.GetProperties()
            .Where(p => p.GetIndexParameters().Length == 1 && p.GetIndexParameters()[0].ParameterType == typeof(string))
            .FirstOrDefault();
        if (indexer != null)
        {
            indexer.SetValue(dictionary, value, new object[] { key });
            return;
        }

        // Try generic Dictionary<string, TValue> setter via indexer or Add method
        var addMethod = type.GetMethod("Add", new[] { typeof(string), value.GetType() })
            ?? type.GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(string));
        addMethod?.Invoke(dictionary, new object[] { key, value });
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
        return StimParentId;
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
        return "5b47574386f77428ca22b33a";
    }

    private static Dictionary<HealthFactor, EffectsHealthProperties>? ConvertEffectsHealth(
        Dictionary<string, EffectsHealthProperties>? source)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<HealthFactor, EffectsHealthProperties>();
        foreach (var (key, value) in source)
        {
            if (value is null || !Enum.TryParse<HealthFactor>(key, true, out var healthFactor))
            {
                continue;
            }

            result[healthFactor] = value;
        }

        return result.Count > 0 ? result : null;
    }

    private static Dictionary<DamageEffectType, EffectsDamageProperties>? ConvertEffectsDamage(
        Dictionary<string, EffectsDamageProperties>? source)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<DamageEffectType, EffectsDamageProperties>();
        foreach (var (key, value) in source)
        {
            if (value is null || !Enum.TryParse<DamageEffectType>(key, true, out var damageEffectType))
            {
                continue;
            }

            result[damageEffectType] = value;
        }

        return result.Count > 0 ? result : null;
    }
}
