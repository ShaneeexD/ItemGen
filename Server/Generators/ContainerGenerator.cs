using System.Collections.Generic;
using System.Linq;
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

public static class ContainerGenerator
{
    public static void RegisterAll(
        CustomItemService customItemService,
        DatabaseService databaseService,
        IReadOnlyList<ContainerDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        foreach (var def in definitions)
        {
            try
            {
                RegisterContainer(def, customItemService, databaseService, logger);
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register container '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }
    }

    private static void RegisterContainer(
        ContainerDefinition def,
        CustomItemService customItemService,
        DatabaseService databaseService,
        ISptLogger<ItemGenPlugin> logger)
    {
        var parentId = ResolveParentId(databaseService, def);
        var handbookParentId = ResolveHandbookParent(databaseService, def);

        TemplateItemProperties? overrides = null;
        if (def.Properties.ValueKind != JsonValueKind.Undefined && def.Properties.ValueKind != JsonValueKind.Null)
        {
            overrides = JsonSerializer.Deserialize<TemplateItemProperties>(def.Properties.GetRawText(), new JsonSerializerOptions
            {
                Converters = { new MongoIdJsonConverter() },
            });
        }

        overrides ??= new TemplateItemProperties();

        // Ensure core identity fields from the editor are applied over the cloned vanilla props.
        overrides.Name = def.ShortName;
        overrides.ShortName = def.ShortName;
        overrides.Description = def.Description;
        overrides.Weight = def.Weight;
        if (!string.IsNullOrWhiteSpace(def.BackgroundColor))
        {
            overrides.BackgroundColor = def.BackgroundColor;
        }

        // Do not override the model if no custom path is provided.
        if (overrides.Prefab is { Path: "" or null })
        {
            overrides.Prefab = null;
        }
        if (overrides.UsePrefab is { Path: "" or null })
        {
            overrides.UsePrefab = null;
        }

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
            logger.LogWithColor($"[ItemGen] Registered container: {def.Name} ({def.Id})", LogTextColor.Green);
        }
        else
        {
            logger.LogWithColor(
                $"[ItemGen] CreateItemFromClone reported failure for container '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
                LogTextColor.Yellow);
        }
    }

    private static string ResolveParentId(DatabaseService databaseService, ContainerDefinition def)
    {
        if (!string.IsNullOrWhiteSpace(def.Parent))
        {
            return def.Parent;
        }

        var items = databaseService.GetItems();
        if (items.TryGetValue(def.BaseTpl, out var baseItem) && !string.IsNullOrWhiteSpace(baseItem.Parent))
        {
            return baseItem.Parent;
        }
        return "5795f317245977243854e041"; // SimpleContainer
    }

    private static string ResolveHandbookParent(DatabaseService databaseService, ContainerDefinition def)
    {
        if (!string.IsNullOrWhiteSpace(def.HandbookParentId))
        {
            return def.HandbookParentId;
        }

        var items = databaseService.GetItems();
        if (items.TryGetValue(def.BaseTpl, out var baseItem))
        {
            var handbook = databaseService.GetHandbook().Items.FirstOrDefault(h => h.Id == def.BaseTpl);
            if (handbook != null && !string.IsNullOrWhiteSpace(handbook.ParentId))
            {
                return handbook.ParentId;
            }
        }
        return "5b5f6fa186f77409407a7eb7"; // Containers
    }
}
