using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace ItemGen.Converters;

public class MongoIdJsonConverter : JsonConverter<MongoId>
{
    public override MongoId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new MongoId(reader.GetString());
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        throw new JsonException($"Unable to convert {reader.TokenType} to MongoId.");
    }

    public override void Write(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
