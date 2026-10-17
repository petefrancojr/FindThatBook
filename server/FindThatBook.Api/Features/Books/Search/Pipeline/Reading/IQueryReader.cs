
namespace FindThatBook.Api.Features.Books.Search.Pipeline.Reading;

/// <summary>Works out the books a messy search could mean, as one or more readings, most likely first.</summary>
public interface IQueryReader
{
    /// <summary>
    /// Reads the search into normalized readings. Returns an empty list when no reading could be made,
    /// for example when the answer is blocked or isn't valid JSON.
    /// </summary>
    Task<IReadOnlyList<QueryReading>> ReadAsync(string query, CancellationToken cancellationToken = default);
}
