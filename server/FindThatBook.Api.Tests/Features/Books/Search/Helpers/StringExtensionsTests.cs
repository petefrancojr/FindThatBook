using FindThatBook.Api.Features.Books.Search.Helpers;

namespace FindThatBook.Api.Tests.Features.Books.Search.Helpers;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("The Hobbit", "the hobbit")]
    [InlineData("J.R.R. Tolkien", "JRR Tolkien")]
    [InlineData("Brontë", "Bronte")]
    [InlineData("J. R. R. Tolkien", "JRR Tolkien")]
    [InlineData("Pride & Prejudice", "pride and prejudice")]
    [InlineData("  The   Hobbit  ", "the hobbit")]
    public void MakesTheBriefsExamplesEqual(string first, string second)
    {
        Assert.Equal(first.ToNormalized(), second.ToNormalized());
    }

    [Theory]
    [InlineData("The Hobbit", "the hobbit")]
    [InlineData("TOLKIEN", "tolkien")]
    [InlineData("İstanbul", "istanbul")]
    public void Lowercases(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("Brontë", "bronte")]
    [InlineData("Gabriel García Márquez", "gabriel garcia marquez")]
    [InlineData("Nguyễn Du", "nguyen du")]
    [InlineData("Ångström", "angstrom")]
    [InlineData("façade", "facade")]
    public void RemovesAccentsFromLatinLetters(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("Søren Kierkegaard", "soren kierkegaard")]
    [InlineData("Straße", "strasse")]
    [InlineData("Łódź", "lodz")]
    [InlineData("Æsop", "aesop")]
    [InlineData("Œuvres", "oeuvres")]
    [InlineData("Þór", "thor")]
    public void SpellsLatinLettersWithoutAnAccentToStripPlainly(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("Война и мир", "война и мир")]
    [InlineData("Ёжик", "ёжик")]
    [InlineData("Αθήνα", "αθήνα")]
    [InlineData("हिन्दी", "हिन्दी")]
    public void KeepsOtherScriptsAndTheirMarks(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("J.R.R. Tolkien", "jrr tolkien")]
    [InlineData("O'Brien", "obrien")]
    [InlineData("Philosopher’s Stone", "philosophers stone")]
    [InlineData("Philosopher‘s Stone", "philosophers stone")]
    [InlineData("Philosopherʼs Stone", "philosophers stone")]
    [InlineData("rock`n roll", "rockn roll")]
    [InlineData("2.0", "20")]
    public void DropsFullStopsAndApostrophesWithoutASpace(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("The Hobbit, or There and Back Again", "the hobbit or there and back again")]
    [InlineData("Dune: Messiah", "dune messiah")]
    [InlineData("spider-man", "spider man")]
    [InlineData("Catch‑22", "catch 22")]
    [InlineData("Tolkien — The Hobbit!", "tolkien the hobbit")]
    [InlineData("\"Emma\" (1815)", "emma 1815")]
    [InlineData("who/what?", "who what")]
    [InlineData("Dune 📚 Herbert", "dune herbert")]
    [InlineData("50% off #1 $5", "50 off 1 5")]
    public void TurnsOtherPunctuationAndSymbolsIntoSpaces(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("Pride & Prejudice", "pride and prejudice")]
    [InlineData("Pride&Prejudice", "pride and prejudice")]
    public void SpellsOutAmpersands(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("ＴＯＬＫＩＥＮ", "tolkien")]
    [InlineData("ﬁnding nemo", "finding nemo")]
    [InlineData("E=mc²", "e mc2")]
    [InlineData("Henry Ⅷ", "henry viii")]
    public void TurnsCompatibilityCharactersIntoPlainOnes(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("zero​width", "zerowidth")]
    [InlineData("soft­hyphen", "softhyphen")]
    [InlineData("﻿dune", "dune")]
    public void RemovesInvisibleCharacters(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("  the   hobbit  ", "the hobbit")]
    [InlineData("tale two\tcities\n", "tale two cities")]
    [InlineData("tale\r\ntwo cities", "tale two cities")]
    public void CollapsesAllWhitespaceToSingleSpaces(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("J. R. R. Tolkien", "jrr tolkien")]
    [InlineData("C. S. Lewis", "cs lewis")]
    [InlineData("e. e. cummings", "ee cummings")]
    [InlineData("tolkien j r r", "tolkien jrr")]
    public void JoinsSpacedInitials(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("A Tale of Two Cities", "a tale of two cities")]
    [InlineData("It", "it")]
    [InlineData("a b", "ab")]
    [InlineData("I, Robot", "i robot")]
    [InlineData("Plan 9 from Outer Space", "plan 9 from outer space")]
    public void KeepsSmallWordsAndLeavesSingleLettersAloneUnlessTheyRepeat(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData("1984", "1984")]
    [InlineData("Fahrenheit 451", "fahrenheit 451")]
    public void KeepsDigits(string text, string expected)
    {
        Assert.Equal(expected, text.ToNormalized());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("... --- ...")]
    [InlineData("​")]
    public void GivesAnEmptyStringWhenNothingIsLeft(string? text)
    {
        Assert.Equal(string.Empty, text.ToNormalized());
    }

    [Theory]
    [InlineData("  J.R.R. Tolkien — The Hobbit!")]
    [InlineData("Brontë & Søren")]
    [InlineData("Война и мир")]
    public void GivesTheSameResultWhenRunTwice(string text)
    {
        var once = text.ToNormalized();

        Assert.Equal(once, once.ToNormalized());
    }
}
