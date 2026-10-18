using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Mapping;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Mapping;

/// <summary>
/// "Return results": title, primary author(s), first publish year, Open Library IDs/links, cover image if available,
/// and an explanation.
/// </summary>
public class BookResultMapperTests
{
    private static BookCandidate Ranked(BookCandidate candidate, string tier, string? explanation) => new()
    {
        Work = candidate.Work,
        Authors = candidate.Authors,
        Tier = tier,
        Explanation = explanation,
    };

    private static readonly BookCandidate DixonsHobbit = Ranked(
        Candidate(Work("OL219602W", "The Hobbit", ["Charles Dixon", "J.R.R. Tolkien"], year: 1990, coverId: 8406766),
            Contributor("Charles Dixon", "Adapter"), Primary("J.R.R. Tolkien")),
        "B",
        "Exact title; Tolkien is primary author; Dixon listed as adapter.");

    [Fact]
    public void ReturnsTheBookDetailsTierAndExplanation()
    {
        var result = BookResultMapper.ToBookResult(DixonsHobbit)!;

        Assert.Equal("The Hobbit", result.Title);
        Assert.Equal(1990, result.FirstPublishYear);
        Assert.Equal("OL219602W", result.OpenLibraryId);
        Assert.Equal("B", result.Tier);
        Assert.Equal("Exact title; Tolkien is primary author; Dixon listed as adapter.", result.Explanation);
    }

    [Fact]
    public void NamesThePrimaryAuthorNotTheContributors() => Assert.Equal("J.R.R. Tolkien", BookResultMapper.ToBookResult(DixonsHobbit)!.Author);

    [Fact]
    public void NamesEveryListedAuthorWhenNoneIsKnownToBePrimary()
    {
        var goodOmens = Ranked(Candidate(Work("OL453936W", "Good Omens", ["Terry Pratchett", "Neil Gaiman"]), Listed("Terry Pratchett"), Listed("Neil Gaiman")), "A", null);

        Assert.Equal("Terry Pratchett, Neil Gaiman", BookResultMapper.ToBookResult(goodOmens)!.Author);
    }

    [Fact]
    public void ListsThePrimaryAuthorsAndTheContributorsWithTheirRoles()
    {
        var result = BookResultMapper.ToBookResult(DixonsHobbit)!;

        Assert.Equal(["J.R.R. Tolkien"], result.Authors);
        var contributor = Assert.Single(result.Contributors);
        Assert.Equal("Charles Dixon", contributor.Name);
        Assert.Equal("Adapter", contributor.Role);
    }

    [Fact]
    public void ListsEveryCoAuthorSeparately()
    {
        var goodOmens = Ranked(Candidate(Work("OL453936W", "Good Omens", ["Terry Pratchett", "Neil Gaiman"]), Listed("Terry Pratchett"), Listed("Neil Gaiman")), "A", null);

        var result = BookResultMapper.ToBookResult(goodOmens)!;

        Assert.Equal(["Terry Pratchett", "Neil Gaiman"], result.Authors);
        Assert.Empty(result.Contributors);
    }

    [Fact]
    public void LeavesAContributorsRoleEmptyWhenNoneWasRecorded()
    {
        var hobbit = Ranked(Candidate(Work("OL219602W", "The Hobbit", ["Charles Dixon", "J.R.R. Tolkien"]),
            (Name: "Charles Dixon", Credit: AuthorCredit.Contributor, Role: (string?)null), Primary("J.R.R. Tolkien")), "B", null);

        Assert.Null(Assert.Single(BookResultMapper.ToBookResult(hobbit)!.Contributors).Role);
    }

    [Fact]
    public void LinksAWorkToItsOpenLibraryPage() =>
        Assert.Equal("https://openlibrary.org/works/OL219602W", BookResultMapper.ToBookResult(DixonsHobbit)!.Url);

    [Fact]
    public void LinksAnEditionWithoutAWorkToItsBookPage()
    {
        var edition = Ranked(Candidate(Work("OL8866470M", "Harry Potter Activities Plus", ["Delta Education"]), Primary("Delta Education")), "E", null);

        Assert.Equal("https://openlibrary.org/books/OL8866470M", BookResultMapper.ToBookResult(edition)!.Url);
    }

    [Fact]
    public void BuildsTheCoverUrlFromTheCoverId() =>
        Assert.Equal("https://covers.openlibrary.org/b/id/8406766-M.jpg", BookResultMapper.ToBookResult(DixonsHobbit)!.CoverUrl);

    [Theory]
    [InlineData(null)]
    [InlineData(-1L)]
    public void LeavesTheCoverOutWhenThereIsNoImage(long? coverId)
    {
        var noCover = Ranked(Candidate(Work("OL1W", "Untitled Notes", ["Someone"], coverId: coverId), Primary("Someone")), "E", null);

        Assert.Null(BookResultMapper.ToBookResult(noCover)!.CoverUrl);
    }

    [Fact]
    public void FallsBackToAnExplanationBuiltFromTheFetchedFields()
    {
        var unexplained = Ranked(Candidate(Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien"], year: 1937), Primary("J.R.R. Tolkien")), "A", null);

        Assert.Equal("Open Library lists \"The Hobbit\" by J.R.R. Tolkien, first published in 1937.", BookResultMapper.ToBookResult(unexplained)!.Explanation);
    }

    [Fact]
    public void UsesTierEForACandidateThatWasNotRanked()
    {
        var unranked = Candidate(Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien"]), Primary("J.R.R. Tolkien"));

        Assert.Equal("E", BookResultMapper.ToBookResult(unranked)!.Tier);
    }

    [Fact]
    public void SkipsAWorkWithoutATitle()
    {
        var untitled = Candidate(Work("OL1W", "", ["Someone"]), Primary("Someone"));

        Assert.Null(BookResultMapper.ToBookResult(untitled));
    }
}
