namespace FindThatBook.Api.Integrations.Gemini.Models;

public class PromptFeedback
{
    /// <summary>Set when Gemini refused the prompt, for example "SAFETY". No candidates come back then.</summary>
    public string? BlockReason { get; init; }
}
