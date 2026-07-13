using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using ItemGen.Generators;
using ItemGen.Models;
using ItemGen.Services;
using ItemGen.Validation;
using System.Text.Json;

namespace ItemGen;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.serenity.itemgen";
    public override string Name { get; init; } = "ItemGen";
    public override string Author { get; init; } = "Serenity";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.2.5");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("4.0.13");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public class ItemGenPlugin(
    ISptLogger<ItemGenPlugin> logger,
    ItemLoader itemLoader,
    CustomItemService customItemService,
    DatabaseService databaseService)
    : IOnLoad
{
    public Task OnLoad()
    {
        logger.LogWithColor("[ItemGen] ====================================", LogTextColor.Cyan);
        logger.LogWithColor($"[ItemGen] ItemGen Framework v{new ModMetadata().Version} loading...", LogTextColor.Cyan);
        logger.LogWithColor("[ItemGen] ====================================", LogTextColor.Cyan);

        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "user", "mods", "ItemGen", "config", "config.json");
        var config = ModConfig.Load(configPath);
        if (config.Debug)
            logger.LogWithColor($"[ItemGen] Debug logging enabled (config: {configPath}).", LogTextColor.Gray);

        if (!config.Enabled)
        {
            logger.LogWithColor("[ItemGen] Mod disabled in config.json — skipping load.", LogTextColor.Yellow);
            return Task.CompletedTask;
        }

        try
        {
            var packs = itemLoader.LoadAllPacks();
            if (packs.Count == 0)
            {
                logger.LogWithColor(
                    "[ItemGen] No item packs found. Place item pack JSON files in: user/mods/ItemGen/items/",
                    LogTextColor.Yellow);
                return Task.CompletedTask;
            }

            logger.LogWithColor($"[ItemGen] Found {packs.Count} item pack(s). Processing...", LogTextColor.Cyan);

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

            logger.LogWithColor($"[ItemGen] Loaded {questDefinitions.Count} quest item definition(s), {enabledQuestItems.Count} enabled.", LogTextColor.Cyan);
            logger.LogWithColor($"[ItemGen] Loaded {keyDefinitions.Count} key definition(s), {enabledKeys.Count} enabled.", LogTextColor.Cyan);
            logger.LogWithColor($"[ItemGen] Loaded {containerDefinitions.Count} container definition(s), {enabledContainers.Count} enabled.", LogTextColor.Cyan);
            logger.LogWithColor($"[ItemGen] Loaded {stimDefinitions.Count} stim definition(s), {enabledStims.Count} enabled.", LogTextColor.Cyan);
            logger.LogWithColor($"[ItemGen] Loaded {medkitDefinitions.Count} medkit definition(s), {enabledMedkits.Count} enabled.", LogTextColor.Cyan);

            // Register custom quest inventory items
            var registeredQuestItems = QuestInventoryItemGenerator.RegisterAll(customItemService, databaseService, enabledQuestItems, logger);

            // Register custom keys
            var registeredKeys = KeyGenerator.RegisterAll(customItemService, databaseService, enabledKeys, logger);

            // Write door-key mappings so the client can patch doors at runtime
            WriteDoorKeyMappings(configPath, enabledKeys);

            // Register custom containers
            var registeredContainers = ContainerGenerator.RegisterAll(customItemService, databaseService, enabledContainers, logger);

            // Register custom stims
            var registeredStims = StimGenerator.RegisterAll(customItemService, databaseService, enabledStims, logger);

            // Register custom medkits
            var registeredMedkits = MedKitGenerator.RegisterAll(customItemService, databaseService, enabledMedkits, logger);

            // Add custom items to trader assorts
            var traderEntries = TraderGenerator.RegisterAll(databaseService, packs.Select(p => p.Definition), logger);

            // Inject enabled items into container loot distributions
            var enabledItems = new List<ItemDefinition>();
            enabledItems.AddRange(enabledQuestItems);
            enabledItems.AddRange(enabledKeys);
            enabledItems.AddRange(enabledContainers);
            enabledItems.AddRange(enabledStims);
            enabledItems.AddRange(enabledMedkits);
            var lootInjections = LootInjector.InjectAll(databaseService, enabledItems, logger, config.Debug);

            logger.LogWithColor("[ItemGen] ====================================", LogTextColor.Cyan);
            logger.LogWithColor($"[ItemGen] Done! Registered {registeredQuestItems}/{enabledQuestItems.Count} custom quest item(s), {registeredKeys}/{enabledKeys.Count} custom key(s), {registeredContainers}/{enabledContainers.Count} custom container(s), {registeredStims}/{enabledStims.Count} custom stim(s), {registeredMedkits}/{enabledMedkits.Count} custom medkit(s), {traderEntries} trader entry/entries, and {lootInjections} loot injection(s).", LogTextColor.Green);
            logger.LogWithColor("[ItemGen] ====================================", LogTextColor.Cyan);
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[ItemGen] Fatal error during load: {ex}", LogTextColor.Red);
        }

        return Task.CompletedTask;
    }

    private void WriteDoorKeyMappings(string configPath, List<KeyDefinition> keys)
    {
        var modDir = Path.GetDirectoryName(Path.GetDirectoryName(configPath));
        if (string.IsNullOrEmpty(modDir))
        {
            logger.LogWithColor("[ItemGen] Could not determine mod directory for door-key mappings.", LogTextColor.Yellow);
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
            logger.LogWithColor($"[ItemGen] Wrote {mapping.Count} door-key mapping(s) to {doorsJsonPath}", LogTextColor.Green);
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[ItemGen] Failed to write door-key mappings: {ex.Message}", LogTextColor.Red);
        }
    }
}
