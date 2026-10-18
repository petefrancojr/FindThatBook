using System.Net;
using FindThatBook.Api.Integrations.OpenLibrary;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Tests.Integrations.OpenLibrary;

/// <summary>
/// Requirement 3a: search Open Library with /search.json and fetch details from /works/{id}.json,
/// /authors/{id}.json and /authors/{id}/works.json.
/// </summary>
public class OpenLibraryClientTests
{
    private static OpenLibraryClient CreateClient(StubHttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://openlibrary.test/"), Timeout = timeout ?? TimeSpan.FromSeconds(5) },
            Options.Create(new OpenLibraryOptions { BaseUrl = "https://openlibrary.test/", TimeoutSeconds = 5 }),
            NullLogger<OpenLibraryClient>.Instance);

    private static string QueryOf(StubHttpMessageHandler handler) => Uri.UnescapeDataString(Assert.Single(handler.Requests).Uri.Query);

    [Fact]
    public async Task SearchesWorksWithTheFieldsThatAreSet()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{ "numFound": 0, "docs": [] }""");

        await CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Title = "the hobbit", Author = "j r r tolkien", Query = "1937", Limit = 10 });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/search.json", request.Uri.AbsolutePath);
        var query = QueryOf(handler);
        Assert.Contains("title=the hobbit", query);
        Assert.Contains("author=j r r tolkien", query);
        Assert.Contains("q=1937", query);
        Assert.Contains("limit=10", query);
        // Without fields, Open Library returns every field, including edition lists hundreds of entries long.
        Assert.Contains($"fields={string.Join(',', WorkSearchRequest.DefaultFields)}", query);
        Assert.DoesNotContain("subject=", query);
    }

    [Fact]
    public async Task ReadsSearchResults()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """
            { "numFound": 1, "docs": [{
              "key": "/works/OL219602W", "title": "The Hobbit", "author_name": ["Charles Dixon", "J.R.R. Tolkien"],
              "author_key": ["OL2453782A", "OL26320A"], "contributor": ["Charles Dixon (Adapter)"],
              "first_publish_year": 1990, "cover_i": 8406766, "edition_count": 10 }] }
            """);

        var doc = Assert.Single((await CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Title = "the hobbit" })).Docs);

        Assert.Equal("OL219602W", doc.WorkId);
        Assert.Equal(["Charles Dixon", "J.R.R. Tolkien"], doc.AuthorNames);
        Assert.Equal(["OL2453782A", "OL26320A"], doc.AuthorKeys);
        Assert.Equal(["Charles Dixon (Adapter)"], doc.Contributors);
        Assert.Equal(1990, doc.FirstPublishYear);
        Assert.Equal(8406766, doc.CoverId);
        Assert.Equal(10, doc.EditionCount);
        Assert.True(doc.IsWork);
    }

    [Fact]
    public async Task RecognisesAnEditionListedAsAWork()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{ "docs": [{ "key": "/works/OL8866470M", "title": "Harry Potter Activities Plus" }] }""");

        var doc = Assert.Single((await CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Query = "harry potter" })).Docs);

        Assert.False(doc.IsWork);
    }

    [Fact]
    public async Task RefusesASearchWithNothingToSearchFor()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Query = " " }));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PassesOnAQueryOpenLibraryRefusesWithItsReasons()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.UnprocessableEntity, """
            { "detail": [{ "msg": "Value error, ('Invalid query; the following queries are not allowed: the',)", "input": "the" }] }
            """);

        var refusal = await Assert.ThrowsAsync<OpenLibraryQueryRejectedException>(() =>
            CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Query = "the" }));

        Assert.Equal(["Invalid query; the following queries are not allowed: the"], refusal.Reasons);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task TreatsAnErrorStatusFromSearchAsAnOutage(HttpStatusCode status)
    {
        var handler = StubHttpMessageHandler.Returning(status, "oops", "text/plain");

        await Assert.ThrowsAsync<OpenLibraryUnavailableException>(() => CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Query = "dune" }));
    }

    [Fact]
    public async Task TreatsAnUnreachableServiceAsAnOutage()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("no route"));

        await Assert.ThrowsAsync<OpenLibraryUnavailableException>(() => CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Query = "dune" }));
    }

    [Fact]
    public async Task TreatsATimeoutAsAnOutage()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAsync<OpenLibraryUnavailableException>(() =>
            CreateClient(handler, TimeSpan.FromMilliseconds(50)).SearchWorksAsync(new WorkSearchRequest { Query = "dune" }));
    }

    [Fact]
    public async Task TreatsABodyThatIsNotJsonAsAnOutage()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "<html>maintenance</html>", "text/html");

        await Assert.ThrowsAsync<OpenLibraryUnavailableException>(() => CreateClient(handler).SearchWorksAsync(new WorkSearchRequest { Query = "dune" }));
    }

    [Fact]
    public async Task GetsAWorkWithItsAuthors()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """
            { "key": "/works/OL27482W", "title": "The Hobbit",
              "authors": [{ "author": { "key": "/authors/OL26320A" }, "type": { "key": "/type/author_role" } }],
              "description": { "type": "/type/text", "value": "Bilbo Baggins is a hobbit." } }
            """);

        var work = await CreateClient(handler).GetWorkAsync("OL27482W");

        Assert.Equal("/works/OL27482W.json", Assert.Single(handler.Requests).Uri.AbsolutePath);
        Assert.Equal("OL26320A", Assert.Single(work!.Authors).AuthorId);
        Assert.Equal("Bilbo Baggins is a hobbit.", work.Description);
    }

    [Fact]
    public async Task GetsAnAuthor()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{ "key": "/authors/OL26320A", "name": "J.R.R. Tolkien", "bio": "English writer." }""");

        var author = await CreateClient(handler).GetAuthorAsync("OL26320A");

        Assert.Equal("/authors/OL26320A.json", Assert.Single(handler.Requests).Uri.AbsolutePath);
        Assert.Equal("J.R.R. Tolkien", author!.Name);
        Assert.Equal("English writer.", author.Bio);
    }

    [Fact]
    public async Task GetsAPageOfAnAuthorsWorks()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """
            { "size": 350, "entries": [{ "key": "/works/OL27482W", "title": "The Hobbit" }], "links": { "next": "/authors/OL26320A/works.json?offset=1" } }
            """);

        var works = await CreateClient(handler).GetAuthorWorksAsync("OL26320A", limit: 1, offset: 0);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/authors/OL26320A/works.json", request.Uri.AbsolutePath);
        Assert.Contains("limit=1", request.Uri.Query);
        Assert.Equal(350, works!.Size);
        Assert.Equal("OL27482W", Assert.Single(works.Entries).WorkId);
    }

    [Fact]
    public async Task ReturnsNullForAWorkOrAuthorThatDoesNotExist()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.NotFound, """{ "error": "notfound" }""");
        var client = CreateClient(handler);

        Assert.Null(await client.GetWorkAsync("OL1W"));
        Assert.Null(await client.GetAuthorAsync("OL1A"));
        Assert.Null(await client.GetAuthorWorksAsync("OL1A"));
    }

    [Fact]
    public async Task AcceptsAnIdInAnyCase()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{ "key": "/works/OL27482W", "title": "The Hobbit" }""");

        await CreateClient(handler).GetWorkAsync("ol27482w");

        Assert.Equal("/works/OL27482W.json", Assert.Single(handler.Requests).Uri.AbsolutePath);
    }

    [Theory]
    [InlineData("OL26320A")]
    [InlineData("/works/OL27482W")]
    [InlineData("OL27482W/../../authors/OL26320A")]
    [InlineData("")]
    public async Task RefusesAnythingButABareWorkId(string id)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(handler).GetWorkAsync(id));
        Assert.Empty(handler.Requests);
    }
}
