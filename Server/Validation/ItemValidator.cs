using System.Text.RegularExpressions;
using ItemGen.Models;

namespace ItemGen.Validation;

public static class ItemValidator
{
    private static readonly Regex Hex24 = new("^[0-9a-fA-F]{24}$", RegexOptions.Compiled);

    public static List<string> ValidatePack(ItemPackDefinition pack, string fileName)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(pack.Name))
            errors.Add($"Pack '{fileName}': 'name' is required.");

        if ((pack.QuestItems == null || pack.QuestItems.Count == 0) && (pack.Keys == null || pack.Keys.Count == 0))
        {
            errors.Add($"Pack '{fileName}': at least one quest item or key entry is required.");
            return errors;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var questList = pack.QuestItems ?? [];
        var keyList = pack.Keys ?? [];

        for (var i = 0; i < questList.Count; i++)
        {
            var item = questList[i];
            var prefix = $"QuestItem[{i}]";
            ValidateItem(item, prefix, errors, seenIds);

            if (item.Weight < 0)
                errors.Add($"{prefix}: 'weight' cannot be negative.");

            if (item.StackMaxSize < 1)
                errors.Add($"{prefix}: 'stackMaxSize' must be >= 1.");

            if (item.HandbookPriceRoubles < 0)
                errors.Add($"{prefix}: 'handbookPriceRoubles' cannot be negative.");

            if (item.FleaPriceRoubles < 0)
                errors.Add($"{prefix}: 'fleaPriceRoubles' cannot be negative.");
        }

        for (var i = 0; i < keyList.Count; i++)
        {
            var key = keyList[i];
            var prefix = $"Key[{i}]";
            ValidateItem(key, prefix, errors, seenIds);

            if (key.Uses < 1)
                errors.Add($"{prefix}: 'uses' must be >= 1.");

            if (key.Weight < 0)
                errors.Add($"{prefix}: 'weight' cannot be negative.");

            if (key.HandbookPriceRoubles < 0)
                errors.Add($"{prefix}: 'handbookPriceRoubles' cannot be negative.");

            if (key.FleaPriceRoubles < 0)
                errors.Add($"{prefix}: 'fleaPriceRoubles' cannot be negative.");

            foreach (var doorId in key.DoorIds)
            {
                if (string.IsNullOrWhiteSpace(doorId))
                    errors.Add($"{prefix}: 'doorIds' entries cannot be empty.");
            }
        }

        return errors;
    }

    private static void ValidateItem(ItemDefinition item, string prefix, List<string> errors, HashSet<string> seenIds)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            errors.Add($"{prefix}: 'id' is required.");
        }
        else if (!Hex24.IsMatch(item.Id))
        {
            errors.Add($"{prefix}: 'id' must be a 24-character hex string.");
        }
        else if (!seenIds.Add(item.Id))
        {
            errors.Add($"{prefix}: duplicate id '{item.Id}'.");
        }

        if (string.IsNullOrWhiteSpace(item.BaseTpl) || !Hex24.IsMatch(item.BaseTpl))
            errors.Add($"{prefix}: 'baseTpl' must be a 24-character hex string.");

        if (string.IsNullOrWhiteSpace(item.Name))
            errors.Add($"{prefix}: 'name' is required.");

        if (string.IsNullOrWhiteSpace(item.ShortName))
            errors.Add($"{prefix}: 'shortName' is required.");

        if (string.IsNullOrWhiteSpace(item.Description))
            errors.Add($"{prefix}: 'description' is required.");
    }
}
