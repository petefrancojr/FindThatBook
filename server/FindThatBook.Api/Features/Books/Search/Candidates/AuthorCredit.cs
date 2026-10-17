namespace FindThatBook.Api.Features.Books.Search.Candidates;

/// <summary>How a person listed on a work is credited, once the work's authors are resolved.</summary>
public enum AuthorCredit
{
    /// <summary>Wrote the book.</summary>
    Primary,

    /// <summary>Listed as an author, but nothing shows whether they wrote the book or contributed to it.</summary>
    Listed,

    /// <summary>Contributed without writing the book, for example as an adapter or illustrator.</summary>
    Contributor,
}
