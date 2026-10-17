using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>An author record from /authors/{id}.json. Every field is optional.</summary>
public class AuthorRecord
{
    /// <summary>The author's path, for example "/authors/OL26320A".</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>The name Open Library displays, for example "J.R.R. Tolkien".</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("personal_name")]
    public string? PersonalName { get; init; }

    [JsonPropertyName("fuller_name")]
    public string? FullerName { get; init; }

    /// <summary>Other spellings and forms of the name, for example "J. R. R. Tolkien" or "Tolkien, J. R. R.".</summary>
    [JsonPropertyName("alternate_names")]
    public IReadOnlyList<string> AlternateNames { get; init; } = [];

    /// <summary>Free text, for example "3 January 1892".</summary>
    [JsonPropertyName("birth_date")]
    public string? BirthDate { get; init; }

    /// <summary>Free text, for example "2 September 1973".</summary>
    [JsonPropertyName("death_date")]
    public string? DeathDate { get; init; }

    [JsonPropertyName("bio")]
    [JsonConverter(typeof(TextValueJsonConverter))]
    public string? Bio { get; init; }

    /// <summary>Photo IDs, used in https://covers.openlibrary.org/a/id/{id}-M.jpg. Can contain -1, which means no image.</summary>
    [JsonPropertyName("photos")]
    public IReadOnlyList<long> Photos { get; init; } = [];

    /// <summary>The author's ID without its path, for example "OL26320A". Null when there is no key.</summary>
    [JsonIgnore]
    public string? AuthorId => OpenLibraryKeys.ToId(Key);
}
