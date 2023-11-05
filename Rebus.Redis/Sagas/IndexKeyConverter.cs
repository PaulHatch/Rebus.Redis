using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Sagas;

internal class IndexKeyConverter : JsonConverter<IndexKey>
{
    public override IndexKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        RedisKey key = default;
        RedisValue hashField = default;

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object.");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            switch (reader.GetString())
            {
                case "key" when reader.Read():
                    key = reader.GetString();
                    break;
                case "hashField" when reader.Read():
                    hashField = reader.GetString();
                    break;
            }
        }

        return new IndexKey(key, hashField);
    }

    public override void Write(Utf8JsonWriter writer, IndexKey value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("key", value.Key);
        writer.WriteString("hashField", value.HashField);
        writer.WriteEndObject();
    }
}