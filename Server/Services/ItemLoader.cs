using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using ItemGen.Models;
using ItemGen.Validation;

namespace ItemGen.Services;

[Injectable(TypePriority = OnLoadOrder.Database + 1)]
public class ItemLoader(ISptLogger<ItemLoader> logger, ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public record LoadedPack(ItemPackDefinition Definition, string SourceFile, string PackFolder);

    public List<LoadedPack> LoadAllPacks()
    {
        var results = new List<LoadedPack>();
        var assembly = Assembly.GetExecutingAssembly();
        var modPath = modHelper.GetAbsolutePathToModFolder(assembly);
        var itemsDir = Path.Combine(modPath, "items");

        if (!Directory.Exists(itemsDir))
        {
            Directory.CreateDirectory(itemsDir);
            logger.LogWithColor("[ItemGen] Created items/ directory. Place item pack JSON files here.", LogTextColor.Yellow);
            return results;
        }

        foreach (var packDir in Directory.GetDirectories(itemsDir))
        {
            foreach (var jsonFile in Directory.GetFiles(packDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                var loaded = TryLoadPackFile(jsonFile, packDir);
                if (loaded != null)
                    results.Add(loaded);
            }
        }

        foreach (var jsonFile in Directory.GetFiles(itemsDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            var loaded = TryLoadPackFile(jsonFile, itemsDir);
            if (loaded != null)
                results.Add(loaded);
        }

        return results;
    }

    public LoadedPack? LoadPackFromPath(string jsonFilePath, string packFolder)
    {
        return TryLoadPackFile(jsonFilePath, packFolder);
    }

    private LoadedPack? TryLoadPackFile(string jsonFilePath, string packFolder)
    {
        var fileName = Path.GetFileName(jsonFilePath);
        try
        {
            var jsonContent = File.ReadAllText(jsonFilePath);
            var pack = JsonSerializer.Deserialize<ItemPackDefinition>(jsonContent, JsonOptions);

            if (pack == null)
            {
                logger.LogWithColor($"[ItemGen] Failed to parse '{fileName}': JSON deserialized to null.", LogTextColor.Red);
                return null;
            }

            if (!pack.Enabled)
            {
                logger.LogWithColor($"[ItemGen] Skipping disabled pack '{fileName}'", LogTextColor.Yellow);
                return null;
            }

            var errors = ItemValidator.ValidatePack(pack, fileName);
            if (errors.Count > 0)
            {
                logger.LogWithColor($"[ItemGen] Validation errors in '{fileName}':", LogTextColor.Red);
                foreach (var error in errors)
                    logger.LogWithColor($"  - {error}", LogTextColor.Red);
                return null;
            }

            logger.LogWithColor($"[ItemGen] Loaded pack '{pack.Name}' from '{fileName}' ({pack.QuestItems.Count} quest items, {pack.Keys.Count} keys)", LogTextColor.Green);
            return new LoadedPack(pack, jsonFilePath, packFolder);
        }
        catch (JsonException ex)
        {
            logger.LogWithColor($"[ItemGen] JSON parse error in '{fileName}': {ex.Message}", LogTextColor.Red);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[ItemGen] Error loading '{fileName}': {ex.Message}", LogTextColor.Red);
            return null;
        }
    }
}
