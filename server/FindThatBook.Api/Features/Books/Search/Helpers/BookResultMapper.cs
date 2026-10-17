using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Helpers;

/// <summary>Turns book candidates into the <see cref="BookResult"/>s the API returns.</summary>
public static class BookResultMapper
{
    private const string OpenLibrarySite = "https://openlibrary.org";
    private const string CoversSite = "https://covers.openlibrary.org";

    /// <summary>
    /// The brief's lowest level, "no clear winner": every result gets it until ranking
    /// decides which level each one actually reaches.
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

        var author = ToAuthor(candidate);

        return new BookResult
        {
            Title = doc.Title,
            Author = author,
            FirstPublishYear = doc.FirstPublishYear,
            OpenLibraryId = doc.WorkId,
            Url = ToUrl(doc),
            CoverUrl = ToCoverUrl(doc.CoverId),
            Tier = UnrankedTier,
            Explanation = ToExplanation(doc.Title, author, doc.FirstPublishYear),
        };
    }

    /// <summary>
    /// The primary authors. When nothing shows who wrote the book, everyone listed as an author, since guessing
    /// could name an illustrator as the writer. Contributors are only used when no one else is listed.
    /// </summary>
    private static string ToAuthor(BookCandidate candidate)
    {
        var primary = candidate.PrimaryAuthors.Select(author => author.Name).ToList();
        if (primary.Count > 0)
        {
            return string.Join(", ", primary);
        }

        var listed = candidate.Authors.Where(author => author.Credit == AuthorCredit.Listed).Select(author => author.Name).ToList();
        if (listed.Count > 0)
        {
            return string.Join(", ", listed);
        }

        return candidate.Authors.FirstOrDefault()?.Name ?? "Unknown author";
    }

    /// <summary>
    /// An edition that was never attached to a work is listed under its edition ID, which lives at /books/.
    /// </summary>
    private static string ToUrl(WorkSearchDoc doc) =>
        doc.IsWork ? $"{OpenLibrarySite}/works/{doc.WorkId}" : $"{OpenLibrarySite}/books/{doc.WorkId}";

    /// <summary>Open Library uses -1 for "no image", so only a positive ID makes a cover URL.</summary>
    private static string? ToCoverUrl(long? coverId) =>
        coverId > 0 ? $"{CoversSite}/b/id/{coverId}-M.jpg" : null;

    /// <summary>
    /// A plain explanation built only from fields Open Library returned. Used until Gemini writes
    /// explanations, and kept as the fallback when it can't.
    /// </summary>
    private static string ToExplanation(string title, string author, int? firstPublishYear) =>
        firstPublishYear is null
            ? $"Open Library lists \"{title}\" by {author}."
            : $"Open Library lists \"{title}\" by {author}, first published in {firstPublishYear}.";
}
