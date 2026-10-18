using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Integrations.OpenLibrary;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Pipeline.Finding;

/// <summary>
/// Finds candidate works with Open Library's search: one search per reading, a few at a time, keeping whatever
/// the searches that worked found.
/// </summary>
public class OpenLibraryCandidateFinder(IOpenLibraryClient openLibrary, ILogger<OpenLibraryCandidateFinder> logger) : ICandidateFinder
{
    /// <summary>How many works to ask Open Library for per reading.</summary>
    private const int MaxWorksPerSearch = 10;

    /// <summary>
    /// How many searches may run against Open Library at once. Open Library is a free service that asks
    /// applications to use it considerately, so a search with many readings runs in a few rounds instead of all at once.
    /// </summary>
    public const int MaxConcurrentSearches = 3;

    /// <summary>
    /// Searches Open Library once per reading, at most <see cref="MaxConcurrentSearches"/> at a time, and returns the
    /// works in reading order (most likely reading first), each work once. When Gemini gave no readings, the
    /// normalized search is used instead.
    /// </summary>
    /// <remarks>
    /// A search that is refused or hits an outage is skipped while others found works, so one failure doesn't lose
    /// the rest. Only when no search worked does the caller hear about it: an outage wins over refusals, because
    /// trying again may help, while a refusal tells the reader to change the search.
    /// </remarks>
    public async Task<IReadOnlyList<WorkSearchDoc>> FindAsync(IReadOnlyList<QueryReading> readings, string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var requests = readings.Select(ToSearchRequest).DistinctBy(Describe).ToList();
        if (requests.Count == 0)
        {
            requests = [new WorkSearchRequest { Query = normalizedQuery, Limit = MaxWorksPerSearch }];
        }

        // Task.WhenAll keeps the order it was given, so the most likely reading's works still come first.
        using var gate = new SemaphoreSlim(MaxConcurrentSearches);
        var searches = await Task.WhenAll(requests.Select(request => SearchWhenAllowedAsync(request, gate, cancellationToken)));

        logger.LogInformation(
            "\nWorks per search: {PerSearch}\nTotal: {Total}\n",
            string.Join(", ", searches.Select(search => search.Docs.Count)),
            searches.Sum(search => search.Docs.Count));

        if (!searches.Any(search => search.Succeeded))
        {
            var outage = searches.Select(search => search.Outage).OfType<OpenLibraryUnavailableException>().FirstOrDefault();
            if (outage is not null)
            {
                throw outage;
            }

            // Open Library refused every search, so the client is told why, with all of Open Library's reasons.
            var rejections = searches.Select(search => search.Rejection).OfType<OpenLibraryQueryRejectedException>();
            throw new OpenLibraryQueryRejectedException(rejections.SelectMany(rejection => rejection.Reasons).Distinct().ToList());
        }

        return searches
            .SelectMany(search => search.Docs)
            .DistinctBy(doc => doc.Key)
            .ToList();
    }

    /// <summary>Waits for a free slot, then searches.</summary>
    private async Task<SearchOutcome> SearchWhenAllowedAsync(WorkSearchRequest request, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await SearchWorksAsync(request, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Turns a reading into an Open Library search that keeps what Gemini worked out: the title goes in
    /// Open Library's title field, the author in its author field, the keywords in q.
    /// </summary>
    private static WorkSearchRequest ToSearchRequest(QueryReading reading) => new()
    {
        Title = reading.Title,
        Author = reading.Author,
        Query = reading.Keywords.Count > 0
            ? string.Join(' ', reading.Keywords)
            : null,
        Limit = MaxWorksPerSearch,
    };

    /// <summary>
    /// Sends one search to Open Library. A search Open Library refuses (such as q=the or a q under 3 characters) or
    /// that hits an outage comes back with no works and what went wrong, so the caller can decide whether the user
    /// needs to hear about it. A cancellation by the caller is left to propagate.
    /// </summary>
    private async Task<SearchOutcome> SearchWorksAsync(WorkSearchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkSearchDoc> docs;
        try
        {
            docs = (await openLibrary.SearchWorksAsync(request, cancellationToken)).Docs;
        }
        catch (OpenLibraryQueryRejectedException ex)
        {
            logger.LogInformation("Open Library refused the search {Search}: {Reasons}", Describe(request), string.Join("; ", ex.Reasons));
            return new SearchOutcome { Rejection = ex };
        }
        catch (OpenLibraryUnavailableException ex)
        {
            logger.LogWarning(ex, "Open Library search {Search} failed, so its works are left out: {Reason}", Describe(request), ex.Message);
            return new SearchOutcome { Outage = ex };
        }

        // logger.LogInformation(
        //     "\nOpen Library search {Search} found {Count} works:\n{Works}\n",
        //     Describe(request),
        //     docs.Count,
        //     string.Join("\n", docs.Select(doc => $"  {doc.WorkId} \"{doc.Title}\" by {string.Join(", ", doc.AuthorNames)} ({doc.FirstPublishYear})")));
        return new SearchOutcome { Docs = docs };
    }

    /// <summary>What one search found, or what stopped it.</summary>
    private class SearchOutcome
    {
        public IReadOnlyList<WorkSearchDoc> Docs { get; init; } = [];

        public OpenLibraryQueryRejectedException? Rejection { get; init; }

        public OpenLibraryUnavailableException? Outage { get; init; }

        public bool Succeeded => Rejection is null && Outage is null;
    }

    /// <summary>A search's fields as they are sent, for example: title="the hobbit" author="j r r tolkien".</summary>
    private static string Describe(WorkSearchRequest request) =>
        string.Join(' ', new[]
        {
            request.Title is null ? null : $"title=\"{request.Title}\"",
            request.Author is null ? null : $"author=\"{request.Author}\"",
            request.Query is null ? null : $"q=\"{request.Query}\"",
        }.OfType<string>());
}
