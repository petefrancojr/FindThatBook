namespace FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;

/// <summary>How the author in the most likely reading is credited on a work.</summary>
public enum AuthorMatch
{
    /// <summary>The reading has no author.</summary>
    NotGiven,

    /// <summary>A primary author of the work.</summary>
    Primary,

    /// <summary>Listed as an author, with nothing showing whether they wrote it or contributed.</summary>
    Listed,

    /// <summary>Only a contributor, for example the adapter.</summary>
    Contributor,

    /// <summary>Not on the work.</summary>
    None,
}
