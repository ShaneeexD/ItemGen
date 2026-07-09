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
using MongoId = SPTarkov.Server.Core.Models.Common.MongoId;

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
            logger.LogWithColor($"[ItemGen] Registered container: {def.Name} ({def.Id})", LogTextColor.Green);

            PatchSafeContainerFilters(databaseService, def, logger);

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
                        $"[ItemGen] Could not inject bundle path for container '{def.Name}' - item not found after clone.",
                        LogTextColor.Yellow);
                }
            }
        }
        else
        {
            logger.LogWithColor(
                $"[ItemGen] CreateItemFromClone reported failure for container '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
                LogTextColor.Yellow);
        }
    }

    private static readonly string[] DefaultSafeContainerIds =
    {
        "544a11ac4bdc2d470e8b456a", // Alpha
        "5857a8b324597729ab0a0e7d", // Beta
        "5857a8bc2459772bad15db29", // Gamma
        "59db794186f77448bc595262", // Epsilon
        "5c093ca986f7740a1867ab12", // Kappa
        "676008db84e242067d0dc4c9", // Kappa (Desecrated)
        "664a55d84a90fc2c8a6305c9", // Theta
        "60b0f9326c1d6c524c5ce3d5", // Waist pouch
    };

    private static void PatchSafeContainerFilters(
        DatabaseService databaseService,
        ContainerDefinition def,
        ISptLogger<ItemGenPlugin> logger)
    {
        if (def.SafeContainerMode == SafeContainerMode.Include && def.SafeContainerIds.Count == 0)
        {
            logger.LogWithColor($"[ItemGen] Container '{def.Name}' has safeContainerMode 'include' but no IDs; skipping safe-container patch.", LogTextColor.Yellow);
            return;
        }

        var targetIds = def.SafeContainerMode switch
        {
            SafeContainerMode.All => DefaultSafeContainerIds.ToList(),
            SafeContainerMode.Include => def.SafeContainerIds,
            SafeContainerMode.Exclude => DefaultSafeContainerIds.Except(def.SafeContainerIds).ToList(),
            _ => DefaultSafeContainerIds.ToList(),
        };

        var itemId = new MongoId(def.Id);
        var items = databaseService.GetItems();
        var patched = 0;
        foreach (var safeId in targetIds)
        {
            if (!items.TryGetValue(safeId, out var safeContainer) || safeContainer.Properties?.Grids == null)
            {
                continue;
            }

            // SPT only checks the first grid's first filter when deciding if an item is allowed.
            var firstGrid = safeContainer.Properties.Grids.FirstOrDefault();
            var firstFilter = firstGrid?.Properties?.Filters?.FirstOrDefault();
            if (firstFilter == null)
            {
                continue;
            }

            firstFilter.ExcludedFilter ??= new HashSet<MongoId>();
            firstFilter.ExcludedFilter.Remove(itemId);

            firstFilter.Filter ??= new HashSet<MongoId>();
            if (!firstFilter.Filter.Contains(itemId))
            {
                firstFilter.Filter.Add(itemId);
            }

            patched++;
        }

        if (patched > 0)
        {
            logger.LogWithColor($"[ItemGen] Patched {patched} safe container(s) to allow '{def.Name}'.", LogTextColor.Green);
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
