namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>
/// The query parameters for /search.json. At least one of Query, Title, Author or Subject is required.
/// Unset parameters are left out, so Open Library uses its own defaults.
/// </summary>
public class WorkSearchRequest
{
    /// <summary>
    /// The fields asked for when <see cref="Fields"/> is not set: the ones <see cref="WorkSearchDoc"/> reads.
    /// Without a field list Open Library returns every field, including very long edition and ISBN lists.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultFields =
    [
        "key",
        "title",
        "subtitle",
        "author_name",
        "author_key",
        "contributor",
        "first_publish_year",
        "cover_i",
        "cover_edition_key",
        "edition_count",
        "language",
        "has_fulltext",
    ];

    /// <summary>"q": free text, or Solr syntax such as "language:fre".</summary>
    public string? Query { get; init; }

    /// <summary>"title": matches the work's title.</summary>
    public string? Title { get; init; }

    /// <summary>"author": matches author names.</summary>
    public string? Author { get; init; }

    /// <summary>"subject": matches the work's subjects.</summary>
    public string? Subject { get; init; }

    /// <summary>
    /// "fields": which fields each result includes. Defaults to <see cref="DefaultFields"/>.
    /// Add "editions" (or "editions.key", "editions.title", ...) to get the best-matching edition of each work.
    /// </summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>"sort": for example "new", "old", "random" or "key". Relevance when not set.</summary>
    public string? Sort { get; init; }

    /// <summary>"lang": a two-letter ISO 639-1 code. Prefers editions in that language without excluding others.</summary>
    public string? Language { get; init; }

    /// <summary>"limit": results per page.</summary>
    public int? Limit { get; init; }

    /// <summary>"page": starts at 1. Use either Page or Offset.</summary>
    public int? Page { get; init; }

    /// <summary>"offset": how many results to skip. Use either Page or Offset.</summary>
    public int? Offset { get; init; }
}
