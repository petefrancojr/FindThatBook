using System.Text.Json;
using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>A link to another record, for example { "key": "/authors/OL26320A" }.</summary>
[JsonConverter(typeof(KeyReferenceJsonConverter))]
public class KeyReference
{
    /// <summary>The record's path, for example "/authors/OL26320A".</summary>
    public string? Key { get; init; }

    /// <summary>The record's ID without its path, for example "OL26320A". Null when there is no key.</summary>
    public string? Id => OpenLibraryKeys.ToId(Key);
}

/// <summary>Reads a reference written as { "key": "..." } or, in some older records, as the bare path string.</summary>
public class KeyReferenceJsonConverter : JsonConverter<KeyReference>
{
    public override KeyReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new KeyReference { Key = reader.GetString() };
            case JsonTokenType.StartObject:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    var key = document.RootElement.TryGetProperty("key", out var value) && value.ValueKind == JsonValueKind.String
                        ? value.GetString()
                        : null;
                    return new KeyReference { Key = key };
                }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, KeyReference value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("key", value.Key);
        writer.WriteEndObject();
    }
}
