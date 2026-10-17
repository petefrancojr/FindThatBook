using FindThatBook.Api.Features.Books.Search.Pipeline.Authors;
using FindThatBook.Api.Features.Books.Search.Pipeline.Consolidation;
using FindThatBook.Api.Integrations.OpenLibrary.Models;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Consolidation;

/// <summary>
/// Requirement 3b.ii: de-duplicate to canonical works. Requirement 3b.i: handle subtitle variants of a title.
/// </summary>
public class WorkConsolidatorTests
{
    private static IReadOnlyList<string?> Consolidate(params WorkSearchDoc[] works) =>
        WorkConsolidator.Consolidate(PrimaryAuthorResolver.Resolve(works)).Select(candidate => candidate.Work.WorkId).ToList();

    [Fact]
    public void KeepsTheWorkWithTheMostEditionsWhenTitleAndAuthorsMatch()
    {
        var kept = Consolidate(
            Work("OL15165350W", "Pride and Prejudice", ["Jane Austen|OL21594A"], editions: 132),
            Work("OL66554W", "Pride and Prejudice", ["Jane Austen|OL21594A"], editions: 4042));

        Assert.Equal(["OL66554W"], kept);
    }

    [Theory]
    [InlineData("Frankenstein; or, The Modern Prometheus", "Frankenstein")]
    [InlineData("The Hobbit: or There and Back Again", "The Hobbit")]
    [InlineData("Pride and Prejudice [1/2]", "Pride and Prejudice")]
    [InlineData("PRIDE AND PREJUDICE", "Pride and Prejudice")]
    public void TreatsSubtitleNoteAndCaseVariantsAsTheSameBook(string longTitle, string shortTitle)
    {
        var kept = Consolidate(
            Work("OL1W", longTitle, ["Author|OL1A"], editions: 50),
            Work("OL2W", shortTitle, ["Author|OL1A"], editions: 5));

        Assert.Equal(["OL1W"], kept);
    }

    [Fact]
    public void KeepsAnAdaptationWithOtherAuthorsSeparate()
    {
        var kept = Consolidate(
            Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien|OL26320A"], editions: 482),
            Work("OL219602W", "The Hobbit", ["Charles Dixon|OL2453782A", "J.R.R. Tolkien|OL26320A"], editions: 10));

        Assert.Equal(["OL27482W", "OL219602W"], kept);
    }

    [Fact]
    public void KeepsCollectionsWithDifferentContentsSeparate()
    {
        var kept = Consolidate(
            Work("OL66597W", "Novels (Pride and Prejudice / Sense and Sensibility)", ["Jane Austen|OL21594A"]),
            Work("OL66518W", "Novels (Emma / Pride and Prejudice / Sense and Sensibility)", ["Jane Austen|OL21594A"]));

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void KeepsBooksInTheOrderTheyWereFirstFound()
    {
        var kept = Consolidate(
            Work("OL893414W", "Dune", ["Frank Herbert|OL79034A"], editions: 300),
            Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien|OL26320A"], editions: 482),
            Work("OL2W", "Dune", ["Frank Herbert|OL79034A"], editions: 3));

        Assert.Equal(["OL893414W", "OL27482W"], kept);
    }
}
