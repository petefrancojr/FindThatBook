using System.Text.Json.Serialization;

namespace FindThatBook.Api.Features.Books.Search.DTOs;

public class BookResult
{
    public required string Title { get; init; }

    /// <summary>The people in <see cref="Authors"/>, joined with commas: the brief's single author field.</summary>
    public required string Author { get; init; }

    /// <summary>
    /// The people who wrote the book: its primary authors, or everyone listed as an author when Open Library
    /// doesn't show which of them wrote it.
    /// </summary>
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>People who worked on the book without writing it, with their role when one was recorded.</summary>
    public IReadOnlyList<BookContributor> Contributors { get; init; } = [];

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; init; }

    [JsonPropertyName("open_library_id")]
    public required string OpenLibraryId { get; init; }

    public required string Url { get; init; }

    [JsonPropertyName("cover_url")]
    public string? CoverUrl { get; init; }

    public required string Tier { get; init; }
    public required string Explanation { get; init; }
}
