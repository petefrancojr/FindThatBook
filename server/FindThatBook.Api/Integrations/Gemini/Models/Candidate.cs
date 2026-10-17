namespace FindThatBook.Api.Integrations.Gemini.Models;

public class Candidate
{
    public Content? Content { get; init; }

    /// <summary>Why the model stopped, for example "STOP", "MAX_TOKENS" or "SAFETY".</summary>
    public string? FinishReason { get; init; }
}
