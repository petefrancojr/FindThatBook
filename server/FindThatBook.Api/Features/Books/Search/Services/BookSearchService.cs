using FindThatBook.Api.Features.Books.Search.DTOs;
using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>
/// Runs a book search from start to end: reads the search into readings, finds candidate works for them,
/// works out who wrote each one, merges works that are the same book, ranks them against the most likely reading,
/// has Gemini explain the ones shown, and maps them to the books the client receives.
/// </summary>
public class BookSearchService(IQueryReader queryReader, ICandidateFinder candidateFinder, ICandidateExplainer candidateExplainer, ILogger<BookSearchService> logger) : IBookSearchService
{
    /// <summary>The most distinct books the client receives for one search.</summary>
    private const int MaxResults = 10;

    public async Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.ToNormalized();

        // A search such as "!!!" passes validation but has no letters or digits, so there is nothing to look up.
        if (normalizedQuery.Length == 0)
        {
            logger.LogInformation("Search {Query} is empty once normalized, so Gemini and Open Library are skipped.", query);
            return new BookSearchResponse { Query = query, Results = [] };
        }

        var readings = await queryReader.ReadAsync(query, cancellationToken);

        var openLibraryResponse = await candidateFinder.FindAsync(readings, normalizedQuery, cancellationToken);

        logger.LogInformation("\nTotal Unique Result Count: {total}\n", openLibraryResponse.Count);

        var candidates = PrimaryAuthorResolver.Resolve(openLibraryResponse);

        foreach (var candidate in candidates.Where(candidate => candidate.Authors.Count > 1))
        {
            logger.LogDebug(
                "Authors of {WorkId} \"{Title}\": {Authors}",
                candidate.Work.WorkId,
                candidate.Work.Title,
                string.Join("; ", candidate.Authors.Select(author => $"{author.Name} = {author.Credit} ({author.Reason})")));
        }

        var canonicalWorks = WorkConsolidator.Consolidate(candidates);

        logger.LogInformation("\nCanonical Work Count: {total}\n", canonicalWorks.Count);

        // The first reading is the most likely one, so the works are ranked against it.
        var mostLikelyReading = readings.FirstOrDefault();
        var rankedWorks = CandidateRanker.Rank(mostLikelyReading, canonicalWorks);

        // Tiers run "A" to "E", so ordinal order is best first. OrderBy is stable, so works keep their order within a tier.
        var sortedRankedWorks = rankedWorks.OrderBy(work => work.Tier, StringComparer.Ordinal);

        // Only the works that will be shown are explained. A work without a key or title can't be shown.
        var shownWorks = sortedRankedWorks
            .Where(work => work.Work.WorkId is not null && !string.IsNullOrWhiteSpace(work.Work.Title))
            .Take(MaxResults)
            .ToList();
        var explainedWorks = await candidateExplainer.ExplainAsync(query, mostLikelyReading, shownWorks, cancellationToken);

        var results = explainedWorks
            .Select(BookResultMapper.ToBookResult)
            .OfType<BookResult>()
            .ToList();

        return new BookSearchResponse { Query = query, Results = results };
    }
}
