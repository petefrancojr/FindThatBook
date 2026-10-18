using System.Net;
using System.Text.Json;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;
using FindThatBook.Api.Integrations.OpenLibrary;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search;

/// <summary>
/// Runs searches through the real ASP.NET pipeline and every search stage, with Gemini and Open Library replaced
/// by fakes so no test calls the network. Covers the brief's request and response requirements end to end.
/// </summary>
public class BookSearchEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TolkienHobbitReadings = """
        { "readings": [{ "title": "The Hobbit", "author": "J.R.R. Tolkien", "keywords": ["illustrated", "deluxe", "1937"] }] }
        """;

    private static readonly WorkSearchDoc Hobbit = Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien|OL26320A"], editions: 482, year: 1937, coverId: 14627509);
    private static readonly WorkSearchDoc DixonsHobbit = Work("OL219602W", "The Hobbit", ["Charles Dixon|OL2453782A", "Sean Deming|OL758306A", "J.R.R. Tolkien|OL26320A"],
        editions: 10, year: 1990, contributors: ["Charles Dixon (Adapter)", "Sean Deming (Adapter)"]);

    /// <summary>Answers the reading prompt with the given readings and the explanation prompt with one sentence per id.</summary>
    private static FakeGeminiClient Gemini(string readings, Dictionary<string, string>? explanations = null) => new(request =>
    {
        var instruction = request.SystemInstruction?.Parts[0].Text ?? "";
        var text = instruction.Contains("You read messy library search text")
            ? readings
            : JsonSerializer.Serialize(new { explanations = (explanations ?? []).Select(pair => new { id = pair.Key, explanation = pair.Value }) });
        return new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = text }] } }] };
    });

    private HttpClient Client(IGeminiClient gemini, IOpenLibraryClient openLibrary) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(gemini);
            services.AddSingleton(openLibrary);
        })).CreateClient();

    private static async Task<JsonElement> ResultsOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("results").Clone();
    }

    [Fact]
    public async Task ReturnsTheBriefsFieldsForEachBook()
    {
        var client = Client(Gemini(TolkienHobbitReadings), FakeOpenLibraryClient.Finding(Hobbit));

        var response = await client.GetAsync("/api/books/search?q=tolkien%20hobbit%20illustrated%20deluxe%201937");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("tolkien hobbit illustrated deluxe 1937", json.RootElement.GetProperty("query").GetString());
        var book = json.RootElement.GetProperty("results")[0];
        Assert.Equal("The Hobbit", book.GetProperty("title").GetString());
        Assert.Equal("J.R.R. Tolkien", book.GetProperty("author").GetString());
        Assert.Equal(1937, book.GetProperty("first_publish_year").GetInt32());
        Assert.Equal("OL27482W", book.GetProperty("open_library_id").GetString());
        Assert.Equal("https://openlibrary.org/works/OL27482W", book.GetProperty("url").GetString());
        Assert.Equal("https://covers.openlibrary.org/b/id/14627509-M.jpg", book.GetProperty("cover_url").GetString());
        Assert.Equal("A", book.GetProperty("tier").GetString());
        Assert.False(string.IsNullOrWhiteSpace(book.GetProperty("explanation").GetString()));
    }

    [Fact]
    public async Task ExplainsWhoIsThePrimaryAuthorAndWhoIsAnAdapter()
    {
        var gemini = Gemini(
            """{ "readings": [{ "title": "The Hobbit", "author": "Charles Dixon", "keywords": [] }] }""",
            new() { ["OL219602W"] = "Exact title; Tolkien is primary author; Dixon listed as adapter." });
        var client = Client(gemini, FakeOpenLibraryClient.Finding(Hobbit, DixonsHobbit));

        var results = await ResultsOf(await client.GetAsync("/api/books/search?q=dixon%20hobbit"));

        var first = results[0];
        Assert.Equal("OL219602W", first.GetProperty("open_library_id").GetString());
        Assert.Equal("B", first.GetProperty("tier").GetString());
        Assert.Equal("J.R.R. Tolkien", first.GetProperty("author").GetString());
        Assert.Equal("Exact title; Tolkien is primary author; Dixon listed as adapter.", first.GetProperty("explanation").GetString());
    }

    [Fact]
    public async Task ReturnsTheAuthorsAndContributorsAsLists()
    {
        var gemini = Gemini("""{ "readings": [{ "title": "The Hobbit", "author": "Charles Dixon", "keywords": [] }] }""");
        var client = Client(gemini, FakeOpenLibraryClient.Finding(DixonsHobbit));

        var book = (await ResultsOf(await client.GetAsync("/api/books/search?q=dixon%20hobbit")))[0];

        Assert.Equal(["J.R.R. Tolkien"], book.GetProperty("authors").EnumerateArray().Select(author => author.GetString()));
        var contributors = book.GetProperty("contributors").EnumerateArray().ToList();
        Assert.Equal(["Charles Dixon", "Sean Deming"], contributors.Select(contributor => contributor.GetProperty("name").GetString()));
        Assert.All(contributors, contributor => Assert.Equal("Adapter", contributor.GetProperty("role").GetString()));
    }

    [Fact]
    public async Task ReturnsAnOrderedListBestMatchFirst()
    {
        var silmarillion = Work("OL27495W", "The Silmarillion", ["J.R.R. Tolkien|OL26320A"], editions: 100);
        var client = Client(Gemini(TolkienHobbitReadings), FakeOpenLibraryClient.Finding(silmarillion, Hobbit));

        var results = await ResultsOf(await client.GetAsync("/api/books/search?q=tolkien%20hobbit"));

        Assert.Equal(["A", "E"], results.EnumerateArray().Select(book => book.GetProperty("tier").GetString()));
    }

    [Fact]
    public async Task ReturnsAtMostTenBooks()
    {
        var works = Enumerable.Range(1, 15).Select(index => Work($"OL{index}W", $"Book {index}", ["Charles Dickens|OL24638A"])).ToArray();
        var client = Client(Gemini("""{ "readings": [{ "author": "Charles Dickens", "keywords": [] }] }"""), FakeOpenLibraryClient.Finding(works));

        var results = await ResultsOf(await client.GetAsync("/api/books/search?q=dickens"));

        Assert.Equal(10, results.GetArrayLength());
    }

    [Theory]
    [InlineData("dickens")]
    [InlineData("tale two cities")]
    [InlineData("tolkien hobbit illustrated deluxe 1937")]
    [InlineData("mark huckleberry")]
    [InlineData("austen bennet")]
    public async Task AcceptsSparseDenseAndAmbiguousSearches(string query)
    {
        var client = Client(Gemini("""{ "readings": [] }"""), FakeOpenLibraryClient.Finding(Hobbit));

        var response = await client.GetAsync($"/api/books/search?q={Uri.EscapeDataString(query)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsNoBooksForASearchWithNoLettersOrDigits()
    {
        var openLibrary = FakeOpenLibraryClient.Finding(Hobbit);
        var client = Client(Gemini("""{ "readings": [] }"""), openLibrary);

        var results = await ResultsOf(await client.GetAsync("/api/books/search?q=!!!"));

        Assert.Equal(0, results.GetArrayLength());
        Assert.Empty(openLibrary.Searches);
    }

    [Theory]
    [InlineData("/api/books/search")]
    [InlineData("/api/books/search?q=")]
    [InlineData("/api/books/search?q=%20%20%20")]
    public async Task RejectsAMissingOrBlankSearch(string url)
    {
        var response = await Client(Gemini("""{ "readings": [] }"""), FakeOpenLibraryClient.Finding()).GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsASearchOver200Characters()
    {
        var response = await Client(Gemini("""{ "readings": [] }"""), FakeOpenLibraryClient.Finding()).GetAsync($"/api/books/search?q={new string('a', 201)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PassesOnOpenLibrarysReasonAsA422WhenItRefusesTheSearch()
    {
        var openLibrary = new FakeOpenLibraryClient(_ => throw new OpenLibraryQueryRejectedException(["Query too short, must be at least 3 characters"]));
        var client = Client(Gemini("""{ "readings": [] }"""), openLibrary);

        var response = await client.GetAsync("/api/books/search?q=of");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Query too short, must be at least 3 characters.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ReturnsA502WhenOpenLibraryIsUnavailable()
    {
        var openLibrary = new FakeOpenLibraryClient(_ => throw new OpenLibraryUnavailableException("Open Library returned 503."));
        var client = Client(Gemini(TolkienHobbitReadings), openLibrary);

        var response = await client.GetAsync("/api/books/search?q=tolkien%20hobbit");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Open Library is unavailable right now. Try again in a moment.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ReturnsTheBooksOtherSearchesFoundWhenOneOpenLibrarySearchFails()
    {
        var gemini = Gemini("""{ "readings": [{ "title": "The Hobbit", "author": "J.R.R. Tolkien", "keywords": [] }, { "title": "The Hobbit", "keywords": [] }] }""");
        var openLibrary = new FakeOpenLibraryClient(request =>
            request.Author is not null ? throw new OpenLibraryUnavailableException("Open Library timed out.") : [Hobbit]);

        var results = await ResultsOf(await Client(gemini, openLibrary).GetAsync("/api/books/search?q=tolkien%20hobbit"));

        Assert.Equal(2, openLibrary.Searches.Count);
        Assert.Equal("OL27482W", Assert.Single(results.EnumerateArray()).GetProperty("open_library_id").GetString());
    }

    [Fact]
    public async Task StillReturnsBooksWhenGeminiCannotReadTheSearch()
    {
        var gemini = new FakeGeminiClient(_ => new GenerateContentResponse { PromptFeedback = new PromptFeedback { BlockReason = "SAFETY" } });
        var openLibrary = FakeOpenLibraryClient.Finding(Hobbit);

        var results = await ResultsOf(await Client(gemini, openLibrary).GetAsync("/api/books/search?q=the%20hobbit"));

        Assert.Equal("the hobbit", Assert.Single(openLibrary.Searches).Query);
        Assert.Equal("OL27482W", results[0].GetProperty("open_library_id").GetString());
        Assert.StartsWith("Open Library lists", results[0].GetProperty("explanation").GetString());
    }
}
