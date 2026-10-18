using FindThatBook.Api.Features.Books.Search.Candidates;

namespace FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;

/// <summary>
/// How a candidate work compares with the most likely reading: the facts its tier was decided from.
/// Used inside <see cref="Services.BookSearchService"/>; never sent to the client.
/// </summary>
public class CandidateMatch
{
    public required TitleMatch Title { get; init; }

    /// <summary>How closely the reading's author matches one of the work's primary authors.</summary>
    public required AuthorMatch Author { get; init; }

    /// <summary>The primary author who matched the reading's author, when one did.</summary>
    public CandidateAuthor? MatchedAuthor { get; init; }

    /// <summary>
    /// How closely the reading's author matches one of the work's contributors instead, such as its adapter.
    /// <see cref="AuthorMatch.None"/> when no contributor matched, or when a primary author already did.
    /// </summary>
    public AuthorMatch Contributor { get; init; } = AuthorMatch.None;

    /// <summary>The contributor who matched the reading's author, when one did.</summary>
    public CandidateAuthor? MatchedContributor { get; init; }
}
