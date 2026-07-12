using System.Text.Json;
using System.Text.Json.Serialization;
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

public static class MedKitGenerator
{
    private const string MedKitParentId = "5448f39d4bdc2d0a728b4568";
    private const string MedKitHandbookParentId = "5b47574386f77428ca22b338";

    public static void RegisterAll(
        CustomItemService customItemService,
        DatabaseService databaseService,
        IReadOnlyList<MedKitDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        foreach (var def in definitions)
        {
            try
            {
                RegisterMedKit(def, customItemService, databaseService, logger);
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register medkit '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }
    }

    private static void RegisterMedKit(
        MedKitDefinition def,
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

        overrides.ItemSound = def.ItemSound;
        overrides.StimulatorBuffs = string.Empty;
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
            logger.LogWithColor($"[ItemGen] Registered medkit: {def.Name} ({def.Id})", LogTextColor.Green);

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
                    $"[ItemGen] Could not inject bundle path for medkit '{def.Name}' - item not found after clone.",
                    LogTextColor.Yellow);
            }
        }
        else
        {
            logger.LogWithColor(
                $"[ItemGen] CreateItemFromClone reported failure for medkit '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
                LogTextColor.Yellow);
        }
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
        return MedKitParentId;
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
        return MedKitHandbookParentId;
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
