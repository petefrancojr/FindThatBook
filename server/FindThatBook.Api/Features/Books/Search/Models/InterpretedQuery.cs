namespace FindThatBook.Api.Features.Books.Search.Models;

/// <summary>How the messy search text was understood. Either field may be missing.</summary>
public class InterpretedQuery
{
    public string? Title { get; init; }
    public string? Author { get; init; }
}
