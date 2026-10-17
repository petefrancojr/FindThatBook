namespace FindThatBook.Api.Features.Books.Search.Models;

/// <summary>One way Gemini read the search: the book it may be, and the words left over.</summary>
public class QueryReading
{
    private readonly IReadOnlyList<string> _keywords = [];

    public string? Title { get; init; }

    public string? Author { get; init; }

    /// <summary>Never null. A missing or null "keywords" in Gemini's JSON becomes an empty list.</summary>
    public IReadOnlyList<string> Keywords
    {
        get => _keywords;
        init => _keywords = value ?? [];
    }
}
