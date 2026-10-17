using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;

namespace FindThatBook.Api.Tests.TestSupport;

/// <summary>Records each request and answers with a canned response, so no test calls the real Gemini.</summary>
public class FakeGeminiClient(Func<GenerateContentRequest, GenerateContentResponse> respond) : IGeminiClient
{
    public List<GenerateContentRequest> Requests { get; } = [];

    public static FakeGeminiClient Answering(string text) =>
        new(_ => new GenerateContentResponse
        {
            Candidates = [new Candidate { Content = new Content { Role = "model", Parts = [new Part { Text = text }] }, FinishReason = "STOP" }],
        });

    public Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }
}
