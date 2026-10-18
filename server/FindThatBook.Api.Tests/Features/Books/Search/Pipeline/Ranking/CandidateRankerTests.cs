using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Ranking;

/// <summary>Requirement 4: apply the matching hierarchy, A (strongest) to E (no clear winner).</summary>
public class CandidateRankerTests
{
    private static readonly BookCandidate Hobbit = Candidate(Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien"]), Primary("J.R.R. Tolkien"));

    private static readonly BookCandidate DixonsHobbit = Candidate(
        Work("OL219602W", "The Hobbit", ["Charles Dixon", "J.R.R. Tolkien"]),
        Contributor("Charles Dixon", "Adapter"),
        Primary("J.R.R. Tolkien"));

    private static string TierOf(BookCandidate candidate, params string?[] reading) =>
        Assert.Single(CandidateRanker.Rank(ReadingOf(reading[0], reading.Length > 1 ? reading[1] : null), [candidate])).Tier!;

    [Fact]
    public void ExactTitleAndPrimaryAuthorIsA() => Assert.Equal("A", TierOf(Hobbit, "the hobbit", "j r r tolkien"));

    [Fact]
    public void ExactTitleWithAContributorOnlyAuthorIsB() => Assert.Equal("B", TierOf(DixonsHobbit, "the hobbit", "charles dixon"));

    [Fact]
    public void NearTitleWithTheAuthorIsC()
    {
        var collection = Candidate(Work("OL14926019W", "The Hobbit & The Lord of the Rings [collection/set]", ["J.R.R. Tolkien"]), Primary("J.R.R. Tolkien"));

        Assert.Equal("C", TierOf(collection, "the hobbit", "tolkien"));
    }

    [Fact]
    public void AnAuthorWithNoTitleIsTheAuthorOnlyFallbackD()
    {
        var emma = Candidate(Work("OL66534W", "Emma", ["Jane Austen"]), Primary("Jane Austen"));

        Assert.Equal("D", TierOf(emma, null, "jane austen"));
    }

    [Fact]
    public void NoClearMatchIsE()
    {
        var dune = Candidate(Work("OL893414W", "Dune", ["Frank Herbert"]), Primary("Frank Herbert"));

        Assert.Equal("E", TierOf(dune, "the hobbit", "j r r tolkien"));
    }

    [Fact]
    public void ExactTitleBySomeoneElseIsE() => Assert.Equal("E", TierOf(Hobbit, "the hobbit", "charles dixon"));

    [Fact]
    public void AnUnrelatedBookByTheAuthorIsEWhenTheSearchGaveATitle()
    {
        var silmarillion = Candidate(Work("OL27495W", "The Silmarillion", ["J.R.R. Tolkien"]), Primary("J.R.R. Tolkien"));

        Assert.Equal("E", TierOf(silmarillion, "the hobbit", "j r r tolkien"));
    }

    [Fact]
    public void EveryCandidateIsEWithoutAReading() =>
        Assert.Equal("E", Assert.Single(CandidateRanker.Rank(null, [Hobbit])).Tier);

    [Fact]
    public void AnExactTitleWithNoAuthorInTheSearchIsA() => Assert.Equal("A", TierOf(Hobbit, "the hobbit"));

    [Theory]
    [InlineData("tale two cities")]
    [InlineData("a tale of two cities")]
    [InlineData("A TALE OF TWO CITIES")]
    public void IgnoresSmallWordsAndCaseInAnExactTitle(string title)
    {
        var taleOfTwoCities = Candidate(Work("OL118974W", "A Tale of Two Cities", ["Charles Dickens"]), Primary("Charles Dickens"));

        Assert.Equal("A", TierOf(taleOfTwoCities, title.ToLowerInvariant(), "charles dickens"));
    }

    [Fact]
    public void IgnoresASubtitleInAnExactTitle()
    {
        var frankenstein = Candidate(Work("OL450063W", "Frankenstein; or, The Modern Prometheus", ["Mary Shelley"]), Primary("Mary Shelley"));

        Assert.Equal("A", TierOf(frankenstein, "frankenstein", "mary shelley"));
    }

    [Fact]
    public void MatchesASearchForTheSubtitle()
    {
        var hobbit = Candidate(Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien"], subtitle: "There and Back Again"), Primary("J.R.R. Tolkien"));

        Assert.Equal("C", TierOf(hobbit, "there and back again", "j r r tolkien"));
    }

    [Fact]
    public void ToleratesASmallMisspelling() => Assert.Equal("C", TierOf(Hobbit, "the hobit", "j r r tolkien"));

    [Fact]
    public void MatchesASurnameAgainstTheFullName() => Assert.Equal("A", TierOf(Hobbit, "the hobbit", "tolkien"));

    [Fact]
    public void TreatsAPersonKeptAsAKeywordAsTheAuthor()
    {
        var ranked = CandidateRanker.Rank(ReadingOf("the hobbit", null, "dixon"), [Hobbit, DixonsHobbit]);

        Assert.Equal("B", ranked.Single(candidate => candidate.Work.WorkId == "OL219602W").Tier);
        Assert.Equal("E", ranked.Single(candidate => candidate.Work.WorkId == "OL27482W").Tier);
    }

    [Fact]
    public void DoesNotTreatACharacterNameAsTheAuthor()
    {
        var pride = Candidate(Work("OL66554W", "Pride and Prejudice", ["Jane Austen"]), Primary("Jane Austen"));
        var unrelated = Candidate(Work("OL1W", "Master Your Emotions", ["Oliver Bennet"]), Primary("Oliver Bennet"));

        var ranked = CandidateRanker.Rank(ReadingOf("pride and prejudice", null, "bennet"), [unrelated, pride]);

        Assert.Equal("A", ranked.Single(candidate => candidate.Work.WorkId == "OL66554W").Tier);
    }

    [Fact]
    public void OrdersByTierAndKeepsTheFoundOrderWithinATier()
    {
        var dune = Candidate(Work("OL893414W", "Dune", ["Frank Herbert"]), Primary("Frank Herbert"));
        var secondHobbit = Candidate(Work("OL45346371W", "The Hobbit", ["J.R.R. Tolkien"]), Primary("J.R.R. Tolkien"));

        var ranked = CandidateRanker.Rank(ReadingOf("the hobbit", "charles dixon"), [dune, Hobbit, DixonsHobbit, secondHobbit]);

        Assert.Equal(["OL219602W", "OL893414W", "OL27482W", "OL45346371W"], ranked.Select(candidate => candidate.Work.WorkId));
        Assert.Equal(["B", "E", "E", "E"], ranked.Select(candidate => candidate.Tier));
    }

    [Fact]
    public void RecordsAContributorMatchApartFromThePrimaryAuthorMatch()
    {
        var match = Assert.Single(CandidateRanker.Rank(ReadingOf("the hobbit", "charles dixon"), [DixonsHobbit])).Match!;

        Assert.Equal(TitleMatch.Exact, match.Title);
        Assert.Equal(AuthorMatch.None, match.Author);
        Assert.Null(match.MatchedAuthor);
        Assert.Equal(AuthorMatch.Exact, match.Contributor);
        Assert.Equal("Charles Dixon", match.MatchedContributor!.Name);
    }

    [Theory]
    [InlineData("j r r tolkien", AuthorMatch.Exact)]
    [InlineData("tolkien", AuthorMatch.Loose)]
    [InlineData("tolkein", AuthorMatch.Loose)]
    [InlineData("j r r tolkein", AuthorMatch.Loose)]
    [InlineData("k r r tolkien", AuthorMatch.None)]
    [InlineData("charles dixon", AuthorMatch.None)]
    public void MatchesTheReadingsAuthorAgainstThePrimaryAuthorAtThreeLevels(string author, AuthorMatch expected)
    {
        var match = Assert.Single(CandidateRanker.Rank(ReadingOf("the hobbit", author), [Hobbit])).Match!;

        Assert.Equal(expected, match.Author);
    }

    [Theory]
    [InlineData("j r r tolkien")]
    [InlineData("tolkein")]
    public void GivesTierAForAnExactOrLooseAuthorMatch(string author) => Assert.Equal("A", TierOf(Hobbit, "the hobbit", author));

    [Fact]
    public void PutsAnExactAuthorMatchBeforeALooseOneInTheSameTier()
    {
        var loose = Candidate(Work("OL1W", "The Hobbit", ["J.R.R. Tolkien"]), Primary("J.R.R. Tolkien"));
        var exact = Candidate(Work("OL2W", "The Hobbit", ["Tolkien"]), Primary("Tolkien"));

        var ranked = CandidateRanker.Rank(ReadingOf("the hobbit", "tolkien"), [loose, exact]);

        Assert.Equal(["OL2W", "OL1W"], ranked.Select(candidate => candidate.Work.WorkId));
        Assert.Equal(["A", "A"], ranked.Select(candidate => candidate.Tier));
    }

    [Fact]
    public void DoesNotCountAListedAuthorAsAPrimaryAuthor()
    {
        var goodOmens = Candidate(Work("OL453936W", "Good Omens", ["Terry Pratchett", "Neil Gaiman"]), Listed("Terry Pratchett"), Listed("Neil Gaiman"));

        var ranked = Assert.Single(CandidateRanker.Rank(ReadingOf("good omens", "neil gaiman"), [goodOmens]));

        Assert.Equal(AuthorMatch.None, ranked.Match!.Author);
        Assert.Equal("E", ranked.Tier);
    }

    [Fact]
    public void TreatsAPersonInAKeywordOnlyReadingAsTheAuthor()
    {
        var silmarillion = Candidate(Work("OL27495W", "The Silmarillion", ["J.R.R. Tolkien"]), Primary("J.R.R. Tolkien"));

        var ranked = Assert.Single(CandidateRanker.Rank(ReadingOf(null, null, "tolkien", "elves"), [silmarillion]));

        Assert.Equal(AuthorMatch.Loose, ranked.Match!.Author);
        Assert.Equal("D", ranked.Tier);
    }
}
