using System.Text.Json.Serialization;

namespace FindThatBook.Api.Integrations.Gemini.Models;

/// <summary>Gemini's answer to a generateContent call.</summary>
public class GenerateContentResponse
{
    public IReadOnlyList<Candidate> Candidates { get; init; } = [];
    public PromptFeedback? PromptFeedback { get; init; }
    public UsageMetadata? UsageMetadata { get; init; }
    public string? ModelVersion { get; init; }

    /// <summary>
    /// The text of the first candidate, or null when there is none, for example when
    /// the prompt was blocked. Callers decide what an empty answer means for them.
    /// </summary>
    [JsonIgnore]
    public string? Text
    {
        get
        {
            var parts = Candidates.FirstOrDefault()?.Content?.Parts;
            if (parts is null)
            {
                return null;
            }

            var text = string.Concat(parts.Select(part => part.Text));
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
