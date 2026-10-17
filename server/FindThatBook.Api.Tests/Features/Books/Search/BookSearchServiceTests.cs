using FindThatBook.Api.Features.Books.Search.Services;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Api.Tests.Features.Books.Search;

public class BookSearchServiceTests
{
    private const string Readings = """{ "readings": [{ "title": "Pride and Prejudice", "author": "Jane Austen", "keywords": ["Bennet"] }] }""";

    private static BookSearchService CreateService(IGeminiClient gemini) =>
        new(gemini, NullLogger<BookSearchService>.Instance);

    [Fact]
    public async Task SendsTheSearchToGeminiAsTheUsersMessage()
    {
        var gemini = FakeGeminiClient.Answering(Readings);

        await CreateService(gemini).SearchAsync("austen bennet");

        var content = Assert.Single(Assert.Single(gemini.Requests).Contents);
        Assert.Equal("user", content.Role);
        Assert.Equal("austen bennet", Assert.Single(content.Parts).Text);
    }

    [Fact]
    public async Task KeepsTheInstructionsOutOfTheUsersMessage()
    {
        var gemini = FakeGeminiClient.Answering(Readings);

        await CreateService(gemini).SearchAsync("austen bennet");

        var request = Assert.Single(gemini.Requests);
        Assert.Contains("readings", Assert.Single(request.SystemInstruction!.Parts).Text);
        Assert.DoesNotContain("readings", request.Contents[0].Parts[0].Text);
    }

    [Fact]
    public async Task AsksForJsonThatFollowsTheReadingsSchema()
    {
        var gemini = FakeGeminiClient.Answering(Readings);

        await CreateService(gemini).SearchAsync("austen bennet");

        var config = Assert.Single(gemini.Requests).GenerationConfig!;
        Assert.Equal(0, config.Temperature);
        Assert.Equal("application/json", config.ResponseMimeType);
        var schema = config.ResponseJsonSchema!;
        Assert.Equal("""["readings"]""", schema["required"]!.ToJsonString());

        var readings = schema["properties"]!["readings"]!;
        Assert.Equal("array", (string?)readings["type"]);
        Assert.Equal(3, (int?)readings["maxItems"]);

        var reading = readings["items"]!;
        Assert.Equal("object", (string?)reading["type"]);
        Assert.Equal("""["keywords"]""", reading["required"]!.ToJsonString());
        Assert.Equal("""["string","null"]""", reading["properties"]!["title"]!["type"]!.ToJsonString());
        Assert.Equal("""["string","null"]""", reading["properties"]!["author"]!["type"]!.ToJsonString());
        Assert.Equal("""{"type":"array","items":{"type":"string"}}""", reading["properties"]!["keywords"]!.ToJsonString());
    }

    [Fact]
    public async Task ReturnsResultsForTheSearch()
    {
        var response = await CreateService(FakeGeminiClient.Answering(Readings)).SearchAsync("austen bennet");

        Assert.Equal("austen bennet", response.Query);
        Assert.NotEmpty(response.Results);
    }

    [Theory]
    [InlineData("""{ "readings": [{ "title": "Dune" }] }""")]
    [InlineData("""{ "readings": [{ "title": "Dune", "keywords": null }] }""")]
    [InlineData("""{ "readings": [{ "title": null, "author": null, "keywords": [] }] }""")]
    [InlineData("""{ "readings": [] }""")]
    [InlineData("""{ "readings": null }""")]
    [InlineData("""{ }""")]
    [InlineData("null")]
    [InlineData("not json")]
    public async Task StillReturnsResultsWhenGeminisAnswerIsIncompleteOrInvalid(string answer)
    {
        var response = await CreateService(FakeGeminiClient.Answering(answer)).SearchAsync("dune");

        Assert.Equal("dune", response.Query);
        Assert.NotEmpty(response.Results);
    }

    [Fact]
    public async Task StillReturnsResultsWhenGeminiGivesNoText()
    {
        var gemini = new FakeGeminiClient(_ => new GenerateContentResponse
        {
            PromptFeedback = new PromptFeedback { BlockReason = "SAFETY" },
        });

        var response = await CreateService(gemini).SearchAsync("austen bennet");

        Assert.NotEmpty(response.Results);
    }

    [Fact]
    public async Task LetsAGeminiOutageReachTheErrorHandler()
    {
        var gemini = new FakeGeminiClient(_ => throw new GeminiUnavailableException("Gemini returned 503."));

        await Assert.ThrowsAsync<GeminiUnavailableException>(() => CreateService(gemini).SearchAsync("austen bennet"));
    }
}
