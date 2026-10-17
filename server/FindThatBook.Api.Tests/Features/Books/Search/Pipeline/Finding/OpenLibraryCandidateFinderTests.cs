using FindThatBook.Api.Features.Books.Search.Pipeline.Finding;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Finding;

/// <summary>Requirement 3a: search Open Library's /search.json for candidates, one search per reading.</summary>
public class OpenLibraryCandidateFinderTests
{
    private static OpenLibraryCandidateFinder CreateFinder(FakeOpenLibraryClient openLibrary) =>
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
}
