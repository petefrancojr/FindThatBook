namespace FindThatBook.Api.Features.Books.Search.DTOs;

/// <summary>Someone who worked on a book without writing it, such as its adapter or illustrator.</summary>
public class BookContributor
{
    public required string Name { get; init; }

    /// <summary>
    /// The role Open Library recorded, for example "Adapter" or "Illustrator". Null when the person is known to be
    /// a contributor but no role was recorded.
    /// </summary>
    public string? Role { get; init; }
}
