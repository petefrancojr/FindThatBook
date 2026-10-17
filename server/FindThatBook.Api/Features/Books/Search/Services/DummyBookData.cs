using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>Placeholder results until Open Library and Gemini are wired in.</summary>
public static class DummyBookData
{
    public static BookSearchResponse Search(string query) => new()
    {
        Query = query,
        Results =
        [
            new BookResult
            {
                Title = "Pride and Prejudice",
                Author = "Jane Austen",
                FirstPublishYear = 1813,
                OpenLibraryId = "OL66554W",
                Url = "https://openlibrary.org/works/OL66554W",
                CoverUrl = "https://covers.openlibrary.org/b/id/14348537-M.jpg",
                Tier = "A",
                Explanation = "Austen is the primary author; Bennet is the family at the centre of this novel."
            }
        ]
    };
}
