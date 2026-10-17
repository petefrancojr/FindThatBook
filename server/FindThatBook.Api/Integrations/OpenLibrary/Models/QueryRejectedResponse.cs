using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>
/// The body Open Library sends with a 422 when it refuses a query:
/// { "detail": [ { "msg": "...", "input": "the" } ] }.
/// </summary>
public class QueryRejectedResponse
{
    [JsonPropertyName("detail")]
    public IReadOnlyList<QueryRejectedDetail> Details { get; init; } = [];
}

public class QueryRejectedDetail
{
    /// <summary>For example "Value error, Query too short, must be at least 3 characters".</summary>
    [JsonPropertyName("msg")]
    public string? Message { get; init; }

    /// <summary>The value that was refused.</summary>
    [JsonPropertyName("input")]
    public string? Input { get; init; }
}
