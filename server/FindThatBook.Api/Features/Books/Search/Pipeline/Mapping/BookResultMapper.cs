using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.DTOs;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Pipeline.Mapping;

/// <summary>Turns book candidates into the <see cref="BookResult"/>s the API returns.</summary>
public static class BookResultMapper
{
    private const string OpenLibrarySite = "https://openlibrary.org";
    private const string CoversSite = "https://covers.openlibrary.org";

    /// <summary>
    /// The brief's lowest level, "no clear winner", for a candidate that couldn't be ranked.
    /// </summary>
    public const string UnrankedTier = "E";

    /// <summary>
    /// Maps one candidate. Returns null when its work has no key or no title, since a result
    /// can't be shown or linked without them.
    /// </summary>
    public static BookResult? ToBookResult(BookCandidate candidate)
    {
        var doc = candidate.Work;
        if (doc.WorkId is null || string.IsNullOrWhiteSpace(doc.Title))
        {
            return null;
        }

        var authors = ToAuthors(candidate);
        var author = authors.Count > 0 ? string.Join(", ", authors) : "Unknown author";

        return new BookResult
        {
            Title = doc.Title,
            Author = author,
            Authors = authors,
            Contributors = ToContributors(candidate, authors),
            FirstPublishYear = doc.FirstPublishYear,
            OpenLibraryId = doc.WorkId,
            Url = ToUrl(doc),
            CoverUrl = ToCoverUrl(doc.CoverId),
            Tier = candidate.Tier ?? UnrankedTier,
            Explanation = candidate.Explanation ?? ToExplanation(doc.Title, author, doc.FirstPublishYear),
        };
    }

    /// <summary>
    /// The primary authors. When nothing shows who wrote the book, everyone listed as an author, since guessing
    /// could name an illustrator as the writer. Contributors are only used when no one else is listed.
    /// </summary>
    private static IReadOnlyList<string> ToAuthors(BookCandidate candidate)
    {
        var primary = candidate.PrimaryAuthors.Select(author => author.Name).ToList();
        if (primary.Count > 0)
        {
            return primary;
        }

        var listed = candidate.Authors.Where(author => author.Credit == AuthorCredit.Listed).Select(author => author.Name).ToList();
        if (listed.Count > 0)
        {
            return listed;
        }

        return candidate.Authors.Take(1).Select(author => author.Name).ToList();
    }

    /// <summary>The contributors, with their roles, leaving out anyone already named as an author.</summary>
    private static IReadOnlyList<BookContributor> ToContributors(BookCandidate candidate, IReadOnlyList<string> authors) =>
        candidate.Contributors
            .Where(contributor => !authors.Contains(contributor.Name))
            .Select(contributor => new BookContributor { Name = contributor.Name, Role = contributor.Role })
            .ToList();

    /// <summary>
    /// An edition that was never attached to a work is listed under its edition ID, which lives at /books/.
    /// </summary>
    private static string ToUrl(WorkSearchDoc doc) =>
        doc.IsWork ? $"{OpenLibrarySite}/works/{doc.WorkId}" : $"{OpenLibrarySite}/books/{doc.WorkId}";

    /// <summary>Open Library uses -1 for "no image", so only a positive ID makes a cover URL.</summary>
    private static string? ToCoverUrl(long? coverId) =>
        coverId > 0 ? $"{CoversSite}/b/id/{coverId}-M.jpg" : null;

    /// <summary>
    /// A plain explanation built only from fields Open Library returned, for a candidate Gemini couldn't
    /// explain, for example when its answer was blocked or not valid JSON.
    /// </summary>
    private static string ToExplanation(string title, string author, int? firstPublishYear) =>
        firstPublishYear is null
            ? $"Open Library lists \"{title}\" by {author}."
            : $"Open Library lists \"{title}\" by {author}, first published in {firstPublishYear}.";
}
