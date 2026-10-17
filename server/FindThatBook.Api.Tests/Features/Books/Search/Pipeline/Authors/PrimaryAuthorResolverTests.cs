using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Authors;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Pipeline.Authors;

/// <summary>
/// Requirement 3b.ii and the data quality note: resolve primary authors, and treat contributors (illustrators,
/// editors, adaptors) listed in author_name as contributors.
/// </summary>
public class PrimaryAuthorResolverTests
{
    private static AuthorCredit CreditOf(IReadOnlyList<BookCandidate> candidates, string workId, string name) =>
        candidates.Single(candidate => candidate.Work.WorkId == workId).Authors.Single(author => author.Name == name).Credit;

    [Fact]
    public void MarksAnAdapterWithARoleAsAContributorAndTheAuthorAsPrimary()
    {
        // Dixon's graphic novel of The Hobbit, as Open Library returns it.
        var graphicNovel = Work("OL219602W", "The Hobbit", ["Charles Dixon|OL2453782A", "Sean Deming|OL758306A", "J.R.R. Tolkien|OL26320A"],
            editions: 10, contributors: ["Charles Dixon (Adapter)", "Sean Deming (Adapter)", "Tolkien, J. R. R. 1892-1973."]);

        var candidate = Assert.Single(PrimaryAuthorResolver.Resolve([graphicNovel]));

        Assert.Equal(["J.R.R. Tolkien"], candidate.PrimaryAuthors.Select(author => author.Name));
        Assert.Equal(["Charles Dixon", "Sean Deming"], candidate.Contributors.Select(author => author.Name));
        Assert.Equal("Adapter", candidate.Contributors.First().Role);
    }

    [Fact]
    public void MarksTheOnlyAuthorOfTheBiggerSameTitleWorkAsPrimaryWithoutAnyRoles()
    {
        var original = Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien|OL26320A"], editions: 482);
        var graphicNovel = Work("OL219602W", "The Hobbit", ["Charles Dixon|OL2453782A", "J.R.R. Tolkien|OL26320A"], editions: 10);

        var candidates = PrimaryAuthorResolver.Resolve([original, graphicNovel]);

        Assert.Equal(AuthorCredit.Primary, CreditOf(candidates, "OL219602W", "J.R.R. Tolkien"));
        Assert.Equal(AuthorCredit.Contributor, CreditOf(candidates, "OL219602W", "Charles Dixon"));
    }

    [Fact]
    public void DoesNotLetASmallStrayRecordDemoteACoAuthor()
    {
        var goodOmens = Work("OL453936W", "Good Omens", ["Terry Pratchett|OL25712A", "Neil Gaiman|OL53305A"], editions: 73);
        var strayRecord = Work("OL1W", "Good Omens", ["Neil Gaiman|OL53305A"], editions: 2);

        var candidates = PrimaryAuthorResolver.Resolve([goodOmens, strayRecord]);

        Assert.Equal(AuthorCredit.Listed, CreditOf(candidates, "OL453936W", "Terry Pratchett"));
        Assert.Equal(AuthorCredit.Listed, CreditOf(candidates, "OL453936W", "Neil Gaiman"));
    }

    [Theory]
    [InlineData("Narrator")]
    [InlineData("Editor")]
    public void IgnoresRolesThatCoAuthorsAlsoCarry(string role)
    {
        var freakonomics = Work("OL278022W", "Freakonomics", ["Steven D. Levitt", "Stephen J. Dubner"], contributors: [$"Stephen J. Dubner ({role})"]);

        var candidate = Assert.Single(PrimaryAuthorResolver.Resolve([freakonomics]));

        Assert.All(candidate.Authors, author => Assert.Equal(AuthorCredit.Listed, author.Credit));
    }

    [Fact]
    public void MakesASingleListedAuthorPrimary()
    {
        var dune = Work("OL893414W", "Dune", ["Frank Herbert"]);

        Assert.Equal(AuthorCredit.Primary, Assert.Single(Assert.Single(PrimaryAuthorResolver.Resolve([dune])).Authors).Credit);
    }

    [Fact]
    public void KeepsAnAuthorWhoIllustratedTheirOwnBookPrimary()
    {
        var hobbit = Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien"], contributors: ["J.R.R. Tolkien (Illustrator)"]);

        Assert.Equal(AuthorCredit.Primary, Assert.Single(Assert.Single(PrimaryAuthorResolver.Resolve([hobbit])).Authors).Credit);
    }

    [Fact]
    public void CountsANameListedTwiceOnce()
    {
        var salemsLot = Work("OL81632W", "'Salem's Lot", ["Stephen King|OL19981A", "Stephen King|OL2162284A"]);

        var author = Assert.Single(Assert.Single(PrimaryAuthorResolver.Resolve([salemsLot])).Authors);

        Assert.Equal(AuthorCredit.Primary, author.Credit);
    }

    [Fact]
    public void DoesNotGuessWhenMoreThanOnePersonIsLeft()
    {
        var taoTeChing = Work("OL45499W", "Tao te Ching", ["Laozi", "Yang Peng", "James Legge"], contributors: ["James Legge (Translator)"]);

        var candidate = Assert.Single(PrimaryAuthorResolver.Resolve([taoTeChing]));

        Assert.Equal(AuthorCredit.Contributor, candidate.Authors.Single(author => author.Name == "James Legge").Credit);
        Assert.Empty(candidate.PrimaryAuthors);
    }

    [Fact]
    public void MakesTheOnePersonLeftPrimaryWhenEveryoneElseIsAContributor()
    {
        var alice = Work("OL138052W", "Alice's Adventures in Wonderland", ["Lewis Carroll", "John Tenniel"], contributors: ["John Tenniel (Illustrator)"]);

        var candidate = Assert.Single(PrimaryAuthorResolver.Resolve([alice]));

        Assert.Equal(["Lewis Carroll"], candidate.PrimaryAuthors.Select(author => author.Name));
    }

    [Fact]
    public void ReadsASurnameFirstContributorEntry()
    {
        var hobbit = Work("OL219602W", "The Hobbit", ["Charles Dixon", "J.R.R. Tolkien"], contributors: ["Dixon, Charles (Adapter)"]);

        var candidate = Assert.Single(PrimaryAuthorResolver.Resolve([hobbit]));

        Assert.Equal("Charles Dixon", Assert.Single(candidate.Contributors).Name);
    }
}
