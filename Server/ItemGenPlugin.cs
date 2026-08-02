using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Models.Spt.Tables;
using ItemGen.Generators;
using ItemGen.Models;
using ItemGen.Services;
using ItemGen.Validation;
using System.Text.Json;
using Spectre.Console;

namespace ItemGen;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.serenity.itemgen";
    public string Name { get; init; } = "ItemGen";
    public string Author { get; init; } = "Serenity";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.5.1");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.1");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public bool HasPrepatcher { get; init; } = false;
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public class ItemGenPlugin(
    ISptLogger<ItemGenPlugin> logger,
    ItemLoader itemLoader,
    CustomItemService customItemService,
    TemplateTable templateTable,
    TradersTable tradersTable,
    HideoutTable hideoutTable,
    LocationTable locationTable,
    GlobalTable globalTable)
    : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.LogWithColor("[ItemGen] ====================================", Color.Cyan1);
        logger.LogWithColor($"[ItemGen] ItemGen Framework v{new ModMetadata().Version} loading...", Color.Cyan1);
        logger.LogWithColor("[ItemGen] ====================================", Color.Cyan1);

        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "user", "mods", "ItemGen", "config", "config.json");
        var config = ModConfig.Load(configPath);
        if (config.Debug)
            logger.LogWithColor($"[ItemGen] Debug logging enabled (config: {configPath}).", Color.Grey);

        if (!config.Enabled)
        {
            logger.LogWithColor("[ItemGen] Mod disabled in config.json — skipping load.", Color.Yellow);
            return Task.CompletedTask;
        }

        try
        {
            var packs = itemLoader.LoadAllPacks();
            if (packs.Count == 0)
            {
                logger.LogWithColor($"[ItemGen] No item packs found. Place item pack JSON files in: user/mods/ItemGen/items/",
                    Color.Yellow);
                return Task.CompletedTask;
            }

            logger.LogWithColor($"[ItemGen] Found {packs.Count} item pack(s). Processing...", Color.Cyan1);

            var questDefinitions = packs.SelectMany(p => p.Definition.QuestItems).ToList();
            var enabledQuestItems = questDefinitions.Where(d => d.Enabled).ToList();
            var keyDefinitions = packs.SelectMany(p => p.Definition.Keys).ToList();
            var enabledKeys = keyDefinitions.Where(d => d.Enabled).ToList();
            var containerDefinitions = packs.SelectMany(p => p.Definition.Containers).ToList();
            var enabledContainers = containerDefinitions.Where(d => d.Enabled).ToList();
            var stimDefinitions = packs.SelectMany(p => p.Definition.Stims).ToList();
            var enabledStims = stimDefinitions.Where(d => d.Enabled).ToList();
            var medkitDefinitions = packs.SelectMany(p => p.Definition.Medkits).ToList();
            var enabledMedkits = medkitDefinitions.Where(d => d.Enabled).ToList();
            var foodDrinkDefinitions = packs.SelectMany(p => p.Definition.FoodDrinks).ToList();
            var enabledFoodDrinks = foodDrinkDefinitions.Where(d => d.Enabled).ToList();
            var barterDefinitions = packs.SelectMany(p => p.Definition.Barters).ToList();
            var enabledBarters = barterDefinitions.Where(d => d.Enabled).ToList();

            logger.LogWithColor($"[ItemGen] Loaded {questDefinitions.Count} quest item definition(s), {enabledQuestItems.Count} enabled.", Color.Cyan1);
            logger.LogWithColor($"[ItemGen] Loaded {keyDefinitions.Count} key definition(s), {enabledKeys.Count} enabled.", Color.Cyan1);
            logger.LogWithColor($"[ItemGen] Loaded {containerDefinitions.Count} container definition(s), {enabledContainers.Count} enabled.", Color.Cyan1);
            logger.LogWithColor($"[ItemGen] Loaded {stimDefinitions.Count} stim definition(s), {enabledStims.Count} enabled.", Color.Cyan1);
            logger.LogWithColor($"[ItemGen] Loaded {medkitDefinitions.Count} medkit definition(s), {enabledMedkits.Count} enabled.", Color.Cyan1);
            logger.LogWithColor($"[ItemGen] Loaded {foodDrinkDefinitions.Count} food/drink definition(s), {enabledFoodDrinks.Count} enabled.", Color.Cyan1);
            logger.LogWithColor($"[ItemGen] Loaded {barterDefinitions.Count} barter item definition(s), {enabledBarters.Count} enabled.", Color.Cyan1);

            // Register custom quest inventory items
            var registeredQuestItems = QuestInventoryItemGenerator.RegisterAll(customItemService, templateTable, enabledQuestItems, logger);

            // Register custom keys
            var registeredKeys = KeyGenerator.RegisterAll(customItemService, templateTable, enabledKeys, logger);

            // Write door-key mappings so the client can patch doors at runtime
            WriteDoorKeyMappings(configPath, enabledKeys);

            // Register custom containers
            var registeredContainers = ContainerGenerator.RegisterAll(customItemService, templateTable, enabledContainers, logger);

            // Register custom stims
            var registeredStims = StimGenerator.RegisterAll(customItemService, templateTable, globalTable, enabledStims, logger);

            // Register custom medkits
            var registeredMedkits = MedKitGenerator.RegisterAll(customItemService, templateTable, enabledMedkits, logger);

            // Register custom food and drink
            var registeredFoodDrinks = FoodDrinkGenerator.RegisterAll(customItemService, templateTable, globalTable, enabledFoodDrinks, logger);

            // Register custom barter items
            var registeredBarters = BarterGenerator.RegisterAll(customItemService, templateTable, enabledBarters, logger);

            // Add custom items to trader assorts
            var traderEntries = TraderGenerator.RegisterAll(templateTable, tradersTable, packs.Select(p => p.Definition), logger);

            // Inject enabled items into container loot distributions
            var enabledItems = new List<ItemDefinition>();
            enabledItems.AddRange(enabledQuestItems);
            enabledItems.AddRange(enabledKeys);
            enabledItems.AddRange(enabledContainers);
            enabledItems.AddRange(enabledStims);
            enabledItems.AddRange(enabledMedkits);
            enabledItems.AddRange(enabledFoodDrinks);
            enabledItems.AddRange(enabledBarters);
            var lootInjections = LootInjector.InjectAll(locationTable, enabledItems, logger, config.Debug);

            // Add hideout workbench crafting recipes
            var craftingRecipes = CraftingManager.RegisterAll(hideoutTable, enabledItems, logger);

            logger.LogWithColor("[ItemGen] ====================================", Color.Cyan1);
            logger.LogWithColor($"[ItemGen] Done! Registered {registeredQuestItems}/{enabledQuestItems.Count} custom quest item(s), {registeredKeys}/{enabledKeys.Count} custom key(s), {registeredContainers}/{enabledContainers.Count} custom container(s), {registeredStims}/{enabledStims.Count} custom stim(s), {registeredMedkits}/{enabledMedkits.Count} custom medkit(s), {registeredFoodDrinks}/{enabledFoodDrinks.Count} custom food/drink(s), {registeredBarters}/{enabledBarters.Count} custom barter item(s), {traderEntries} trader entry/entries, {lootInjections} loot injection(s), and {craftingRecipes} crafting recipe(s).", Color.Green);
            logger.LogWithColor("[ItemGen] ====================================", Color.Cyan1);
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[ItemGen] Fatal error during load: {ex}", Color.Red);
        }

        return Task.CompletedTask;
    }

    private void WriteDoorKeyMappings(string configPath, List<KeyDefinition> keys)
    {
        var modDir = Path.GetDirectoryName(Path.GetDirectoryName(configPath));
        if (string.IsNullOrEmpty(modDir))
        {
            logger.LogWithColor("[ItemGen] Could not determine mod directory for door-key mappings.", Color.Yellow);
            return;
        }

        Directory.CreateDirectory(modDir);
        var doorsJsonPath = Path.Combine(modDir, "doors.json");

        var mapping = new Dictionary<string, List<string>>();
        foreach (var key in keys)
        {
            foreach (var doorId in key.DoorIds)
            {
                if (string.IsNullOrWhiteSpace(doorId))
                    continue;

                if (!mapping.TryGetValue(doorId, out var keyIds))
                {
                    keyIds = new List<string>();
                    mapping[doorId] = keyIds;
                }

                if (!keyIds.Contains(key.Id))
                    keyIds.Add(key.Id);
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(doorsJsonPath, json);
            logger.LogWithColor($"[ItemGen] Wrote {mapping.Count} door-key mapping(s) to {doorsJsonPath}", Color.Green);
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[ItemGen] Failed to write door-key mappings: {ex.Message}", Color.Red);
        }
    }
}
