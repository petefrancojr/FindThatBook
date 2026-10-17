using FindThatBook.Api.Integrations.OpenLibrary;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Tests.TestSupport;

/// <summary>Records each search and answers with canned works, so no test calls the real Open Library.</summary>
public class FakeOpenLibraryClient(Func<WorkSearchRequest, IReadOnlyList<WorkSearchDoc>> search) : IOpenLibraryClient
{
    public List<WorkSearchRequest> Searches { get; } = [];

    /// <summary>Every search finds the same works.</summary>
    public static FakeOpenLibraryClient Finding(params WorkSearchDoc[] works) => new(_ => works);

    public Task<WorkSearchResponse> SearchWorksAsync(WorkSearchRequest request, CancellationToken cancellationToken = default)
    {
        lock (Searches)
        {
            Searches.Add(request);
        }

        // Called on another thread, so an exception from the test's function surfaces like the real client's.
        return Task.Run(() => new WorkSearchResponse { Docs = search(request) }, cancellationToken);
    }

    public Task<WorkRecord?> GetWorkAsync(string workId, CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkRecord?>(null);

    public Task<AuthorRecord?> GetAuthorAsync(string authorId, CancellationToken cancellationToken = default) =>
        Task.FromResult<AuthorRecord?>(null);

    public Task<AuthorWorksResponse?> GetAuthorWorksAsync(string authorId, int? limit = null, int? offset = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<AuthorWorksResponse?>(null);
}
