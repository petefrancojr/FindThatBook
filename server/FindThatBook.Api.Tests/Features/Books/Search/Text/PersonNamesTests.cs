using FindThatBook.Api.Features.Books.Search.Text;

namespace FindThatBook.Api.Tests.Features.Books.Search.Text;

/// <summary>Requirement 1c: partial names, such as a surname on its own, still find the person.</summary>
public class PersonNamesTests
{
    [Theory]
    [InlineData("tolkien", "J.R.R. Tolkien")]
    [InlineData("j r r tolkien", "J.R.R. Tolkien")]
    [InlineData("charles dixon", "Dixon, Charles.")]
    [InlineData("Gabriel Garcia Marquez", "Gabriel García Márquez")]
    public void MatchesTheSamePersonWrittenDifferently(string first, string second) => Assert.True(PersonNames.Match(first, second));

    [Theory]
    [InlineData("tolkien", "Charles Dixon")]
    [InlineData("jane austen", "Jane Smith")]
    [InlineData("", "Jane Austen")]
    public void DoesNotMatchDifferentPeople(string first, string second) => Assert.False(PersonNames.Match(first, second));

    [Fact]
    public void PutsASurnameFirstNameInOrderAndDropsDates() =>
        Assert.Equal("david wenzel", PersonNames.Key("Wenzel, David, 1950-"));
}
