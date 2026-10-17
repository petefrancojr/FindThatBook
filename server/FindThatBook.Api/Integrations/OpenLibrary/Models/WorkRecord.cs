using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.OpenLibrary.Models;

/// <summary>
/// A work record from /works/{id}.json, also used for each entry of /authors/{id}/works.json.
/// Every field is optional: Open Library leaves out what a record doesn't have.
/// </summary>
public class WorkRecord
{
    /// <summary>The work's path, for example "/works/OL27482W".</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; init; }

    /// <summary>The work's own author list. Unlike search results, it names the work's authors, with a role for contributors.</summary>
    [JsonPropertyName("authors")]
    public IReadOnlyList<WorkAuthorRole> Authors { get; init; } = [];

    /// <summary>Free text, for example "January 1938". Not always a plain year.</summary>
    [JsonPropertyName("first_publish_date")]
    public string? FirstPublishDate { get; init; }

    [JsonPropertyName("description")]
    [JsonConverter(typeof(TextValueJsonConverter))]
    public string? Description { get; init; }

    [JsonPropertyName("first_sentence")]
    [JsonConverter(typeof(TextValueJsonConverter))]
    public string? FirstSentence { get; init; }

    /// <summary>Cover image IDs, used in https://covers.openlibrary.org/b/id/{id}-M.jpg. Can contain -1, which means no image.</summary>
    [JsonPropertyName("covers")]
    public IReadOnlyList<long> Covers { get; init; } = [];

    [JsonPropertyName("subjects")]
    public IReadOnlyList<string> Subjects { get; init; } = [];

    [JsonPropertyName("subject_people")]
    public IReadOnlyList<string> SubjectPeople { get; init; } = [];

    [JsonPropertyName("subject_places")]
    public IReadOnlyList<string> SubjectPlaces { get; init; } = [];

    [JsonPropertyName("subject_times")]
    public IReadOnlyList<string> SubjectTimes { get; init; } = [];

    /// <summary>The work's ID without its path, for example "OL27482W". Null when there is no key.</summary>
    [JsonIgnore]
    public string? WorkId => OpenLibraryKeys.ToId(Key);
}
