using System.Collections.Generic;
using System.Text.Json.Serialization;

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
}
