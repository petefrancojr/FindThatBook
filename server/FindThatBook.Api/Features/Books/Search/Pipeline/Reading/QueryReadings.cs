namespace FindThatBook.Api.Features.Books.Search.Pipeline.Reading;

/// <summary>
/// The JSON Gemini returns for the query-reading prompt: { "readings": [ ... ] }.
/// Used inside <see cref="Services.BookSearchService"/>; never sent to the client.
/// </summary>
public class QueryReadings
{
    private readonly IReadOnlyList<QueryReading> _readings = [];

    /// <summary>Most likely reading first. Never null.</summary>
    public IReadOnlyList<QueryReading> Readings
    {
        get => _readings;
        init => _readings = value ?? [];
    }
}
