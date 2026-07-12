using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace ItemGen.Models;

public class ItemPackDefinition
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("questItems")]
    public List<QuestItemDefinition> QuestItems { get; set; } = [];

    [JsonPropertyName("keys")]
    public List<KeyDefinition> Keys { get; set; } = [];

    [JsonPropertyName("containers")]
    public List<ContainerDefinition> Containers { get; set; } = [];

    [JsonPropertyName("stims")]
    public List<StimDefinition> Stims { get; set; } = [];

    [JsonPropertyName("medkits")]
    public List<MedKitDefinition> Medkits { get; set; } = [];

    [JsonPropertyName("traders")]
    public List<TraderDefinition> Traders { get; set; } = [];
}

public class StimBuff
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("buffType")]
    public string BuffType { get; set; } = "SkillRate";

    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public float Value { get; set; }

    [JsonPropertyName("delay")]
    public float Delay { get; set; } = 1;

    [JsonPropertyName("duration")]
    public float Duration { get; set; } = 300;

    [JsonPropertyName("chance")]
    public float Chance { get; set; } = 1;

    [JsonPropertyName("absoluteValue")]
    public bool AbsoluteValue { get; set; } = true;

    [JsonPropertyName("hydration")]
    public int? Hydration { get; set; }

    [JsonPropertyName("energy")]
    public int? Energy { get; set; }
}

public class StimDefinition : ItemDefinition
{
    [JsonPropertyName("itemSound")]
    public string ItemSound { get; set; } = "med_stimulator";

    [JsonPropertyName("stimulatorBuffs")]
    public string StimulatorBuffs { get; set; } = string.Empty;

    [JsonPropertyName("medEffectType")]
    public string MedEffectType { get; set; } = "duringUse";

    [JsonPropertyName("medUseTime")]
    public float MedUseTime { get; set; } = 2;

    [JsonPropertyName("maxHpResource")]
    public int MaxHpResource { get; set; }

    [JsonPropertyName("hpResourceRate")]
    public int HpResourceRate { get; set; }

    [JsonPropertyName("maxBodyPartsToHeal")]
    public int MaxBodyPartsToHeal { get; set; }

    [JsonPropertyName("stackMaxSize")]
    public int StackMaxSize { get; set; } = 1;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1;

    [JsonPropertyName("customBuffs")]
    public List<StimBuff> CustomBuffs { get; set; } = [];

    [JsonPropertyName("effectsHealth")]
    public Dictionary<string, EffectsHealthProperties>? EffectsHealth { get; set; }

    [JsonPropertyName("effectsDamage")]
    public Dictionary<string, EffectsDamageProperties>? EffectsDamage { get; set; }

    [JsonPropertyName("properties")]
    public JsonElement Properties { get; set; }
}

public class MedKitDefinition : ItemDefinition
{
    [JsonPropertyName("itemSound")]
    public string ItemSound { get; set; } = "med_medkit";

    [JsonPropertyName("medEffectType")]
    public string MedEffectType { get; set; } = "duringUse";

    [JsonPropertyName("medUseTime")]
    public float MedUseTime { get; set; } = 3;

    [JsonPropertyName("maxHpResource")]
    public int MaxHpResource { get; set; } = 400;

    [JsonPropertyName("hpResourceRate")]
    public int HpResourceRate { get; set; } = 85;

    [JsonPropertyName("stackMaxSize")]
    public int StackMaxSize { get; set; } = 1;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 2;

    [JsonPropertyName("effectsHealth")]
    public Dictionary<string, EffectsHealthProperties>? EffectsHealth { get; set; }

    [JsonPropertyName("effectsDamage")]
    public Dictionary<string, EffectsDamageProperties>? EffectsDamage { get; set; }

    [JsonPropertyName("properties")]
    public JsonElement Properties { get; set; }
}

public class TraderItemEntry
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("priceRoubles")]
    public int PriceRoubles { get; set; }

    [JsonPropertyName("loyaltyLevel")]
    public int LoyaltyLevel { get; set; } = 1;

    [JsonPropertyName("stockCount")]
    public int StockCount { get; set; } = 200;

    [JsonPropertyName("buyRestrictionMax")]
    public int BuyRestrictionMax { get; set; } = 200;

    [JsonPropertyName("unlimitedStock")]
    public bool UnlimitedStock { get; set; }

    [JsonPropertyName("unlimitedBuyRestriction")]
    public bool UnlimitedBuyRestriction { get; set; }
}

public class TraderDefinition
{
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("entries")]
    public List<TraderItemEntry> Entries { get; set; } = [];
}

public class ItemDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("baseTpl")]
    public string BaseTpl { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("handbookPriceRoubles")]
    public int HandbookPriceRoubles { get; set; }

    [JsonPropertyName("fleaPriceRoubles")]
    public int FleaPriceRoubles { get; set; }

    [JsonPropertyName("rarityPvE")]
    public string RarityPvE { get; set; } = "Not_exist";

    [JsonPropertyName("canSellOnRagfair")]
    public bool CanSellOnRagfair { get; set; } = true;

    [JsonPropertyName("customIcon")]
    public string? CustomIcon { get; set; }

    [JsonPropertyName("customModel")]
    public string? CustomModel { get; set; }
}

public class QuestItemDefinition : ItemDefinition
{
    [JsonPropertyName("stackMaxSize")]
    public int StackMaxSize { get; set; } = 1;

    [JsonPropertyName("questIds")]
    public List<string> QuestIds { get; set; } = [];
}

public class KeyDefinition : ItemDefinition
{
    [JsonPropertyName("uses")]
    public int Uses { get; set; } = 40;

    [JsonPropertyName("keyCategory")]
    public string KeyCategory { get; set; } = string.Empty;

    [JsonPropertyName("doorIds")]
    public List<string> DoorIds { get; set; } = [];

    [JsonPropertyName("properties")]
    public JsonElement Properties { get; set; }
}

public class ContainerDefinition : ItemDefinition
{
    [JsonPropertyName("parent")]
    public string Parent { get; set; } = string.Empty;

    [JsonPropertyName("handbookParentId")]
    public string HandbookParentId { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public JsonElement Properties { get; set; }

    [JsonPropertyName("safeContainerMode")]
    public SafeContainerMode SafeContainerMode { get; set; } = SafeContainerMode.All;

    [JsonPropertyName("safeContainerIds")]
    public List<string> SafeContainerIds { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<SafeContainerMode>))]
public enum SafeContainerMode
{
    All,
    Include,
    Exclude,
}
