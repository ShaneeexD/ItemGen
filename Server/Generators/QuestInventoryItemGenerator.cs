using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using ItemGen.Models;

namespace ItemGen.Generators;

public static class QuestInventoryItemGenerator
{
    public static void RegisterAll(
        CustomItemService customItemService,
        DatabaseService databaseService,
        IReadOnlyList<QuestItemDefinition> definitions,
        ISptLogger<ItemGenPlugin> logger)
    {
        foreach (var def in definitions)
        {
            try
            {
                RegisterQuestItem(def, customItemService, databaseService, logger);
            }
            catch (Exception ex)
            {
                logger.LogWithColor($"[ItemGen] Failed to register quest item '{def.Name}': {ex.Message}", LogTextColor.Red);
            }
        }
    }

    private static void RegisterQuestItem(
        QuestItemDefinition def,
        CustomItemService customItemService,
        DatabaseService databaseService,
        ISptLogger<ItemGenPlugin> logger)
    {
        var parentId = ResolveParentId(databaseService, def.BaseTpl);
        var handbookParentId = ResolveHandbookParent(databaseService, def.BaseTpl);

        var overrides = new TemplateItemProperties
        {
            Name = def.ShortName,
            ShortName = def.ShortName,
            Description = def.Description,
            Weight = def.Weight,
            BackgroundColor = string.IsNullOrWhiteSpace(def.BackgroundColor) ? null : def.BackgroundColor,
            StackMaxSize = def.StackMaxSize,
            QuestItem = true,
            CanSellOnRagfair = def.CanSellOnRagfair,
            RarityPvE = def.RarityPvE,
        };

        var details = new NewItemFromCloneDetails
        {
            NewId = def.Id,
            ItemTplToClone = def.BaseTpl,
            ParentId = parentId,
            HandbookParentId = handbookParentId,
            HandbookPriceRoubles = def.HandbookPriceRoubles,
            FleaPriceRoubles = def.FleaPriceRoubles,
            OverrideProperties = overrides,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = def.Name,
                    ShortName = def.ShortName,
                    Description = def.Description,
                }
            },
        };

        var result = customItemService.CreateItemFromClone(details);

        if (result.Success == true)
        {
            logger.LogWithColor($"[ItemGen] Registered quest item: {def.Name} ({def.Id})", LogTextColor.Green);
            ValidateLinkedQuests(def, databaseService, logger);
        }
        else
        {
            logger.LogWithColor(
                $"[ItemGen] CreateItemFromClone reported failure for quest item '{def.Name}': {string.Join(", ", result.Errors ?? [])}",
                LogTextColor.Yellow);
        }
    }

    private static void ValidateLinkedQuests(
        QuestItemDefinition def,
        DatabaseService databaseService,
        ISptLogger<ItemGenPlugin> logger)
    {
        if (def.QuestIds is null || def.QuestIds.Count == 0)
        {
            return;
        }

        var quests = databaseService.GetQuests();
        foreach (var questId in def.QuestIds)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                continue;
            }

            if (!quests.TryGetValue(questId, out var quest))
            {
                logger.LogWithColor(
                    $"[ItemGen] Quest item '{def.Name}' references unknown quest '{questId}'. Make sure the quest is registered by a companion mod (e.g. TraderGen).",
                    LogTextColor.Yellow);
                continue;
            }

            var hasFindItem = quest.Conditions?.AvailableForFinish?.Any(c =>
                c.ConditionType == "FindItem" &&
                ((c.Target.IsList ? c.Target.List : [c.Target.Item])?.Contains(def.Id) ?? false)
            ) ?? false;

            if (!hasFindItem)
            {
                logger.LogWithColor(
                    $"[ItemGen] Quest item '{def.Name}' is linked to quest '{questId}' but that quest does not have a FindItem condition targeting this item.",
                    LogTextColor.Yellow);
            }
        }
    }

    private static string ResolveParentId(DatabaseService databaseService, string baseTpl)
    {
        var items = databaseService.GetItems();
        if (items.TryGetValue(baseTpl, out var baseItem) && !string.IsNullOrWhiteSpace(baseItem.Parent))
        {
            return baseItem.Parent;
        }
        return "5448ecbe4bdc2d60728b4568"; // Info
    }

    private static string ResolveHandbookParent(DatabaseService databaseService, string baseTpl)
    {
        var items = databaseService.GetItems();
        if (items.TryGetValue(baseTpl, out var baseItem))
        {
            var handbook = databaseService.GetHandbook().Items.FirstOrDefault(h => h.Id == baseTpl);
            if (handbook != null && !string.IsNullOrWhiteSpace(handbook.ParentId))
            {
                return handbook.ParentId;
            }
        }
        return "5b47574386f77428ca22b33f"; // Quest items / special items
    }
}
