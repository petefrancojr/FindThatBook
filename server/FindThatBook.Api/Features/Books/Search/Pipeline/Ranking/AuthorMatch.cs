namespace FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;

/// <summary>How closely the author in the most likely reading matches a person on a work.</summary>
public enum AuthorMatch
{
    /// <summary>The reading has no author.</summary>
    NotGiven,

    /// <summary>The same name once normalized: "j r r tolkien" and "J.R.R. Tolkien".</summary>
    Exact,

    /// <summary>Part of the name, such as a surname alone ("tolkien"), or a small misspelling ("tolkein").</summary>
    Loose,

    /// <summary>A different person.</summary>
    None,
}
