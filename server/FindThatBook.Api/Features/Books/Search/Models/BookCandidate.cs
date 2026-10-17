using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Models;

/// <summary>
/// A canonical work that could be the book the user wants, with its authors resolved.
/// Used inside <see cref="Services.BookSearchService"/>; never sent to the client.
/// </summary>
public class BookCandidate
{
    public required WorkSearchDoc Work { get; init; }

    /// <summary>Everyone listed as an author of the work, in Open Library's order, with how each is credited.</summary>
    public required IReadOnlyList<CandidateAuthor> Authors { get; init; }

    /// <summary>The tier in the brief's matching hierarchy, "A" (best) to "E". Null until the candidate is ranked.</summary>
    public string? Tier { get; init; }

    /// <summary>The facts the tier was decided from. Null until the candidate is ranked.</summary>
    public CandidateMatch? Match { get; init; }

    /// <summary>Why the candidate matched, in one sentence. Null until the candidate is explained.</summary>
    public string? Explanation { get; init; }

    public IEnumerable<CandidateAuthor> PrimaryAuthors => Authors.Where(author => author.Credit == AuthorCredit.Primary);

    public IEnumerable<CandidateAuthor> Contributors => Authors.Where(author => author.Credit == AuthorCredit.Contributor);
}
