using System.Text.Json.Nodes;

namespace FindThatBook.Api.Integrations.Gemini.Models;

/// <summary>How the model should generate its answer. Every field is optional.</summary>
public class GenerationConfig
{
    public double? Temperature { get; init; }
    public int? MaxOutputTokens { get; init; }

    /// <summary>"application/json" asks for JSON instead of prose.</summary>
    public string? ResponseMimeType { get; init; }

    /// <summary>A JSON Schema the answer must follow. Needs ResponseMimeType "application/json".</summary>
    public JsonNode? ResponseJsonSchema { get; init; }
}
