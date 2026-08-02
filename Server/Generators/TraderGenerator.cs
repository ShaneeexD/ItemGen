using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;
using ItemGen.Models;
using SpectreColor = Spectre.Console.Color;

namespace ItemGen.Generators;

public static class TraderGenerator
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";

    public static int RegisterAll(
        TemplateTable templateTable,
        TradersTable tradersTable,
        IEnumerable<ItemPackDefinition> packs,
        ISptLogger<ItemGenPlugin> logger)
    {
        var traders = tradersTable;
        var added = 0;

        foreach (var pack in packs)
        {
            if (!pack.Enabled)
            {
                continue;
            }

            foreach (var traderDef in pack.Traders)
            {
                if (!traderDef.Enabled)
                {
                    continue;
                }

                foreach (var entry in traderDef.Entries)
                {
                    if (!entry.Enabled)
                    {
                        continue;
                    }

                    try
                    {
                        if (AddToTrader(templateTable, traders, traderDef.TraderId, entry, logger))
                        {
                            added++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWithColor(
                            $"[ItemGen] Failed to add trader entry for item '{entry.ItemId}' on trader '{traderDef.TraderId}': {ex.Message}",
                            SpectreColor.Red);
                    }
                }
            }
        }

        if (added > 0)
        {
            logger.LogWithColor($"[ItemGen] Added {added} trader entry/entries across all packs.", SpectreColor.Green);
        }

        return added;
    }

    private static bool AddToTrader(
        TemplateTable templateTable,
        TradersTable traders,
        string traderId,
        TraderItemEntry entry,
        ISptLogger<ItemGenPlugin> logger)
    {
        var traderIdValue = new MongoId(traderId);

        if (!traders.ContainsKey(traderIdValue))
        {
            logger.LogWithColor(
                $"[ItemGen] Trader '{traderId}' not found. Skipping entry for item '{entry.ItemId}'.",
                SpectreColor.Red);
            return false;
        }

        var trader = traders[traderIdValue];
        var assort = trader.Assort as TraderAssort;

        if (assort == null)
        {
            logger.LogWithColor(
                $"[ItemGen] Trader '{traderId}' has no assort. Skipping entry for item '{entry.ItemId}'.",
                SpectreColor.Red);
            return false;
        }

        var itemTemplateId = new MongoId(entry.ItemId);
        var items = templateTable.Items;
        if (!items.ContainsKey(entry.ItemId))
        {
            logger.LogWithColor(
                $"[ItemGen] Cannot add item '{entry.ItemId}' to trader '{traderId}' - item not registered.",
                SpectreColor.Red);
            return false;
        }

        var stockCount = entry.UnlimitedStock ? 999999 : entry.StockCount;
        int? buyRestrictionMax = entry.UnlimitedBuyRestriction ? null : entry.BuyRestrictionMax;

        var typedItem = new Item
        {
            Id = itemTemplateId,
            Template = itemTemplateId,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                StackObjectsCount = stockCount,
                UnlimitedCount = entry.UnlimitedStock,
                BuyRestrictionMax = buyRestrictionMax,
                BuyRestrictionCurrent = 0,
            },
        };
        assort.Items.Add(typedItem);

        assort.BarterScheme[itemTemplateId] = BuildBarterScheme(entry);
        assort.LoyalLevelItems[itemTemplateId] = entry.LoyaltyLevel;

        return true;
    }

    private static List<List<BarterScheme>> BuildBarterScheme(TraderItemEntry entry)
    {
        if (entry.Barter is { Count: > 0 })
        {
            var schemeItems = new List<BarterScheme>();
            foreach (var barter in entry.Barter)
            {
                var scheme = new BarterScheme
                {
                    Template = new MongoId(barter.ItemTpl),
                    Count = barter.Count,
                };

                if (barter.Level.HasValue)
                {
                    scheme.Level = barter.Level.Value;
                }

                if (!string.IsNullOrEmpty(barter.Side))
                {
                    scheme.Side = Enum.Parse<DogtagExchangeSide>(barter.Side, true);
                }

                schemeItems.Add(scheme);
            }

            return [schemeItems];
        }

        return
        [
            new List<BarterScheme>
            {
                new BarterScheme
                {
                    Count = entry.PriceRoubles,
                    Template = new MongoId(RoublesTpl),
                }
            }
        ];
    }
}
