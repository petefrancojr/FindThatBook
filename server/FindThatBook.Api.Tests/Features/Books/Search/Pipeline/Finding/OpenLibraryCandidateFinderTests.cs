using FindThatBook.Api.Features.Books.Search.Pipeline.Finding;
using FindThatBook.Api.Integrations.OpenLibrary;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Finding;

/// <summary>Requirement 3a: search Open Library's /search.json for candidates, one search per reading.</summary>
public class OpenLibraryCandidateFinderTests
{
    private static OpenLibraryCandidateFinder CreateFinder(IOpenLibraryClient openLibrary) =>
        new(openLibrary, NullLogger<OpenLibraryCandidateFinder>.Instance);

    [Fact]
    public async Task SendsTheTitleAuthorAndKeywordsToTheirOwnSearchFields()
    {
        var openLibrary = FakeOpenLibraryClient.Finding();

        await CreateFinder(openLibrary).FindAsync([ReadingOf("the hobbit", "j r r tolkien", "illustrated", "1937")], "tolkien hobbit illustrated 1937");

        var search = Assert.Single(openLibrary.Searches);
        Assert.Equal("the hobbit", search.Title);
        Assert.Equal("j r r tolkien", search.Author);
        Assert.Equal("illustrated 1937", search.Query);
    }

    [Fact]
    public async Task SearchesOnlyTheFieldsAReadingHas()
    {
        var openLibrary = FakeOpenLibraryClient.Finding();

        await CreateFinder(openLibrary).FindAsync([ReadingOf(author: "charles dickens")], "dickens");

        var search = Assert.Single(openLibrary.Searches);
        Assert.Null(search.Title);
        Assert.Equal("charles dickens", search.Author);
        Assert.Null(search.Query);
    }

    [Fact]
    public async Task SearchesOncePerDistinctReading()
    {
        var openLibrary = FakeOpenLibraryClient.Finding();

        await CreateFinder(openLibrary).FindAsync(
            [ReadingOf("the hobbit", "j r r tolkien"), ReadingOf("the hobbit"), ReadingOf("the hobbit")],
            "tolkien hobbit");

        Assert.Equal(2, openLibrary.Searches.Count);
    }

    [Fact]
    public async Task SearchesTheNormalizedQueryWhenThereAreNoReadings()
    {
        var openLibrary = FakeOpenLibraryClient.Finding();

        await CreateFinder(openLibrary).FindAsync([], "tale two cities");

        Assert.Equal("tale two cities", Assert.Single(openLibrary.Searches).Query);
    }

    [Fact]
    public async Task ReturnsEachWorkOnceInTheOrderOfTheReadings()
    {
        var hobbit = Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien"]);
        var rings = Work("OL27448W", "The Lord of the Rings", ["J.R.R. Tolkien"]);
        var openLibrary = new FakeOpenLibraryClient(request => request.Author is not null ? [hobbit] : [rings, hobbit]);

        var works = await CreateFinder(openLibrary).FindAsync([ReadingOf("the hobbit", "j r r tolkien"), ReadingOf("the hobbit")], "tolkien hobbit");

        Assert.Equal(["OL27482W", "OL27448W"], works.Select(work => work.WorkId));
    }

    [Fact]
    public async Task SkipsARefusedSearchWhenOthersFoundWorks()
    {
        var dune = Work("OL893414W", "Dune", ["Frank Herbert"]);
        var openLibrary = new FakeOpenLibraryClient(request =>
            request.Query == "of" ? throw new OpenLibraryQueryRejectedException(["Query too short"]) : [dune]);

        var works = await CreateFinder(openLibrary).FindAsync([ReadingOf(keywords: "of"), ReadingOf("dune")], "dune of");

        Assert.Equal("OL893414W", Assert.Single(works).WorkId);
    }

    [Fact]
    public async Task PassesOnTheRefusalWithEveryReasonWhenEverySearchIsRefused()
    {
        var openLibrary = new FakeOpenLibraryClient(request =>
            throw new OpenLibraryQueryRejectedException([request.Query == "of" ? "Query too short" : "Not allowed: the"]));

        var refusal = await Assert.ThrowsAsync<OpenLibraryQueryRejectedException>(() =>
            CreateFinder(openLibrary).FindAsync([ReadingOf(keywords: "of"), ReadingOf(keywords: "the")], "of the"));

        Assert.Equal(["Query too short", "Not allowed: the"], refusal.Reasons);
    }

    [Fact]
    public async Task KeepsTheWorksFromTheSearchesThatWorkedWhenOneHitsAnOutage()
    {
        var dune = Work("OL893414W", "Dune", ["Frank Herbert"]);
        var openLibrary = new FakeOpenLibraryClient(request =>
            request.Author is not null ? throw new OpenLibraryUnavailableException("Open Library returned 503.") : [dune]);

        var works = await CreateFinder(openLibrary).FindAsync([ReadingOf("dune", "frank herbert"), ReadingOf("dune")], "dune herbert");

        Assert.Equal("OL893414W", Assert.Single(works).WorkId);
    }

