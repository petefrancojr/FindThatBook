using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>
/// Open Library's answer to /authors/{id}/works.json: one page of the author's works.
/// The works are not ranked by popularity or relevance.
/// </summary>
public class AuthorWorksResponse
{
    /// <summary>How many works the author has in total, across every page.</summary>
    [JsonPropertyName("size")]
    public int Size { get; init; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<WorkRecord> Entries { get; init; } = [];

    [JsonPropertyName("links")]
    public AuthorWorksLinks? Links { get; init; }
}

/// <summary>Paths to this page and its neighbours, for example "/authors/OL26320A/works.json?limit=2&amp;offset=2".</summary>
public class AuthorWorksLinks
{
    [JsonPropertyName("self")]
    public string? Self { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    /// <summary>Null on the last page.</summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }

    /// <summary>Null on the first page.</summary>
    [JsonPropertyName("prev")]
    public string? Prev { get; init; }
}
