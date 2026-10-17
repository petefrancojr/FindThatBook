namespace FindThatBook.Api.Integrations.Gemini.Models;

/// <summary>One message: who sent it and its parts.</summary>
public class Content
{
    /// <summary>"user" or "model". Left out for a system instruction.</summary>
    public string? Role { get; init; }

    public required IReadOnlyList<Part> Parts { get; init; }
}
