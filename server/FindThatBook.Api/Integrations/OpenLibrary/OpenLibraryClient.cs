using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Integrations.OpenLibrary;

public class OpenLibraryClient(HttpClient httpClient, IOptions<OpenLibraryOptions> options, ILogger<OpenLibraryClient> logger) : IOpenLibraryClient
{
    // Field names are set on each model with [JsonPropertyName]: Open Library mixes snake_case and camelCase.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkSearchResponse> SearchWorksAsync(WorkSearchRequest request, CancellationToken cancellationToken = default)
    {
        var url = BuildSearchUrl(request);

        // A 404 here means the endpoint is wrong, not that nothing matched: no matches is an empty list.
        return await GetJsonAsync<WorkSearchResponse>(url, notFoundIsNull: false, cancellationToken)
            ?? throw new OpenLibraryUnavailableException("Open Library returned an empty response.");
    }

    public Task<WorkRecord?> GetWorkAsync(string workId, CancellationToken cancellationToken = default)
    {
        var id = ToValidId(workId, OpenLibraryKeys.IsWorkId, "work", "OL27482W", nameof(workId));

        return GetJsonAsync<WorkRecord>($"works/{id}.json", notFoundIsNull: true, cancellationToken);
    }

    public Task<AuthorRecord?> GetAuthorAsync(string authorId, CancellationToken cancellationToken = default)
    {
        var id = ToValidId(authorId, OpenLibraryKeys.IsAuthorId, "author", "OL26320A", nameof(authorId));

        return GetJsonAsync<AuthorRecord>($"authors/{id}.json", notFoundIsNull: true, cancellationToken);
    }

    public Task<AuthorWorksResponse?> GetAuthorWorksAsync(string authorId, int? limit = null, int? offset = null, CancellationToken cancellationToken = default)
    {
        var id = ToValidId(authorId, OpenLibraryKeys.IsAuthorId, "author", "OL26320A", nameof(authorId));

        var parameters = new List<KeyValuePair<string, string?>>();
        if (limit is not null)
        {
            parameters.Add(new("limit", limit.Value.ToString()));
        }
        if (offset is not null)
        {
            parameters.Add(new("offset", offset.Value.ToString()));
        }

        var url = QueryHelpers.AddQueryString($"authors/{id}/works.json", parameters);
        return GetJsonAsync<AuthorWorksResponse>(url, notFoundIsNull: true, cancellationToken);
    }

    /// <summary>
    /// Sends a GET and reads the JSON. Returns null for a 404 when <paramref name="notFoundIsNull"/> is set;
    /// every other failure throws <see cref="OpenLibraryUnavailableException"/>.
    /// </summary>
    private async Task<T?> GetJsonAsync<T>(string url, bool notFoundIsNull, CancellationToken cancellationToken) where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new OpenLibraryUnavailableException("Open Library could not be reached.", innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout fired. A cancellation by the caller is left to propagate.
            throw new OpenLibraryUnavailableException(
                $"Open Library did not answer within {options.Value.TimeoutSeconds} seconds.", innerException: ex);
        }

        using (response)
        {
            logger.LogInformation(
                "Open Library {Url} returned {StatusCode} in {ElapsedMs} ms",
                url,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            if (response.StatusCode == HttpStatusCode.NotFound && notFoundIsNull)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                throw new OpenLibraryQueryRejectedException(await ReadRejectionReasonsAsync(response, cancellationToken));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new OpenLibraryUnavailableException(
                    $"Open Library returned {(int)response.StatusCode} for {url}.", response.StatusCode);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                // ReadFromJsonAsync doesn't check the content type, so an HTML error page with a 200 status lands here too.
                throw new OpenLibraryUnavailableException("Open Library returned a response that isn't valid JSON.", response.StatusCode, ex);
            }
        }
    }

    /// <summary>Reads Open Library's messages from a 422 body. An unreadable body gives no reasons.</summary>
    private static async Task<IReadOnlyList<string>> ReadRejectionReasonsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<QueryRejectedResponse>(JsonOptions, cancellationToken);
            return body?.Details
                .Select(detail => CleanRejectionMessage(detail.Message))
                .OfType<string>()
                .Where(message => message.Length > 0)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// "Value error, Query too short, ..." becomes "Query too short, ...". Some messages also arrive
    /// wrapped as a Python tuple, "('Invalid query; ...',)", which is unwrapped.
    /// </summary>
    private static string? CleanRejectionMessage(string? message)
    {
        var cleaned = message?.Replace("Value error, ", "").Trim();
        if (cleaned is not null && cleaned.StartsWith("('") && cleaned.EndsWith("',)"))
        {
            cleaned = cleaned[2..^3].Trim();
        }

        return cleaned;
    }

    /// <summary>
    /// Accepts only a bare ID such as "OL27482W", in any case, and returns it upper-cased, the form Open Library uses.
    /// Anything else is rejected, so a caller's value can never change the path being requested.
    /// Models give the bare form through WorkId, AuthorId and KeyReference.Id.
    /// </summary>
    private static string ToValidId(string value, Func<string?, bool> isValid, string kind, string example, string parameterName)
    {
        var id = value?.ToUpperInvariant();
        if (!isValid(id))
        {
            throw new ArgumentException($"'{value}' isn't an Open Library {kind} ID such as \"{example}\".", parameterName);
        }

        return id!;
    }

    /// <summary>Builds "search.json?..." with only the parameters that are set, each one URL-encoded.</summary>
    private static string BuildSearchUrl(WorkSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query)
            && string.IsNullOrWhiteSpace(request.Title)
            && string.IsNullOrWhiteSpace(request.Author)
            && string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new ArgumentException("A work search needs a query, title, author or subject.", nameof(request));
        }

        var fields = request.Fields is { Count: > 0 } ? request.Fields : WorkSearchRequest.DefaultFields;

        var parameters = new List<KeyValuePair<string, string?>>();
        AddIfSet("q", request.Query);
        AddIfSet("title", request.Title);
        AddIfSet("author", request.Author);
        AddIfSet("subject", request.Subject);
        AddIfSet("fields", string.Join(',', fields));
        AddIfSet("sort", request.Sort);
        AddIfSet("lang", request.Language);
        AddIfSet("limit", request.Limit?.ToString());
        AddIfSet("page", request.Page?.ToString());
        AddIfSet("offset", request.Offset?.ToString());

        return QueryHelpers.AddQueryString("search.json", parameters);

        void AddIfSet(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(new(name, value.Trim()));
            }
        }
    }
}
