using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

public class BookSearchService : IBookSearchService
{
    public Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        // Returns placeholder results until Open Library and Gemini are wired in.
        return Task.FromResult(DummyBookData.Search(query));
    }
}
