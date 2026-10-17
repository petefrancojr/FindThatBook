using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Helpers;

/// <summary>Turns Open Library search results into the <see cref="BookResult"/>s the API returns.</summary>
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
    /// Maps one search result. Returns null when it has no key or no title, since a result
    /// can't be shown or linked without them.
    /// </summary>
    public static BookResult? ToBookResult(WorkSearchDoc doc)
    {
        if (doc.WorkId is null || string.IsNullOrWhiteSpace(doc.Title))
        {
            return null;
        }

        // Until primary authors are resolved from the work record, the first credited name is used:
        // author_name can also list illustrators and translators, and the first name is usually the author.
        var author = doc.AuthorNames.FirstOrDefault() ?? "Unknown author";

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
