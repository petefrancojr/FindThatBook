using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;

namespace FindThatBook.Api.Features.Books.Search.Pipeline.Explanation;

/// <summary>Explains, in one sentence each, why ranked candidates matched the search.</summary>
public interface ICandidateExplainer
{
    /// <summary>
    /// Returns the candidates in the same order, each with an explanation of its tier. A candidate that couldn't be
    /// explained keeps a null explanation. <paramref name="reading"/> is the most likely reading, or null when there was none.
    /// </summary>
    Task<IReadOnlyList<BookCandidate>> ExplainAsync(string query, QueryReading? reading, IReadOnlyList<BookCandidate> candidates, CancellationToken cancellationToken = default);
}
