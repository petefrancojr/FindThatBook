using FindThatBook.Api.Features.Books.Search.DTOs;
using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>
/// Runs a book search from start to end: reads the search into readings, finds candidate works for them,
/// and maps the works to the books the client receives.
/// </summary>
public class BookSearchService(IQueryReader queryReader, ICandidateFinder candidateFinder, ILogger<BookSearchService> logger) : IBookSearchService
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

        // TODO: Consolidate works into canonicle works openLibraryResponse

        // TODO: Resolve primary authors from 'works.authors'

        // TODO: Update the Rank and Explanation for each uniqueResult. Gemeni should assist with this.

        // TODO: Sort by Rank

        var results = openLibraryResponse
            .Select(BookResultMapper.ToBookResult)
            .OfType<BookResult>()
            .Take(MaxResults)
            .ToList();

        // Use Gemeni to Rank & Explain openLibraryResponse

        return new BookSearchResponse { Query = query, Results = results };
    }
}
