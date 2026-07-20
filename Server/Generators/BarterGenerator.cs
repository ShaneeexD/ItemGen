using System.Text.Json;
using System.Text.Json.Serialization;
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

public static class BarterGenerator
{
    private const string BarterParentId = "5448eb774bdc2d0a728b4567";
    private const string BarterHandbookParentId = "5b47574386f77428ca22b33e";

    public static int RegisterAll(
        CustomItemService customItemService,
        DatabaseService databaseService,
        IReadOnlyList<BarterDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        var registered = 0;
        foreach (var def in definitions)
        {
            try
            {
                if (RegisterBarter(def, customItemService, databaseService, logger))
                {
                    registered++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register barter item '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }

        return registered;
    }

    private static bool RegisterBarter(
        BarterDefinition def,
        CustomItemService customItemService,
        DatabaseService databaseService,
        ISptLogger<ItemGenPlugin> logger)
    {
        var parentId = ResolveParentId(databaseService, def.BaseTpl, def.Parent);
        var handbookParentId = ResolveHandbookParent(databaseService, def.BaseTpl, def.HandbookParentId);

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
            var items = databaseService.GetItems();
            if (items.TryGetValue(def.Id, out var tpl) && tpl.Properties != null)
            {
                tpl.Properties.Width = def.Width;
                tpl.Properties.Height = def.Height;

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
                    $"[ItemGen] Could not inject bundle path for barter item '{def.Name}' - item not found after clone.",
                    LogTextColor.Yellow);
            }

            return true;
        }

        logger.LogWithColor(
            $"[ItemGen] CreateItemFromClone reported failure for barter item '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
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

    private static string ResolveParentId(DatabaseService databaseService, string baseTpl, string configuredParent)
    {
        if (!string.IsNullOrWhiteSpace(configuredParent) && configuredParent != BarterParentId)
        {
            return configuredParent;
        }

        var items = databaseService.GetItems();
        if (items.TryGetValue(baseTpl, out var baseItem) && !string.IsNullOrWhiteSpace(baseItem.Parent))
        {
            return baseItem.Parent;
        }
        return BarterParentId;
    }

    private static string ResolveHandbookParent(DatabaseService databaseService, string baseTpl, string configuredHandbookParent)
    {
        if (!string.IsNullOrWhiteSpace(configuredHandbookParent) && configuredHandbookParent != BarterHandbookParentId)
        {
            return configuredHandbookParent;
        }

        var items = databaseService.GetItems();
        if (items.TryGetValue(baseTpl, out var baseItem))
        {
            var handbook = databaseService.GetHandbook().Items.FirstOrDefault(h => h.Id == baseTpl);
            if (handbook != null && !string.IsNullOrWhiteSpace(handbook.ParentId))
            {
                return handbook.ParentId;
            }
        }
        return BarterHandbookParentId;
    }
}
