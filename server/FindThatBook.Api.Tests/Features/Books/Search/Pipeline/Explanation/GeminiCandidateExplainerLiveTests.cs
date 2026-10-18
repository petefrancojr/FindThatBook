using System.Text.RegularExpressions;
using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Explanation;
using FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;
using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Tests.Integrations.Gemini;
using Microsoft.Extensions.Logging.Abstractions;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Explanation;

/// <summary>
/// Sends the explanation prompt to the real Gemini. Skipped unless LIVE_TESTS=true:
/// docker compose run --rm -e LIVE_TESTS=true server-tests dotnet test FindThatBook.Api.Tests --filter Category=Live
/// </summary>
/// <remarks>
/// Gemini's wording varies from run to run, so these check what the brief asks of an explanation (grounded in the
/// fetched fields, naming the primary author and contributors) rather than exact sentences.
/// </remarks>
[Trait("Category", "Live")]
public partial class GeminiCandidateExplainerLiveTests
{
    private static async Task<IReadOnlyList<BookCandidate>> ExplainAsync(string query, QueryReading reading, params BookCandidate[] candidates)
    {
        var ranked = CandidateRanker.Rank(reading, candidates);
        return await new GeminiCandidateExplainer(LiveTests.CreateGeminiClient(), NullLogger<GeminiCandidateExplainer>.Instance)
            .ExplainAsync(query, reading, ranked);
    }

    private static readonly BookCandidate DixonsHobbit = Candidate(
        Work("OL219602W", "The Hobbit", ["Charles Dixon", "Sean Deming", "J.R.R. Tolkien"], year: 1990),
        Contributor("Charles Dixon", "Adapter"),
        Contributor("Sean Deming", "Adapter"),
        Primary("J.R.R. Tolkien"));

    private static readonly BookCandidate TolkiensHobbit = Candidate(Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien"], year: 1937), Primary("J.R.R. Tolkien"));

    [LiveFact]
    public async Task NamesThePrimaryAuthorAndTheAdaptersRole()
    {
        var explained = await ExplainAsync("dixon hobbit", ReadingOf("the hobbit", "charles dixon"), DixonsHobbit);

        var explanation = Assert.Single(explained).Explanation;
        Assert.NotNull(explanation);
        Assert.Contains("Tolkien", explanation);
        Assert.Contains("Dixon", explanation);
        Assert.Contains("adapt", explanation, StringComparison.OrdinalIgnoreCase);
    }

    [LiveFact]
    public async Task DoesNotCallAListedAuthorPrimary()
    {
        var goodOmens = Candidate(Work("OL453936W", "Good Omens", ["Terry Pratchett", "Neil Gaiman"], year: 1990), Listed("Terry Pratchett"), Listed("Neil Gaiman"));

        var explanation = Assert.Single(await ExplainAsync("good omens", ReadingOf("good omens"), goodOmens)).Explanation;

        Assert.NotNull(explanation);
        Assert.Contains("Pratchett", explanation);
        Assert.Contains("Gaiman", explanation);
        Assert.DoesNotContain("primary", explanation, StringComparison.OrdinalIgnoreCase);
    }

    [LiveFact]
    public async Task ExplainsEveryBookInOneShortSentenceUsingOnlyItsOwnYear()
    {
        var explained = await ExplainAsync("tolkien hobbit", ReadingOf("the hobbit", "j r r tolkien"), TolkiensHobbit, DixonsHobbit);

        Assert.Equal(2, explained.Count);
        Assert.All(explained, candidate =>
        {
            Assert.False(string.IsNullOrWhiteSpace(candidate.Explanation));
            Assert.True(candidate.Explanation!.Length <= 300, $"Too long for one sentence: {candidate.Explanation}");

            // Grounded: any year mentioned is the year Open Library gave for this book.
            var years = YearPattern().Matches(candidate.Explanation).Select(match => int.Parse(match.Value));
            Assert.All(years, year => Assert.Equal(candidate.Work.FirstPublishYear, year));
        });
    }

    [GeneratedRegex(@"\b(1[5-9]|20)\d{2}\b")]
    private static partial Regex YearPattern();
}
