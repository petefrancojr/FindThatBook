using System.Text.Json;
using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>
/// Reads Open Library text fields such as "description" and "bio", which come either as a plain
/// string or as { "type": "/type/text", "value": "..." }. Both become the string.
/// </summary>
public class TextValueJsonConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.StartObject:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    return document.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String
                        ? value.GetString()
                        : null;
                }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
