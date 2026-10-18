using FindThatBook.Api.Features.Books.Search.Controllers;
using FindThatBook.Api.Features.Books.Search.DTOs;
using FindThatBook.Api.Features.Books.Search.Services;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Tests.Features.Books.Search;

public class BookSearchControllerTests
{
    [Fact]
    public async Task Search_PassesTrimmedQueryToService()
    {
        var service = new FakeBookSearchService();
        var controller = new BookSearchController(service);

        await controller.Search("  austen bennet  ", CancellationToken.None);

        Assert.Equal("austen bennet", service.ReceivedQuery);
    }

    private class FakeBookSearchService : IBookSearchService
    {
        public string? ReceivedQuery { get; private set; }

        public BookSearchResponse Response { get; } = new()
        {
            Query = "austen bennet",
            Results = []
        };

        public Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;
            return Task.FromResult(Response);
        }
    }
}
