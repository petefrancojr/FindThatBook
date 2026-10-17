namespace FindThatBook.Api.Integrations.Gemini.Models;

/// <summary>The body Gemini sends with an error status: { "error": { code, message, status } }.</summary>
public class GeminiErrorResponse
{
    public GeminiError? Error { get; init; }
}

public class GeminiError
{
    public int Code { get; init; }
    public string? Message { get; init; }

    /// <summary>For example "RESOURCE_EXHAUSTED" or "UNAVAILABLE".</summary>
    public string? Status { get; init; }
}
