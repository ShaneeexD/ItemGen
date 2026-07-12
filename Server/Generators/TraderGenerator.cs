using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ItemGen.Models;

namespace ItemGen.Generators;

public static class TraderGenerator
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";

    public static int RegisterAll(
        DatabaseService databaseService,
        IEnumerable<ItemPackDefinition> packs,
        ISptLogger<ItemGenPlugin> logger)
    {
        var traders = databaseService.GetTraders();
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
                        if (AddToTrader(databaseService, traders, traderDef.TraderId, entry, logger))
                        {
                            added++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWithColor(
                            $"[ItemGen] Failed to add trader entry for item '{entry.ItemId}' on trader '{traderDef.TraderId}': {ex.Message}",
                            LogTextColor.Yellow);
                    }
                }
            }
        }

        if (added > 0)
        {
            logger.LogWithColor($"[ItemGen] Added {added} trader entry/entries across all packs.", LogTextColor.Green);
        }

        return added;
    }

    private static bool AddToTrader(
        DatabaseService databaseService,
        dynamic traders,
        string traderId,
        TraderItemEntry entry,
        ISptLogger<ItemGenPlugin> logger)
    {
        var traderIdValue = new MongoId(traderId);

        if (!traders.ContainsKey(traderIdValue))
        {
            logger.LogWithColor(
                $"[ItemGen] Trader '{traderId}' not found. Skipping entry for item '{entry.ItemId}'.",
                LogTextColor.Yellow);
            return false;
        }

        var trader = traders[traderIdValue];
        var assort = trader.Assort as TraderAssort;

        if (assort == null)
        {
            logger.LogWithColor(
                $"[ItemGen] Trader '{traderId}' has no assort. Skipping entry for item '{entry.ItemId}'.",
                LogTextColor.Yellow);
            return false;
        }

        var itemTemplateId = new MongoId(entry.ItemId);
        var items = databaseService.GetItems();
        if (!items.ContainsKey(entry.ItemId))
        {
            logger.LogWithColor(
                $"[ItemGen] Cannot add item '{entry.ItemId}' to trader '{traderId}' - item not registered.",
                LogTextColor.Yellow);
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

        var barterEntry = new List<List<BarterScheme>>
        {
            new List<BarterScheme>
            {
                new BarterScheme
                {
                    Count = entry.PriceRoubles,
                    Template = new MongoId(RoublesTpl),
                }
            }
        };
        assort.BarterScheme[itemTemplateId] = barterEntry;
        assort.LoyalLevelItems[itemTemplateId] = entry.LoyaltyLevel;

        return true;
    }
}
