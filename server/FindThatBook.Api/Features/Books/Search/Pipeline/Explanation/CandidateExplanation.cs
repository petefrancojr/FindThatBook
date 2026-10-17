namespace FindThatBook.Api.Features.Books.Search.Pipeline.Explanation;

/// <summary>
/// Gemini's explanation of why one candidate matched.
/// Used inside <see cref="Services.BookSearchService"/>; never sent to the client.
/// </summary>
public class CandidateExplanation
{
    /// <summary>The candidate's Open Library ID, as it was sent, for example "OL27482W".</summary>
    public string Id { get; init; } = "";

    /// <summary>One sentence citing the candidate's own fields.</summary>
    public string Explanation { get; init; } = "";
}