    [Fact]
    public async Task ReportsTheOutageWhenEverySearchHitsOne()
    {
        var openLibrary = new FakeOpenLibraryClient(_ => throw new OpenLibraryUnavailableException("Open Library returned 503."));

        await Assert.ThrowsAsync<OpenLibraryUnavailableException>(() =>
            CreateFinder(openLibrary).FindAsync([ReadingOf("dune", "frank herbert"), ReadingOf("dune")], "dune herbert"));
    }

    [Fact]
    public async Task ReportsAnOutageRatherThanARefusalWhenNoSearchWorked()
    {
        var openLibrary = new FakeOpenLibraryClient(request => request.Query == "of"
            ? throw new OpenLibraryQueryRejectedException(["Query too short"])
            : throw new OpenLibraryUnavailableException("Open Library returned 503."));

        await Assert.ThrowsAsync<OpenLibraryUnavailableException>(() =>
            CreateFinder(openLibrary).FindAsync([ReadingOf(keywords: "of"), ReadingOf("dune")], "dune of"));
    }

    [Fact]
    public async Task RunsAtMostThreeSearchesAtOnce()
    {
        var openLibrary = new SlowOpenLibraryClient(_ => TimeSpan.FromMilliseconds(50));
        var readings = Enumerable.Range(1, 9).Select(index => ReadingOf($"book {index}")).ToList();

        await CreateFinder(openLibrary).FindAsync(readings, "books");

        Assert.Equal(9, openLibrary.Searched);
        Assert.Equal(OpenLibraryCandidateFinder.MaxConcurrentSearches, openLibrary.MostAtOnce);
    }

    [Fact]
    public async Task KeepsTheReadingOrderWhenSearchesFinishOutOfOrder()
    {
        // The first reading's search is the slowest, so it finishes last.
        var openLibrary = new SlowOpenLibraryClient(request => request.Title == "first" ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero);

        var works = await CreateFinder(openLibrary).FindAsync([ReadingOf("first"), ReadingOf("second"), ReadingOf("third")], "books");

        Assert.Equal(["first", "second", "third"], works.Select(work => work.Title));
    }

    [Fact]
    public async Task LetsTheCallersCancellationThrough()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateFinder(FakeOpenLibraryClient.Finding()).FindAsync([ReadingOf("dune")], "dune", cancelled.Token));
    }

    [Fact]
    public async Task AsksForMoreWorksThanAreReturnedSoRankingHasCandidates()
    {
        var openLibrary = FakeOpenLibraryClient.Finding();

        await CreateFinder(openLibrary).FindAsync([ReadingOf("dune")], "dune");

        Assert.Equal(10, Assert.Single(openLibrary.Searches).Limit);
    }

    [Theory]
    [InlineData("contributor")]
    [InlineData("author_key")]
    [InlineData("edition_count")]
    [InlineData("first_publish_year")]
    [InlineData("cover_i")]
    public void RequestsTheFieldsTheLaterStagesNeed(string field) => Assert.Contains(field, WorkSearchRequest.DefaultFields);

    /// <summary>
    /// Answers each search after a delay with one work titled like the search, and records how many searches
    /// were running at the same time.
    /// </summary>
    private class SlowOpenLibraryClient(Func<WorkSearchRequest, TimeSpan> delay) : IOpenLibraryClient
    {
        private int _running;
        private int _mostAtOnce;
        private int _searched;

        public int MostAtOnce => _mostAtOnce;

        public int Searched => _searched;

        public async Task<WorkSearchResponse> SearchWorksAsync(WorkSearchRequest request, CancellationToken cancellationToken = default)
        {
            var running = Interlocked.Increment(ref _running);
            InterlockedMax(ref _mostAtOnce, running);
            try
            {
                await Task.Delay(delay(request), cancellationToken);
                Interlocked.Increment(ref _searched);
                return new WorkSearchResponse { Docs = [Work($"OL-{request.Title}-W", request.Title!, ["Someone"])] };
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while (value > (current = target) && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }

        public Task<WorkRecord?> GetWorkAsync(string workId, CancellationToken cancellationToken = default) => Task.FromResult<WorkRecord?>(null);

        public Task<AuthorRecord?> GetAuthorAsync(string authorId, CancellationToken cancellationToken = default) => Task.FromResult<AuthorRecord?>(null);

        public Task<AuthorWorksResponse?> GetAuthorWorksAsync(string authorId, int? limit = null, int? offset = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthorWorksResponse?>(null);
    }
}
