using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.DTOs;

public class BookSearchResponse
{
    public required string Query { get; init; }

    /// <summary>One ranked list of up to five books, best first.</summary>
    public required IReadOnlyList<BookResult> Results { get; init; }
}
