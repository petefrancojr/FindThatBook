using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>Explains, in one sentence each, why ranked candidates matched the search.</summary>
public interface ICandidateExplainer
{
    /// <summary>
    /// Returns the candidates in the same order, each with an explanation of its tier. A candidate that couldn't be
    /// explained keeps a null explanation. <paramref name="reading"/> is the most likely reading, or null when there was none.
    /// </summary>
    Task<IReadOnlyList<BookCandidate>> ExplainAsync(string query, QueryReading? reading, IReadOnlyList<BookCandidate> candidates, CancellationToken cancellationToken = default);
}
