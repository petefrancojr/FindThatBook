namespace FindThatBook.Api.Integrations.Gemini.Models;

/// <summary>Body of a generateContent call, field for field as Gemini's REST API expects it.</summary>
public class GenerateContentRequest
{
    public required IReadOnlyList<Content> Contents { get; init; }

    /// <summary>Instructions the model follows for the whole request, separate from the user's text.</summary>
    public Content? SystemInstruction { get; init; }

    public GenerationConfig? GenerationConfig { get; init; }
}
