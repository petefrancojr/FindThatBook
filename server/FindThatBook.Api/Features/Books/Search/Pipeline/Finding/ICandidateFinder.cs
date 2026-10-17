using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Pipeline.Finding;

/// <summary>Finds the works that could match a search's readings.</summary>
public interface ICandidateFinder
{
    /// <summary>
    /// Finds candidate works for the readings, most likely reading's works first, each work once.
    /// When there are no readings, <paramref name="normalizedQuery"/> is searched instead.
    /// </summary>
    /// <exception cref="OpenLibraryQueryRejectedException">Open Library refused every search made from the readings.</exception>
    Task<IReadOnlyList<WorkSearchDoc>> FindAsync(IReadOnlyList<QueryReading> readings, string normalizedQuery, CancellationToken cancellationToken = default);
}
