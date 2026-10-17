using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FindThatBook.Api.Tests.Features.Books.Search;

/// <summary>Runs requests through the real ASP.NET pipeline: routing, validation and JSON output.</summary>
public class BookSearchEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_WithValidQuery_Returns200()
    {
        var response = await _client.GetAsync("/api/books/search?q=austen%20bennet");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_UsesTheJsonFieldNamesFromTheSpec()
    {
        var response = await _client.GetAsync("/api/books/search?q=austen%20bennet");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var root = json.RootElement;
        Assert.True(root.TryGetProperty("query", out _));

        var book = root.GetProperty("results")[0];
        foreach (var field in new[] { "title", "author", "first_publish_year", "open_library_id", "url", "cover_url", "tier", "explanation" })
        {
            Assert.True(book.TryGetProperty(field, out _), $"Missing result field '{field}'");
        }
    }

    [Fact]
    public async Task Search_ReturnsAtMostFiveResults()
    {
        var response = await _client.GetAsync("/api/books/search?q=austen%20bennet");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.InRange(json.RootElement.GetProperty("results").GetArrayLength(), 0, 5);
    }

    [Theory]
    [InlineData("/api/books/search")]
    [InlineData("/api/books/search?q=")]
    [InlineData("/api/books/search?q=%20%20%20")]
    public async Task Search_WithMissingOrBlankQuery_Returns400(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithQueryOver200Characters_Returns400()
    {
        var response = await _client.GetAsync("/api/books/search?q=" + new string('a', 201));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at most 200 characters", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_WithQueryOfExactly200Characters_Returns200()
    {
        var response = await _client.GetAsync("/api/books/search?q=" + new string('a', 200));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
