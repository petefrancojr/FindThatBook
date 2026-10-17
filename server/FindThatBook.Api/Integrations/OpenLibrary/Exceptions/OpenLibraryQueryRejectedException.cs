namespace FindThatBook.Api.Integrations.OpenLibrary.Exceptions;

/// <summary>
/// Open Library answered, but refused the query (a 422), for example "the" or anything under
/// 3 characters. It is not an outage: the same query will be refused again, so retrying won't help.
/// The caller decides whether to treat it as no matches or to pass the reasons on.
/// </summary>
public class OpenLibraryQueryRejectedException(IReadOnlyList<string> reasons)
    : Exception(reasons.Count > 0
        ? $"Open Library refused the query: {string.Join("; ", reasons)}"
        : "Open Library refused the query.")
{
    /// <summary>Open Library's own messages, for example "Query too short, must be at least 3 characters".</summary>
    public IReadOnlyList<string> Reasons { get; } = reasons;
}
