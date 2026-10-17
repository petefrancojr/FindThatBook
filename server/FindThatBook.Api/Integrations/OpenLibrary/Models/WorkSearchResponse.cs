using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>Open Library's answer to /search.json.</summary>
public class WorkSearchResponse
{
    /// <summary>How many works matched in total, across every page.</summary>
    [JsonPropertyName("numFound")]
    public int NumFound { get; init; }

    /// <summary>The position of the first result in this page, starting at 0.</summary>
    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("numFoundExact")]
    public bool NumFoundExact { get; init; }

    /// <summary>The works in this page, most relevant first unless a sort was asked for.</summary>
    [JsonPropertyName("docs")]
    public IReadOnlyList<WorkSearchDoc> Docs { get; init; } = [];
}
