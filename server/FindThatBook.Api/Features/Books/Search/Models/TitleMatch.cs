namespace FindThatBook.Api.Features.Books.Search.Models;

/// <summary>How a work's title compares with the title in the most likely reading.</summary>
public enum TitleMatch
{
    /// <summary>The reading has no title.</summary>
    NotGiven,

    /// <summary>The same once normalized, ignoring small words such as "a", "the" and "of".</summary>
    Exact,

    /// <summary>Close: missing or extra words, or a small misspelling.</summary>
    Near,

    /// <summary>A different title.</summary>
    None,
}
