using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Integrations.Gemini.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Reading;

/// <summary>Requirement 2a: use AI to extract { title?, author?, keywords[] } from the messy search.</summary>
public class GeminiQueryReaderTests
{
    private static GeminiQueryReader CreateReader(FakeGeminiClient gemini) => new(gemini, NullLogger<GeminiQueryReader>.Instance);

    [Fact]
    public async Task SendsTheSearchToGeminiAsTheUsersMessage()
    {
        var gemini = FakeGeminiClient.Answering("""{ "readings": [] }""");

        await CreateReader(gemini).ReadAsync("tolkien hobbit illustrated deluxe 1937");

        var content = Assert.Single(Assert.Single(gemini.Requests).Contents);
        Assert.Equal("user", content.Role);
        Assert.Equal("tolkien hobbit illustrated deluxe 1937", Assert.Single(content.Parts).Text);
    }

    [Fact]
    public async Task AsksForJsonWithAnOptionalTitleAndAuthorAndAKeywordList()
    {
        var gemini = FakeGeminiClient.Answering("""{ "readings": [] }""");

        await CreateReader(gemini).ReadAsync("dickens");

        var config = Assert.Single(gemini.Requests).GenerationConfig!;
        Assert.Equal(0, config.Temperature);
        Assert.Equal("application/json", config.ResponseMimeType);

        var reading = config.ResponseJsonSchema!["properties"]!["readings"]!["items"]!;
        Assert.Equal("""["string","null"]""", reading["properties"]!["title"]!["type"]!.ToJsonString());
        Assert.Equal("""["string","null"]""", reading["properties"]!["author"]!["type"]!.ToJsonString());
        Assert.Equal("""{"type":"array","items":{"type":"string"}}""", reading["properties"]!["keywords"]!.ToJsonString());
        Assert.Equal("""["keywords"]""", reading["required"]!.ToJsonString());
    }

    [Fact]
    public async Task ReturnsTheReadingsMostLikelyFirst()
    {
        var gemini = FakeGeminiClient.Answering("""
            { "readings": [
              { "title": "The Hobbit", "author": "J.R.R. Tolkien", "keywords": ["illustrated", "deluxe", "1937"] },
              { "title": "The Hobbit", "keywords": [] }
            ] }
            """);

        var readings = await CreateReader(gemini).ReadAsync("tolkien hobbit illustrated deluxe 1937");

        Assert.Equal(2, readings.Count);
        Assert.Equal("the hobbit", readings[0].Title);
        Assert.Equal("j r r tolkien", readings[0].Author);
        Assert.Equal(["illustrated", "deluxe", "1937"], readings[0].Keywords);
        Assert.Null(readings[1].Author);
    }

    [Fact]
    public async Task NormalizesWhatGeminiReturns()
    {
        var gemini = FakeGeminiClient.Answering("""
            { "readings": [{ "title": "Pride & Prejudice", "author": "Jane Austen", "keywords": ["Bennet", "bennet", "!!!"] }] }
            """);

        var reading = Assert.Single(await CreateReader(gemini).ReadAsync("austen bennet"));

        Assert.Equal("pride prejudice", reading.Title);
        Assert.Equal("jane austen", reading.Author);
        Assert.Equal(["bennet"], reading.Keywords);
    }

    [Fact]
    public async Task DropsAReadingLeftWithNothingInIt()
    {
        var gemini = FakeGeminiClient.Answering("""
            { "readings": [{ "title": "...", "author": " ", "keywords": ["?"] }, { "author": "Charles Dickens", "keywords": [] }] }
            """);

        var reading = Assert.Single(await CreateReader(gemini).ReadAsync("dickens"));

        Assert.Equal("charles dickens", reading.Author);
    }

    [Theory]
    [InlineData("""{ "readings": [] }""")]
    [InlineData("""{ "readings": null }""")]
    [InlineData("""{ }""")]
    [InlineData("null")]
    [InlineData("not json")]
    public async Task ReturnsNoReadingsWhenTheAnswerIsEmptyOrInvalid(string answer)
    {
        var readings = await CreateReader(FakeGeminiClient.Answering(answer)).ReadAsync("dune");

        Assert.Empty(readings);
    }

    [Fact]
    public async Task ReturnsNoReadingsWhenGeminiBlocksTheAnswer()
    {
        var gemini = new FakeGeminiClient(_ => new GenerateContentResponse { PromptFeedback = new PromptFeedback { BlockReason = "SAFETY" } });

        Assert.Empty(await CreateReader(gemini).ReadAsync("dune"));
    }

    [Fact]
    public async Task ToleratesAMissingOrNullKeywordList()
    {
        var gemini = FakeGeminiClient.Answering("""{ "readings": [{ "title": "Dune" }, { "title": "Dune Messiah", "keywords": null }] }""");

        var readings = await CreateReader(gemini).ReadAsync("dune");

        Assert.All(readings, reading => Assert.Empty(reading.Keywords));
    }
}
