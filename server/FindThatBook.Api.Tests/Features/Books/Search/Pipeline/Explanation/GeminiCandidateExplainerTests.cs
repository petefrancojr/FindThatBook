using System.Text.Json;
using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Explanation;
using FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;
using FindThatBook.Api.Integrations.Gemini.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Explanation;

/// <summary>
/// Requirement 2b: a short "why it matched" explanation for each returned candidate, grounded in fields we fetched.
/// The data quality note: explanations highlight the primary author and contributors.
/// </summary>
public class GeminiCandidateExplainerTests
{
    private static readonly BookCandidate DixonsHobbit = CandidateRanker.Rank(
        ReadingOf("the hobbit", "charles dixon"),
        [Candidate(Work("OL219602W", "The Hobbit", ["Charles Dixon", "J.R.R. Tolkien"], year: 1990), Contributor("Charles Dixon", "Adapter"), Primary("J.R.R. Tolkien"))])[0];

    private static GeminiCandidateExplainer CreateExplainer(FakeGeminiClient gemini) => new(gemini, NullLogger<GeminiCandidateExplainer>.Instance);

    private static JsonElement SentCandidate(FakeGeminiClient gemini)
    {
        var message = Assert.Single(Assert.Single(gemini.Requests).Contents).Parts[0].Text!;
        return JsonDocument.Parse(message).RootElement.GetProperty("candidates")[0];
    }

    [Fact]
    public async Task SendsOnlyTheCandidatesOwnFieldsAndTheFactsBehindItsTier()
    {
        var gemini = FakeGeminiClient.Answering("""{ "explanations": [] }""");

        await CreateExplainer(gemini).ExplainAsync("dixon hobbit", ReadingOf("the hobbit", "charles dixon"), [DixonsHobbit]);

        var sent = SentCandidate(gemini);
        Assert.Equal("OL219602W", sent.GetProperty("id").GetString());
        Assert.Equal("B", sent.GetProperty("tier").GetString());
        Assert.Equal("exact", sent.GetProperty("title_match").GetString());
        Assert.Equal("only a contributor", sent.GetProperty("reading_author_on_work").GetString());
        Assert.Equal("The Hobbit", sent.GetProperty("title").GetString());
        Assert.Equal(1990, sent.GetProperty("first_publish_year").GetInt32());
    }

    [Fact]
    public async Task SendsThePrimaryAuthorAndContributorsWithTheirRoles()
    {
        var gemini = FakeGeminiClient.Answering("""{ "explanations": [] }""");

        await CreateExplainer(gemini).ExplainAsync("dixon hobbit", ReadingOf("the hobbit", "charles dixon"), [DixonsHobbit]);

        var sent = SentCandidate(gemini);
        Assert.Equal("J.R.R. Tolkien", sent.GetProperty("primary_authors")[0].GetString());
        var contributor = sent.GetProperty("contributors")[0];
        Assert.Equal("Charles Dixon", contributor.GetProperty("name").GetString());
        Assert.Equal("Adapter", contributor.GetProperty("role").GetString());
    }

    [Fact]
    public async Task AsksForJsonWithAnIdAndExplanationForEachCandidate()
    {
        var gemini = FakeGeminiClient.Answering("""{ "explanations": [] }""");

        await CreateExplainer(gemini).ExplainAsync("dixon hobbit", ReadingOf("the hobbit", "charles dixon"), [DixonsHobbit]);

        var config = Assert.Single(gemini.Requests).GenerationConfig!;
        Assert.Equal(0, config.Temperature);
        Assert.Equal("application/json", config.ResponseMimeType);
        var explanation = config.ResponseJsonSchema!["properties"]!["explanations"]!["items"]!;
        Assert.Equal("""["id","explanation"]""", explanation["required"]!.ToJsonString());
        Assert.Equal("string", (string?)explanation["properties"]!["explanation"]!["type"]);
    }

    [Fact]
    public async Task PutsEachExplanationOnItsCandidate()
    {
        var gemini = FakeGeminiClient.Answering("""
            { "explanations": [{ "id": "OL219602W", "explanation": "Exact title; Tolkien is primary author; Dixon listed as adapter." }] }
            """);

        var explained = Assert.Single(await CreateExplainer(gemini).ExplainAsync("dixon hobbit", null, [DixonsHobbit]));

        Assert.Equal("Exact title; Tolkien is primary author; Dixon listed as adapter.", explained.Explanation);
        Assert.Equal("B", explained.Tier);
    }

    [Theory]
    [InlineData("""{ "explanations": [{ "id": "OL999W", "explanation": "Another book." }] }""")]
    [InlineData("""{ "explanations": [{ "id": "OL219602W", "explanation": "  " }] }""")]
    [InlineData("""{ "explanations": [] }""")]
    [InlineData("not json")]
    public async Task LeavesTheExplanationEmptyWhenGeminiGivesNoUsableOne(string answer)
    {
        var explained = Assert.Single(await CreateExplainer(FakeGeminiClient.Answering(answer)).ExplainAsync("dixon hobbit", null, [DixonsHobbit]));

        Assert.Null(explained.Explanation);
        Assert.Equal("B", explained.Tier);
    }

    [Fact]
    public async Task LeavesTheExplanationEmptyWhenGeminiBlocksTheAnswer()
    {
        var gemini = new FakeGeminiClient(_ => new GenerateContentResponse { PromptFeedback = new PromptFeedback { BlockReason = "SAFETY" } });

        Assert.Null(Assert.Single(await CreateExplainer(gemini).ExplainAsync("dixon hobbit", null, [DixonsHobbit])).Explanation);
    }

    [Fact]
    public async Task KeepsTheCandidatesInTheirOrder()
    {
        var dune = CandidateRanker.Rank(null, [Candidate(Work("OL893414W", "Dune", ["Frank Herbert"]), Primary("Frank Herbert"))])[0];
        var gemini = FakeGeminiClient.Answering("""
            { "explanations": [{ "id": "OL893414W", "explanation": "Dune." }, { "id": "OL219602W", "explanation": "Hobbit." }] }
            """);

        var explained = await CreateExplainer(gemini).ExplainAsync("search", null, [DixonsHobbit, dune]);

        Assert.Equal(["OL219602W", "OL893414W"], explained.Select(candidate => candidate.Work.WorkId));
    }
}
