using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.OpenLibrary;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>Finds candidate works with Open Library's search: one search per reading, run at the same time.</summary>
public class OpenLibraryCandidateFinder(IOpenLibraryClient openLibrary, ILogger<OpenLibraryCandidateFinder> logger) : ICandidateFinder
{
    /// <summary>How many works to ask Open Library for per reading.</summary>
    private const int MaxWorksPerSearch = 10;

    /// <summary>
    /// Searches Open Library once per reading, all at the same time, and returns the works in reading order
    /// (most likely reading first), each work once. When Gemini gave no readings, the normalized search is used instead.
    /// </summary>
    public async Task<IReadOnlyList<WorkSearchDoc>> FindAsync(IReadOnlyList<QueryReading> readings, string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var requests = readings.Select(ToSearchRequest).DistinctBy(Describe).ToList();
        if (requests.Count == 0)
        {
            requests = [new WorkSearchRequest { Query = normalizedQuery, Limit = MaxWorksPerSearch }];
        }

        // Task.WhenAll keeps the order it was given, so the most likely reading's works still come first.
        var searches = await Task.WhenAll(requests.Select(request => SearchWorksAsync(request, cancellationToken)));

        logger.LogInformation(
            "\nWorks per search: {PerSearch}\nTotal: {Total}\n",
            string.Join(", ", searches.Select(search => search.Docs.Count)),
            searches.Sum(search => search.Docs.Count));

        // A refused reading is only logged while others got through. When Open Library refused every one,
        // the client is told why, with all of Open Library's reasons.
        var rejections = searches.Select(search => search.Rejection).OfType<OpenLibraryQueryRejectedException>().ToList();
        if (rejections.Count == searches.Length)
        {
            throw new OpenLibraryQueryRejectedException(rejections.SelectMany(rejection => rejection.Reasons).Distinct().ToList());
        }

        return searches
            .SelectMany(search => search.Docs)
            .DistinctBy(doc => doc.Key)
            .ToList();
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
    /// Sends one search to Open Library and logs it with every work it found. A search Open Library refuses,
    /// such as q=the or a q under 3 characters, comes back with no works and the refusal, so the caller can
    /// decide whether the user needs to hear about it.
    /// </summary>
    private async Task<(IReadOnlyList<WorkSearchDoc> Docs, OpenLibraryQueryRejectedException? Rejection)> SearchWorksAsync(WorkSearchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkSearchDoc> docs;
        try
        {
            docs = (await openLibrary.SearchWorksAsync(request, cancellationToken)).Docs;
        }
        catch (OpenLibraryQueryRejectedException ex)
        {
            logger.LogInformation("Open Library refused the search {Search}: {Reasons}", Describe(request), string.Join("; ", ex.Reasons));
            return ([], ex);
        }

        logger.LogInformation(
            "\nOpen Library search {Search} found {Count} works:\n{Works}\n",
            Describe(request),
            docs.Count,
            string.Join("\n", docs.Select(doc => $"  {doc.WorkId} \"{doc.Title}\" by {string.Join(", ", doc.AuthorNames)} ({doc.FirstPublishYear})")));
        return (docs, null);
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
