using FindThatBook.Api.Features.Books.Search.Controllers;
using FindThatBook.Api.Features.Books.Search.Models;
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

    [Fact]
    public async Task Search_ReturnsServiceResponseAsOk()
    {
        var service = new FakeBookSearchService();
        var controller = new BookSearchController(service);

        var result = await controller.Search("austen bennet", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(service.Response, ok.Value);
    }

    private class FakeBookSearchService : IBookSearchService
    {
        public string? ReceivedQuery { get; private set; }

        public BookSearchResponse Response { get; } = new()
        {
            Query = "austen bennet",
            InterpretedAs = new InterpretedQuery(),
            Results = []
        };

        public Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;
            return Task.FromResult(Response);
        }
    }
}
