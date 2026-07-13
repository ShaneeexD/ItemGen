using System.Linq;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Json;
using ItemGen.Models;

namespace ItemGen.Services;

// Injects custom ItemGen items into container loot distributions across all locations.
public static class LootInjector
{
    private static readonly Dictionary<string, int> RarityProbabilities = new()
    {
        ["Common"] = 10000,
        ["Rare"] = 5000,
        ["SuperRare"] = 1000,
        ["NotExists"] = 0,
    };

    private record LootInjectionDefinition(
        string Name,
        IReadOnlyList<string> ContainerIds,
        IReadOnlyList<string> ItemsToInject,
        int Probability);

    public static int InjectAll(
        DatabaseService databaseService,
        IReadOnlyList<ItemDefinition> items,
        ISptLogger<ItemGenPlugin> logger,
        bool debug = false)
    {
        var locations = databaseService.GetLocations();
        if (locations == null)
        {
            logger.LogWithColor("[ItemGen] No locations found in database, skipping loot injection.", LogTextColor.Yellow);
            return 0;
        }

        var locationDictionary = locations.GetDictionary();
        var processedDefinitions = BuildInjectionDefinitions(items, logger);
        if (processedDefinitions.Count == 0)
        {
            logger.LogWithColor("[ItemGen] No items have loot injection enabled, skipping.", LogTextColor.Gray);
            return 0;
        }

        int locationCount = 0;
        foreach (var location in locationDictionary.Values)
        {
            if (location.StaticLoot == null) continue;
            location.StaticLoot.AddTransformer(staticLoot =>
                TransformStaticLoot(staticLoot, processedDefinitions, logger, debug));
            locationCount++;
        }

        logger.LogWithColor(
            $"[ItemGen] Registered loot injection transformer for {locationCount} location(s) covering {processedDefinitions.Count} item definition(s).",
            LogTextColor.Green);

        foreach (var def in processedDefinitions)
        {
            logger.LogWithColor(
                $"[ItemGen] {def.Name}: items [{string.Join(", ", def.ItemsToInject)}] -> containers [{string.Join(", ", def.ContainerIds)}] at probability {def.Probability}.",
                LogTextColor.Gray);
        }

        return processedDefinitions.Count;
    }

    private static List<LootInjectionDefinition> BuildInjectionDefinitions(
        IReadOnlyList<ItemDefinition> items,
        ISptLogger<ItemGenPlugin> logger)
    {
        var result = new List<LootInjectionDefinition>();
        foreach (var item in items)
        {
            if (item.Loot?.Enabled != true || item.Loot.ContainerIds.Count == 0)
            {
                continue;
            }

            var probability = RarityProbabilities.GetValueOrDefault(item.Loot.Rarity, 5000);
            if (probability > 0)
            {
                result.Add(new LootInjectionDefinition(
                    item.Name, item.Loot.ContainerIds, [item.Id], probability));
            }
        }

        return result;
    }

    private static Dictionary<MongoId, StaticLootDetails>? TransformStaticLoot(
        Dictionary<MongoId, StaticLootDetails>? staticLoot,
        IReadOnlyList<LootInjectionDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger,
        bool debug)
    {
        if (staticLoot == null) return staticLoot;

        var injected = 0;
        var containersTouched = new HashSet<string>();
        var containersNotFound = new HashSet<string>();

        foreach (var def in definitions)
        {
            foreach (var containerId in def.ContainerIds)
            {
                if (!staticLoot.TryGetValue(containerId, out var containerLoot) || containerLoot == null)
                {
                    containersNotFound.Add(containerId);
                    continue;
                }

                var distribution = containerLoot.ItemDistribution?.ToList();
                if (distribution == null) continue;

                containersTouched.Add(containerId);

                foreach (var itemId in def.ItemsToInject)
                {
                    var mongoId = new MongoId(itemId);
                    var existing = distribution.FirstOrDefault(d => d.Tpl == mongoId);
                    if (existing != null)
                    {
                        existing.RelativeProbability = def.Probability;
                        if (debug)
                            logger.LogWithColor(
                                $"[ItemGen][Debug] Updated '{itemId}' probability in '{containerId}' to {def.Probability}.",
                                LogTextColor.Gray);
                    }
                    else
                    {
                        distribution.Add(new ItemDistribution { Tpl = mongoId, RelativeProbability = def.Probability });
                        if (debug)
                            logger.LogWithColor(
                                $"[ItemGen][Debug] Added '{itemId}' to '{containerId}' with probability {def.Probability}.",
                                LogTextColor.Gray);
                    }
                    injected++;
                }

                containerLoot.ItemDistribution = distribution;
            }
        }

        if (containersNotFound.Count > 0)
        {
            logger.LogWithColor(
                $"[ItemGen] Warning: {containersNotFound.Count} container ID(s) were not found in static loot: {string.Join(", ", containersNotFound)}.",
                LogTextColor.Yellow);
        }

        return staticLoot;
    }
}
