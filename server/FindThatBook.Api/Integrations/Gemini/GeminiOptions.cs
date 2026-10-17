namespace FindThatBook.Api.Integrations.Gemini;

/// <summary>Settings from the "Gemini" configuration section.</summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";

    /// <summary>The model code, for example "gemini-3.5-flash-lite".</summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Comes from the GEMINI_API_KEY environment variable (see docker-compose.yml).
    /// Optional at startup so the app runs without it; every call fails with a 502 until it is set.
    /// </summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}
