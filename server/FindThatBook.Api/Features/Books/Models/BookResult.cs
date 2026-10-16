using System.Text.Json.Serialization;

namespace FindThatBook.Api.Features.Books.Models;

public class BookResult
{
    public required string Title { get; init; }
    public required string Author { get; init; }

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
