namespace FindThatBook.Api.Features.Books.Search.Pipeline.Explanation;

/// <summary>
/// The JSON Gemini returns for the explanation prompt: { "explanations": [ ... ] }.
/// Used inside <see cref="Services.BookSearchService"/>; never sent to the client.
/// </summary>
public class CandidateExplanations
{
    private readonly IReadOnlyList<CandidateExplanation> _explanations = [];

    /// <summary>Never null.</summary>
    public IReadOnlyList<CandidateExplanation> Explanations
    {
        get => _explanations;
        init => _explanations = value ?? [];
    }
}
