namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>Turns Open Library keys such as "/works/OL27482W" into bare IDs such as "OL27482W".</summary>
public static class OpenLibraryKeys
{
    public static string? ToId(string? key) =>
        key?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
}
