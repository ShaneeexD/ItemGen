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

        var questList = pack.QuestItems ?? [];
        var keyList = pack.Keys ?? [];
        var containerList = pack.Containers ?? [];
        var stimList = pack.Stims ?? [];

        if (questList.Count == 0 && keyList.Count == 0 && containerList.Count == 0 && stimList.Count == 0)
        {
            errors.Add($"Pack '{fileName}': at least one item entry is required.");
            return errors;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        for (var i = 0; i < containerList.Count; i++)
        {
            var container = containerList[i];
            var prefix = $"Container[{i}]";
            ValidateItem(container, prefix, errors, seenIds);

            if (container.Weight < 0)
                errors.Add($"{prefix}: 'weight' cannot be negative.");

            if (container.HandbookPriceRoubles < 0)
                errors.Add($"{prefix}: 'handbookPriceRoubles' cannot be negative.");

            if (container.FleaPriceRoubles < 0)
                errors.Add($"{prefix}: 'fleaPriceRoubles' cannot be negative.");

            if (string.IsNullOrWhiteSpace(container.Parent))
                errors.Add($"{prefix}: 'parent' is required.");

            if (string.IsNullOrWhiteSpace(container.HandbookParentId))
                errors.Add($"{prefix}: 'handbookParentId' is required.");

            if (container.SafeContainerMode == SafeContainerMode.Include && container.SafeContainerIds.Count == 0)
                errors.Add($"{prefix}: 'safeContainerIds' must contain at least one ID when mode is 'include'.");

            for (var j = 0; j < container.SafeContainerIds.Count; j++)
            {
                if (!Hex24.IsMatch(container.SafeContainerIds[j]))
                    errors.Add($"{prefix}: 'safeContainerIds[{j}]' must be a 24-character hex string.");
            }
        }

        for (var i = 0; i < stimList.Count; i++)
        {
            var stim = stimList[i];
            var prefix = $"Stim[{i}]";
            ValidateItem(stim, prefix, errors, seenIds);

            if (stim.Weight < 0)
                errors.Add($"{prefix}: 'weight' cannot be negative.");

            if (stim.MedUseTime < 0)
                errors.Add($"{prefix}: 'medUseTime' cannot be negative.");

            if (stim.StackMaxSize < 1)
                errors.Add($"{prefix}: 'stackMaxSize' must be at least 1.");

            if (stim.Width < 1)
                errors.Add($"{prefix}: 'width' must be at least 1.");

            if (stim.Height < 1)
                errors.Add($"{prefix}: 'height' must be at least 1.");
        }

        var traderList = pack.Traders ?? [];
        for (var i = 0; i < traderList.Count; i++)
        {
            var trader = traderList[i];
            var tPrefix = $"Trader[{i}]";

            if (string.IsNullOrWhiteSpace(trader.TraderId) || !Hex24.IsMatch(trader.TraderId))
                errors.Add($"{tPrefix}: 'traderId' must be a 24-character hex string.");

            for (var j = 0; j < trader.Entries.Count; j++)
            {
                var entry = trader.Entries[j];
                var ePrefix = $"{tPrefix}.Entry[{j}]";

                if (string.IsNullOrWhiteSpace(entry.ItemId) || !Hex24.IsMatch(entry.ItemId))
                    errors.Add($"{ePrefix}: 'itemId' must be a 24-character hex string.");

                if (entry.PriceRoubles < 0)
                    errors.Add($"{ePrefix}: 'priceRoubles' cannot be negative.");

                if (entry.LoyaltyLevel < 1 || entry.LoyaltyLevel > 4)
                    errors.Add($"{ePrefix}: 'loyaltyLevel' must be between 1 and 4.");

                if (entry.StockCount < 0)
                    errors.Add($"{ePrefix}: 'stockCount' cannot be negative.");

                if (entry.BuyRestrictionMax < 0)
                    errors.Add($"{ePrefix}: 'buyRestrictionMax' cannot be negative.");
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
