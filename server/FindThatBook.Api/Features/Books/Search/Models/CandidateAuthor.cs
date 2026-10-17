namespace FindThatBook.Api.Features.Books.Search.Models;

/// <summary>
/// One person listed on a candidate work, and how they are credited.
/// Used inside <see cref="Services.BookSearchService"/>; never sent to the client.
/// </summary>
public class CandidateAuthor
{
    public required string Name { get; init; }

    /// <summary>The author's Open Library ID, for example "OL26320A".</summary>
    public string? AuthorId { get; init; }

    public required AuthorCredit Credit { get; init; }

    /// <summary>The role Open Library recorded, for example "Adapter". Only set when a record gives one.</summary>
    public string? Role { get; init; }

    /// <summary>Why the person got this credit, for logs: for example "role in contributor: Adapter".</summary>
    public required string Reason { get; init; }
}
