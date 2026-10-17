using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>
/// One work in a search result. Every field is optional: Open Library leaves out fields a work
/// doesn't have and fields that weren't asked for.
/// </summary>
public class WorkSearchDoc
{
    /// <summary>The work's path, for example "/works/OL27482W".</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; init; }

    /// <summary>
    /// Every name credited on any edition, which can include illustrators, editors and translators,
    /// not just the work's authors.
    /// </summary>
    [JsonPropertyName("author_name")]
    public IReadOnlyList<string> AuthorNames { get; init; } = [];

    /// <summary>Author IDs in the same order as <see cref="AuthorNames"/>, for example "OL26320A".</summary>
    [JsonPropertyName("author_key")]
    public IReadOnlyList<string> AuthorKeys { get; init; } = [];

    /// <summary>
    /// Everyone credited on the work's editions, as library records write them. Some carry their role in brackets,
    /// for example "Charles Dixon (Adapter)" or "David Wenzel (Illustrator)"; many have no role, for example
    /// "Tolkien, J. R. R. 1892-1973.".
    /// </summary>
    [JsonPropertyName("contributor")]
    public IReadOnlyList<string> Contributors { get; init; } = [];

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; init; }

    /// <summary>The cover image ID, used in https://covers.openlibrary.org/b/id/{id}-M.jpg.</summary>
    [JsonPropertyName("cover_i")]
    public long? CoverId { get; init; }

    /// <summary>The edition the cover comes from, for example "OL51711263M".</summary>
    [JsonPropertyName("cover_edition_key")]
    public string? CoverEditionKey { get; init; }

    [JsonPropertyName("edition_count")]
    public int? EditionCount { get; init; }

    /// <summary>MARC language codes of the work's editions, for example "eng" or "fre".</summary>
    [JsonPropertyName("language")]
    public IReadOnlyList<string> Languages { get; init; } = [];

    [JsonPropertyName("has_fulltext")]
    public bool? HasFulltext { get; init; }

    /// <summary>The best-matching edition. Only present when "editions" was in the field list.</summary>
    [JsonPropertyName("editions")]
    public EditionSearchResults? Editions { get; init; }

    /// <summary>The work's ID without its path, for example "OL27482W". Null when there is no key.</summary>
    [JsonIgnore]
    public string? WorkId => OpenLibraryKeys.ToId(Key);

    /// <summary>
    /// False for an edition that was never attached to a work. Search lists it as a work under the
    /// edition's own ID ("/works/OL8866470M"), and there is no work record to get for it, so only call
    /// <see cref="IOpenLibraryClient.GetWorkAsync"/> when this is true.
    /// </summary>
    [JsonIgnore]
    public bool IsWork => OpenLibraryKeys.IsWorkId(WorkId);
}
