using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ItemGen.Models;

namespace ItemGen.Services;

// Adds hideout Workbench production recipes for custom ItemGen items.
public static class CraftingManager
{
    public static int RegisterAll(
        DatabaseService databaseService,
        IReadOnlyList<ItemDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        var hideout = databaseService.GetHideout();
        if (hideout?.Production?.Recipes == null)
        {
            logger.LogWithColor("[ItemGen] Could not access hideout production recipes. Crafting will not be added.", LogTextColor.Red);
            return 0;
        }

        var productions = hideout.Production.Recipes;
        var added = 0;
        var failed = 0;

        foreach (var def in definitions)
        {
            if (def.Crafting is not { Enabled: true })
                continue;

            try
            {
                if (AddRecipe(def.Id, def.Name, def.Crafting, productions))
                    added++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWithColor($"[ItemGen] Failed to add crafting recipe for '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }

        logger.LogWithColor($"[ItemGen] Added {added} crafting recipe(s).", LogTextColor.Green);
        if (failed > 0)
            logger.LogWithColor($"[ItemGen] {failed} crafting recipe(s) failed.", LogTextColor.Red);

        return added;
    }

    private static bool AddRecipe(
        string itemId,
        string itemName,
        CraftingEntry crafting,
        List<HideoutProduction> productions)
    {
        var requirements = new List<Requirement>
        {
            new Requirement
            {
                Type = "Area",
                AreaType = (int)HideoutAreas.Workbench,
                RequiredLevel = crafting.WorkbenchLevel,
            }
        };

        foreach (var req in crafting.Requirements)
        {
            requirements.Add(new Requirement
            {
                Type = "Item",
                TemplateId = new MongoId(req.Tpl),
                Count = req.Count,
                IsEncoded = false,
            });
        }

        if (productions.Any(p => p.Id == itemId))
        {
            return false;
        }

        var recipe = new HideoutProduction
        {
            Id = new MongoId(itemId),
            AreaType = HideoutAreas.Workbench,
            Requirements = requirements,
            ProductionTime = crafting.CraftTimeSeconds,
            EndProduct = new MongoId(itemId),
            Count = crafting.OutputCount,
            ProductionLimitCount = 0,
            NeedFuelForAllProductionTime = false,
            Locked = false,
            IsEncoded = false,
            Continuous = false,
        };

        productions.Add(recipe);
        return true;
    }
}
