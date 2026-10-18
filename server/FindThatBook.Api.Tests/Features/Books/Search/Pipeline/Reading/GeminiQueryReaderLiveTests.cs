using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Tests.Integrations.Gemini;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Reading;

/// <summary>
/// Sends the reading prompt to the real Gemini with the brief's example searches. Skipped unless LIVE_TESTS=true:
/// docker compose run --rm -e LIVE_TESTS=true server-tests dotnet test FindThatBook.Api.Tests --filter Category=Live
/// </summary>
/// <remarks>
/// Gemini's wording varies from run to run, so these check what the prompt promises rather than exact readings.
/// Readings arrive normalized: lowercase, no accents, initials apart.
/// </remarks>
[Trait("Category", "Live")]
public class GeminiQueryReaderLiveTests
{
    private static Task<IReadOnlyList<QueryReading>> ReadAsync(string query) =>
        new GeminiQueryReader(LiveTests.CreateGeminiClient(), NullLogger<GeminiQueryReader>.Instance).ReadAsync(query);

    [LiveFact]
    public async Task PutsEverythingFromADenseSearchInTheFirstReading()
    {
        var first = (await ReadAsync("tolkien hobbit illustrated deluxe 1937"))[0];

        Assert.Contains("hobbit", first.Title);
        Assert.Contains("tolkien", first.Author);
        Assert.Contains("1937", first.Keywords);
    }

    [LiveTheory]
    [InlineData("dickens", "dickens")]
    [InlineData("austen bennet", "austen")]
    public async Task ReadsAnAuthorFromASparseOrAmbiguousSearch(string query, string author)
    {
        var first = (await ReadAsync(query))[0];

        Assert.Contains(author, first.Author);
    }

    [LiveFact]
    public async Task CompletesAPartialTitle()
    {
        var first = (await ReadAsync("tale two cities"))[0];

        Assert.Equal("a tale of two cities", first.Title);
    }

    [LiveFact]
    public async Task ReadsACharacterHintAsPartOfTheSearch()
    {
        var readings = await ReadAsync("mark huckleberry");

        Assert.Contains(readings, reading =>
            (reading.Title?.Contains("huckleberry") ?? false) || reading.Keywords.Any(keyword => keyword.Contains("huckleberry")));
    }

    [LiveTheory]
    [InlineData("hobbit")]
    [InlineData("dune")]
    public async Task NeverAddsAnAuthorTheSearchDidNotGive(string query)
    {
        var readings = await ReadAsync(query);

        Assert.NotEmpty(readings);
        Assert.All(readings, reading => Assert.Null(reading.Author));
    }

    [LiveFact]
    public async Task NeverAddsATitleTheSearchDidNotGive()
    {
        var readings = await ReadAsync("austen bennet");

        Assert.All(readings, reading => Assert.Null(reading.Title));
    }
}
