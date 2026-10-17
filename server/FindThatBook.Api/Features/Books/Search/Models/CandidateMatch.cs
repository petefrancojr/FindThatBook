namespace FindThatBook.Api.Features.Books.Search.Models;

/// <summary>
/// How a candidate work compares with the most likely reading: the facts its tier was decided from.
/// Used inside <see cref="Services.BookSearchService"/>; never sent to the client.
/// </summary>
public class CandidateMatch
{
    public required TitleMatch Title { get; init; }

    public required AuthorMatch Author { get; init; }

    /// <summary>The person on the work who matched the reading's author, when one did.</summary>
    public CandidateAuthor? MatchedAuthor { get; init; }
}
