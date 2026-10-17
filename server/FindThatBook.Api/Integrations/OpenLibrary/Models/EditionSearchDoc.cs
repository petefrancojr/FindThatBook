using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>One edition inside a search result's "editions" object.</summary>
public class EditionSearchDoc
{
    /// <summary>The edition's path, for example "/books/OL51709286M".</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>MARC language codes, for example "eng".</summary>
    [JsonPropertyName("language")]
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>The edition's ID without its path, for example "OL51709286M". Null when there is no key.</summary>
    [JsonIgnore]
    public string? EditionId => OpenLibraryKeys.ToId(Key);
}
