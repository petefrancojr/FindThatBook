using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>A link to another record, for example { "key": "/authors/OL26320A" }.</summary>
public class KeyReference
{
    /// <summary>The record's path, for example "/authors/OL26320A".</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>The record's ID without its path, for example "OL26320A". Null when there is no key.</summary>
    [JsonIgnore]
    public string? Id => OpenLibraryKeys.ToId(Key);
}
