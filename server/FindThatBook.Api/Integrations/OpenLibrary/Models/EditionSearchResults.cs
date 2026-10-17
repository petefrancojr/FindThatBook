using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>The "editions" object inside a search result. Open Library currently returns one edition in Docs.</summary>
public class EditionSearchResults
{
    /// <summary>How many of the work's editions matched.</summary>
    [JsonPropertyName("numFound")]
    public int NumFound { get; init; }

    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("numFoundExact")]
    public bool NumFoundExact { get; init; }

    [JsonPropertyName("docs")]
    public IReadOnlyList<EditionSearchDoc> Docs { get; init; } = [];
}
