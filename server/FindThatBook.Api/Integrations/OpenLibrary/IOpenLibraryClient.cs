using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Integrations.OpenLibrary;

/// <summary>
/// Calls Open Library's search, works and authors APIs (https://openlibrary.org/developers/api).
/// General purpose: it returns Open Library's data as it is, and each feature decides
/// how to match, merge and rank it.
/// </summary>
/// <remarks>
/// Methods that take an ID accept it bare ("OL27482W") or as a key ("/works/OL27482W").
/// Every method throws <see cref="OpenLibraryUnavailableException"/> when Open Library can't be
/// reached, times out, returns an error status or returns something that isn't JSON.
/// </remarks>
public interface IOpenLibraryClient
{
    /// <summary>
    /// Searches works (/search.json). A search with no matches returns an empty
    /// <see cref="WorkSearchResponse.Docs"/> list, not an error.
    /// </summary>
    /// <exception cref="ArgumentException">The request has no query, title, author or subject.</exception>
    Task<WorkSearchResponse> SearchWorksAsync(WorkSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets one work (/works/{id}.json). Returns null when there is no such work.</summary>
    /// <exception cref="ArgumentException">The ID isn't a work ID such as "OL27482W".</exception>
    Task<WorkRecord?> GetWorkAsync(string workId, CancellationToken cancellationToken = default);

    /// <summary>Gets one author (/authors/{id}.json). Returns null when there is no such author.</summary>
    /// <exception cref="ArgumentException">The ID isn't an author ID such as "OL26320A".</exception>
    Task<AuthorRecord?> GetAuthorAsync(string authorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one page of an author's works (/authors/{id}/works.json). The works are not ranked by
    /// popularity; to find an author's best-known works, search with <see cref="WorkSearchRequest.Author"/>.
    /// Returns null when there is no such author.
    /// </summary>
    /// <param name="limit">Works per page. Open Library's default is used when not set.</param>
    /// <param name="offset">How many works to skip.</param>
    /// <exception cref="ArgumentException">The ID isn't an author ID such as "OL26320A".</exception>
    Task<AuthorWorksResponse?> GetAuthorWorksAsync(string authorId, int? limit = null, int? offset = null, CancellationToken cancellationToken = default);
}
