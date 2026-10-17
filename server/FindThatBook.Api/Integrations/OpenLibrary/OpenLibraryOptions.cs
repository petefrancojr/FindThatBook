namespace FindThatBook.Api.Integrations.OpenLibrary;

/// <summary>Settings from the "OpenLibrary" configuration section.</summary>
public class OpenLibraryOptions
{
    public const string SectionName = "OpenLibrary";

    public string BaseUrl { get; set; } = "https://openlibrary.org/";

    /// <summary>
    /// Sent with every request. Open Library asks applications to identify themselves with a
    /// name and, for regular use, a contact address, for example "FindThatBook/1.0 (you@example.com)".
    /// </summary>
    public string UserAgent { get; set; } = "FindThatBook/1.0";

    public int TimeoutSeconds { get; set; } = 10;
}
