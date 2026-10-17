using System.Text.RegularExpressions;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>
/// Turns Open Library keys such as "/works/OL27482W" into bare IDs such as "OL27482W",
/// and tells what kind of record an ID belongs to from its last letter: W for works, A for authors.
/// </summary>
public static partial class OpenLibraryKeys
{
    public static string? ToId(string? key) =>
        key?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

    /// <summary>True for a bare work ID such as "OL27482W". An edition ID ("OL8866470M") is not a work ID.</summary>
    public static bool IsWorkId(string? id) => id is not null && WorkIdPattern().IsMatch(id);

    /// <summary>True for a bare author ID such as "OL26320A".</summary>
    public static bool IsAuthorId(string? id) => id is not null && AuthorIdPattern().IsMatch(id);

    [GeneratedRegex("^OL[0-9]+W$")]
    private static partial Regex WorkIdPattern();

    [GeneratedRegex("^OL[0-9]+A$")]
    private static partial Regex AuthorIdPattern();
}
