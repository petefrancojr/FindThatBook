using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>
/// Merges works that are the same book into one canonical work. Open Library often has more than one work
/// record for a book, for example two "Pride and Prejudice" works by Jane Austen.
/// </summary>
public static class WorkConsolidator
{
    /// <summary>
    /// Characters that start a subtitle ("The Hobbit: or There and Back Again") or one of Open Library's notes
    /// after the main title ("Pride and Prejudice [1/2]"). Round brackets are not cut, because in a collection they
    /// hold the real titles: "Novels (Emma / Pride and Prejudice)" and "Novels (Persuasion)" are different books.
    /// </summary>
    private static readonly char[] SubtitleMarks = [':', ';', '['];

    /// <summary>
    /// Keeps one work per book, in the order the books were first found. Two works are the same book when they
    /// have the same main title and the same set of authors, so an adaptation with the same title but other
    /// authors (Dixon's graphic novel of The Hobbit) stays a separate result. Of the works for one book, the one
    /// with the most editions is kept, because it is the record Open Library attaches most editions to.
    /// The candidates arrive with their authors already resolved, and each one keeps its own.
    /// </summary>
    public static IReadOnlyList<BookCandidate> Consolidate(IReadOnlyList<BookCandidate> candidates) =>
        candidates
            .GroupBy(candidate => BookKey(candidate.Work))
            .Select(book => book.MaxBy(candidate => candidate.Work.EditionCount ?? 0)!)
            .ToList();

    /// <summary>
    /// The main title and the authors, for example "pride and prejudice|OL21594A". A work without a title can't
    /// be matched with any other, so its own key keeps it apart.
    /// </summary>
    private static string BookKey(WorkSearchDoc work)
    {
        var mainTitle = MainTitle(work.Title);
        if (mainTitle.Length == 0)
        {
            return $"key:{work.Key}";
        }

        var authors = string.Join(',', work.AuthorKeys.Distinct().Order(StringComparer.Ordinal));
        return $"{mainTitle}|{authors}";
    }

    /// <summary>
    /// The title up to its first subtitle mark, normalized. This runs on the title as Open Library wrote it,
    /// because normalizing first would turn the marks into spaces. A title that starts with a mark is used whole.
    /// </summary>
    public static string MainTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var end = title.IndexOfAny(SubtitleMarks);
        var main = end > 0 ? title[..end].ToNormalized() : string.Empty;
        return main.Length > 0 ? main : title.ToNormalized();
    }
}
