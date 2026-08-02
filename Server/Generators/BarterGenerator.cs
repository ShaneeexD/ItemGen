using System.Text.Json;
using System.Text.Json.Serialization;
using ItemGen.Converters;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.Modding.Custom;
using ItemGen.Models;
using SpectreColor = Spectre.Console.Color;

namespace ItemGen.Generators;

public static class BarterGenerator
{
    private const string BarterParentId = "5448eb774bdc2d0a728b4567";
    private const string BarterHandbookParentId = "5b47574386f77428ca22b33e";

    public static int RegisterAll(
        CustomItemService customItemService,
        TemplateTable templateTable,
        IReadOnlyList<BarterDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        var registered = 0;
        foreach (var def in definitions)
        {
            try
            {
                if (RegisterBarter(def, customItemService, templateTable, logger))
                {
                    registered++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register barter item '{def.Name}': {ex.Message}", SpectreColor.Red);
            }
        }

        return registered;
    }

    private static bool RegisterBarter(
        BarterDefinition def,
        CustomItemService customItemService,
        TemplateTable templateTable,
        ISptLogger<ItemGenPlugin> logger)
    {
        var parentId = ResolveParentId(templateTable, def.BaseTpl, def.Parent);
        var handbookParentId = ResolveHandbookParent(templateTable, def.BaseTpl, def.HandbookParentId);

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
            NewItemName = def.Name,
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
            var items = templateTable.Items;
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
                    SpectreColor.Yellow);
            }

            return true;
        }

        logger.LogWithColor(
            $"[ItemGen] CreateItemFromClone reported failure for barter item '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
            SpectreColor.Yellow);
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

    private static string ResolveParentId(TemplateTable templateTable, string baseTpl, string configuredParent)
    {
        if (!string.IsNullOrWhiteSpace(configuredParent) && configuredParent != BarterParentId)
        {
            return configuredParent;
        }

        var items = templateTable.Items;
        if (items.TryGetValue(baseTpl, out var baseItem) && !string.IsNullOrWhiteSpace(baseItem.Parent))
        {
            return baseItem.Parent;
        }
        return BarterParentId;
    }

    private static string ResolveHandbookParent(TemplateTable templateTable, string baseTpl, string configuredHandbookParent)
    {
        if (!string.IsNullOrWhiteSpace(configuredHandbookParent) && configuredHandbookParent != BarterHandbookParentId)
        {
            return configuredHandbookParent;
        }

        var items = templateTable.Items;
        if (items.TryGetValue(baseTpl, out var baseItem))
        {
            var handbook = templateTable.Handbook.Items.FirstOrDefault(h => h.Id == baseTpl);
            if (handbook != null && !string.IsNullOrWhiteSpace(handbook.ParentId))
            {
                return handbook.ParentId;
            }
        }
        return BarterHandbookParentId;
    }
}
