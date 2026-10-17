using System.Net;
using System.Text.Json;
using FindThatBook.Api.Features.Books.Search.DTOs;
using FindThatBook.Api.Features.Books.Search.Services;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Exceptions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FindThatBook.Api.Tests.Common.Errors;

/// <summary>Checks the whole pipeline turns an unavailable external service into a 502.</summary>
public class ExternalServiceErrorEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string InternalReason = "Gemini returned 503. UNAVAILABLE: internal detail key=secret";

    private HttpClient ClientWhereSearchFailsWith(Exception exception) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddScoped<IBookSearchService>(_ => new ThrowingBookSearchService(exception))))
            .CreateClient();

    [Fact]
    public async Task ReturnsA502ProblemDetailsWhenGeminiIsUnavailable()
    {
        var client = ClientWhereSearchFailsWith(new GeminiUnavailableException(InternalReason, HttpStatusCode.ServiceUnavailable));

        var response = await client.GetAsync("/api/books/search?q=austen");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(502, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Gemini is unavailable right now. Try again in a moment.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task DoesNotSendTheInternalReasonToTheClient()
    {
        var client = ClientWhereSearchFailsWith(new GeminiUnavailableException(InternalReason));

        var body = await (await client.GetAsync("/api/books/search?q=austen")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("internal detail", body);
        Assert.DoesNotContain("secret", body);
    }

    [Fact]
    public async Task LeavesOtherExceptionsAsA500()
    {
        var client = ClientWhereSearchFailsWith(new InvalidOperationException("bug"));

        var response = await client.GetAsync("/api/books/search?q=austen");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private class ThrowingBookSearchService(Exception exception) : IBookSearchService
    {
        public Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromException<BookSearchResponse>(exception);
    }
}
