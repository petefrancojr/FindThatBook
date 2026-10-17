using FindThatBook.Api.Features.Books.Search.Text;

namespace FindThatBook.Api.Tests.Features.Books.Search.Text;

/// <summary>Requirement 3b: normalize inputs (lowercase, punctuation, diacritics).</summary>
public class StringExtensionsTests
{
    [Theory]
    [InlineData("The Hobbit", "the hobbit")]
    [InlineData("TOLKIEN", "tolkien")]
    public void Lowercases(string text, string expected) => Assert.Equal(expected, text.ToNormalized());

    [Theory]
    [InlineData("Brontë", "bronte")]
    [InlineData("Gabriel García Márquez", "gabriel garcia marquez")]
    [InlineData("Søren Kierkegaard", "soren kierkegaard")]
    [InlineData("Die Straße", "die strasse")]
    [InlineData("Łódź", "lodz")]
    public void FoldsLatinDiacriticsAndLetters(string text, string expected) => Assert.Equal(expected, text.ToNormalized());

    [Fact]
    public void KeepsMarksThatMakeADifferentLetterInOtherScripts() =>
        Assert.Equal("война и мир", "Война и мир".ToNormalized());

    [Theory]
    [InlineData("spider-man", "spider man")]
    [InlineData("Dune: Messiah", "dune messiah")]
    [InlineData("Mr. Darcy", "mr darcy")]
    [InlineData("!!!", "")]
    public void TurnsPunctuationIntoSpaces(string text, string expected) => Assert.Equal(expected, text.ToNormalized());

    [Theory]
    [InlineData("J.R.R. Tolkien", "j r r tolkien")]
    [InlineData("J. R. R. Tolkien", "j r r tolkien")]
    [InlineData("Ursula K. Le Guin", "ursula k le guin")]
    public void KeepsInitialsApart(string text, string expected) => Assert.Equal(expected, text.ToNormalized());

    [Theory]
    [InlineData("Tim O'Brien", "tim o'brien")]
    [InlineData("Flannery O’Connor", "flannery o'connor")]
    [InlineData("Harry Potter and the Philosopher’s Stone", "harry potter and the philosopher's stone")]
    public void KeepsApostrophesInsideWordsAsAPlainApostrophe(string text, string expected) => Assert.Equal(expected, text.ToNormalized());

    [Theory]
    [InlineData("'Dune'", "dune")]
    [InlineData("the authors' guide", "the authors guide")]
    [InlineData("'", "")]
    public void DropsApostrophesAtTheEdgesOfWords(string text, string expected) => Assert.Equal(expected, text.ToNormalized());

    [Fact]
    public void DropsAnAmpersand() => Assert.Equal("pride prejudice", "Pride & Prejudice".ToNormalized());

    [Theory]
    [InlineData("  ＴＯＬＫＩＥＮ​ ", "tolkien")]
    [InlineData("ﬁnding", "finding")]
    [InlineData("the\thobbit\n deluxe edition", "the hobbit deluxe edition")]
    public void RemovesInvisibleCharactersAndCollapsesWhitespace(string text, string expected) => Assert.Equal(expected, text.ToNormalized());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReturnsEmptyForNoText(string? text) => Assert.Equal("", text.ToNormalized());

    [Theory]
    [InlineData("J.R.R. Tolkien — The Hobbit!")]
    [InlineData("Flannery O’Connor")]
    [InlineData("Gabriel García Márquez")]
    public void ChangesNothingTheSecondTime(string text) => Assert.Equal(text.ToNormalized(), text.ToNormalized().ToNormalized());
}
