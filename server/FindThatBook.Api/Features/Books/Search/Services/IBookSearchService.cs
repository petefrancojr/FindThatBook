using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

public interface IBookSearchService
{
    /// <summary>Finds the books a messy search term most likely refers to.</summary>
    Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default);
}
