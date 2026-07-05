using System.Text.Json;
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

    public static void RegisterAll(
        CustomItemService customItemService,
        DatabaseService databaseService,
        IReadOnlyList<KeyDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        foreach (var def in definitions)
        {
            try
            {
                RegisterKey(def, customItemService, databaseService, logger);
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register key '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }
    }

    private static void RegisterKey(
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
            logger.LogWithColor($"[ItemGen] Registered key: {def.Name} ({def.Id})", LogTextColor.Green);

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
                        $"[ItemGen] Could not inject bundle path for key '{def.Name}' - item not found after clone.",
                        LogTextColor.Yellow);
                }
            }
        }
        else
        {
            logger.LogWithColor(
                $"[ItemGen] CreateItemFromClone reported failure for key '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
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
}
