using FindThatBook.Api.Integrations.Gemini.Models;

namespace FindThatBook.Api.Integrations.Gemini;

/// <summary>
/// Sends requests to Gemini's generateContent API. General purpose: it knows nothing about
/// books. Each feature writes its own prompts and reads the answer in its own way.
/// </summary>
public interface IGeminiClient
{
    /// <summary>
    /// Returns Gemini's response. An answer with no text (for example a blocked prompt) is
    /// returned as is, with <see cref="GenerateContentResponse.Text"/> null.
    /// </summary>
    /// <exception cref="GeminiUnavailableException">
    /// Gemini couldn't be reached, timed out, rejected the request or returned an error status.
    /// </exception>
    Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);
}
