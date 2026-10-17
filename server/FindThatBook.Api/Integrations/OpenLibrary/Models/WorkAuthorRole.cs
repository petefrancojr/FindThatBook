using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>One entry in a work's "authors" list: who, and in what role.</summary>
public class WorkAuthorRole
{
    [JsonPropertyName("author")]
    public KeyReference? Author { get; init; }

    /// <summary>
    /// Set only for contributors, for example "Translator" or "Illustrator".
    /// No role means a primary author.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>The author's ID, for example "OL26320A". Null when the entry has no author key.</summary>
    [JsonIgnore]
    public string? AuthorId => Author?.Id;
}
