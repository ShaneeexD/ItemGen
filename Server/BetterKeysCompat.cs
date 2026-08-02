using System.IO;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;
using ItemGen.Generators;
using Spectre.Console;

namespace ItemGen;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 2)]
public class BetterKeysCompat(TemplateTable templateTable, ISptLogger<ItemGenPlugin> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        if (KeyGenerator.RegisteredKeyColors.Count == 0)
            return Task.CompletedTask;

        var betterKeysColors = LoadBetterKeysColors();
        var isBetterKeysInstalled = betterKeysColors != null;
        var colors = betterKeysColors ?? KeyGenerator.GetDefaultMapColors();

        logger.LogWithColor(
            $"[ItemGen] BetterKeysCompat: BetterKeys {(isBetterKeysInstalled ? "detected" : "not detected")}, {colors.Count} map colors available.",
            Color.Grey);

        var items = templateTable.Items;
        var reapplied = 0;

        foreach (var (keyId, (map, explicitColor)) in KeyGenerator.RegisteredKeyColors)
        {
            if (!items.TryGetValue(keyId, out var tpl) || tpl.Properties == null)
            {
                logger.LogWithColor($"[ItemGen] BetterKeysCompat: key '{keyId}' not found in items db or properties null", Color.Yellow);
                continue;
            }

            var beforeColor = tpl.Properties.BackgroundColor ?? "(none)";

            string? color = null;

            if (isBetterKeysInstalled && !string.IsNullOrWhiteSpace(map))
            {
                // BetterKeys is installed — use its map color (takes priority over explicit)
                colors.TryGetValue(map, out color);
            }

            if (string.IsNullOrWhiteSpace(color))
            {
                // Fallback: explicit backgroundColor, then map default color
                if (!string.IsNullOrWhiteSpace(explicitColor))
                {
                    color = explicitColor;
                }
                else if (!string.IsNullOrWhiteSpace(map))
                {
                    colors.TryGetValue(map, out color);
                }
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                tpl.Properties.BackgroundColor = color;
                reapplied++;
                logger.LogWithColor(
                    $"[ItemGen] BetterKeysCompat: key '{keyId}' map='{map}' color '{beforeColor}' -> '{color}' (explicit={explicitColor ?? "null"})",
                    Color.Grey);
            }
            else
            {
                logger.LogWithColor(
                    $"[ItemGen] BetterKeysCompat: key '{keyId}' map='{map}' — no color resolved (explicit={explicitColor ?? "null"})",
                    Color.Yellow);
            }
        }

        if (reapplied > 0)
        {
            logger.LogWithColor(
                $"[ItemGen] Re-applied background colors to {reapplied} key(s) after BetterKeys ({(isBetterKeysInstalled ? "using BetterKeys colors" : "using ItemGen defaults")}).",
                Color.Grey);
        }

        return Task.CompletedTask;
    }

    private static Dictionary<string, string>? LoadBetterKeysColors()
    {
        var modsDir = IOPath.Combine(Directory.GetCurrentDirectory(), "user", "mods");
        if (!Directory.Exists(modsDir))
            return null;

        foreach (var modDir in Directory.GetDirectories(modsDir))
        {
            var constantsPath = IOPath.Combine(modDir, "db", "_constants.json");
            if (!File.Exists(constantsPath))
                continue;

            var configPath = IOPath.Combine(modDir, "config", "configuser.jsonc");
            if (!File.Exists(configPath))
                configPath = IOPath.Combine(modDir, "config", "config.jsonc");

            if (!File.Exists(configPath))
                continue;

            try
            {
                var json = File.ReadAllText(configPath);
                // Strip JSONC comments — use Multiline so $ matches end of each line
                json = Regex.Replace(json, @"//.*?$", "", RegexOptions.Multiline);
                json = Regex.Replace(json, @"/\*.*?\*/", "", RegexOptions.Singleline);
                // Strip trailing commas that would break JSON parsing
                json = Regex.Replace(json, @",\s*([}\]])", "$1", RegexOptions.Singleline);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("BackgroundColors", out var bgColors))
                {
                    var result = new Dictionary<string, string>();
                    foreach (var prop in bgColors.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            result[prop.Name] = prop.Value.GetString() ?? "";
                        }
                    }
                    if (result.Count > 0)
                        return result;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
