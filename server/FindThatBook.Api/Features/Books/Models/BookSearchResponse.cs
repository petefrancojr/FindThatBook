namespace FindThatBook.Api.Features.Books.Models;

public class BookSearchResponse
{
    public required string Query { get; init; }
    public required InterpretedQuery InterpretedAs { get; init; }
    public required IReadOnlyList<BookResult> Results { get; init; }
}
