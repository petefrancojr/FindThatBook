namespace FindThatBook.Api.Integrations.Gemini.Models;

public class UsageMetadata
{
    public int PromptTokenCount { get; init; }
    public int CandidatesTokenCount { get; init; }
    public int TotalTokenCount { get; init; }
}
