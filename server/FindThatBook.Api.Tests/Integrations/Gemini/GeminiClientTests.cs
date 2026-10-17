using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Exceptions;
using FindThatBook.Api.Integrations.Gemini.Models;
using FindThatBook.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Tests.Integrations.Gemini;

public class GeminiClientTests
{
    private const string ApiKey = "test-key-123";

    private const string SuccessBody = """
        {
          "candidates": [
            { "content": { "role": "model", "parts": [{ "text": "Hello, " }, { "text": "reader." }] }, "finishReason": "STOP" }
          ],
          "usageMetadata": { "promptTokenCount": 7, "candidatesTokenCount": 3, "totalTokenCount": 10 },
          "modelVersion": "gemini-test-001"
        }
        """;

    private static readonly GenerateContentRequest SimpleRequest = new()
    {
        Contents = [new Content { Role = "user", Parts = [new Part { Text = "Say hello." }] }],
    };

    private static GeminiClient CreateClient(
        StubHttpMessageHandler handler,
        string? apiKey = ApiKey,
        TimeSpan? timeout = null) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://gemini.test/"),
                Timeout = timeout ?? TimeSpan.FromSeconds(5),
            },
            Options.Create(new GeminiOptions { ApiKey = apiKey, Model = "gemini-test", TimeoutSeconds = 5 }),
            NullLogger<GeminiClient>.Instance);

    [Fact]
    public async Task PostsToTheModelsGenerateContentEndpoint()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        await CreateClient(handler).GenerateContentAsync(SimpleRequest);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://gemini.test/v1beta/models/gemini-test:generateContent", request.Uri.ToString());
    }

    [Fact]
    public async Task SendsTheApiKeyInAHeaderAndNeverInTheUrl()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        await CreateClient(handler).GenerateContentAsync(SimpleRequest);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(ApiKey, Assert.Single(request.Headers.GetValues("x-goog-api-key")));
        Assert.DoesNotContain(ApiKey, request.Uri.ToString());
        Assert.DoesNotContain(ApiKey, request.Body);
    }

    [Fact]
    public async Task SerializesTheRequestInGeminisCamelCaseShapeAndLeavesOutUnsetFields()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);
        var request = new GenerateContentRequest
        {
            SystemInstruction = new Content { Parts = [new Part { Text = "Answer in JSON." }] },
            Contents = [new Content { Role = "user", Parts = [new Part { Text = "austen bennet" }] }],
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.2,
                ResponseMimeType = "application/json",
                ResponseJsonSchema = JsonNode.Parse("""{ "type": "object" }"""),
            },
        };

        await CreateClient(handler).GenerateContentAsync(request);

        var body = JsonNode.Parse(Assert.Single(handler.Requests).Body!)!;
        Assert.Equal("Answer in JSON.", (string?)body["systemInstruction"]!["parts"]![0]!["text"]);
        Assert.Null(body["systemInstruction"]!["role"]);
        Assert.Equal("user", (string?)body["contents"]![0]!["role"]);
        Assert.Equal("austen bennet", (string?)body["contents"]![0]!["parts"]![0]!["text"]);
        Assert.Equal(0.2, (double?)body["generationConfig"]!["temperature"]);
        Assert.Equal("application/json", (string?)body["generationConfig"]!["responseMimeType"]);
        Assert.Equal("object", (string?)body["generationConfig"]!["responseJsonSchema"]!["type"]);
        Assert.Null(body["generationConfig"]!["maxOutputTokens"]);
    }

    [Fact]
    public async Task ReadsTheAnswerJoiningTheTextParts()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        var response = await CreateClient(handler).GenerateContentAsync(SimpleRequest);

        Assert.Equal("Hello, reader.", response.Text);
        Assert.Equal("STOP", response.Candidates[0].FinishReason);
        Assert.Equal(10, response.UsageMetadata?.TotalTokenCount);
        Assert.Equal("gemini-test-001", response.ModelVersion);
    }

    [Fact]
    public async Task ReturnsABlockedPromptWithoutThrowingSoTheCallerCanFallBack()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{ "promptFeedback": { "blockReason": "SAFETY" } }""");

        var response = await CreateClient(handler).GenerateContentAsync(SimpleRequest);

        Assert.Null(response.Text);
        Assert.Empty(response.Candidates);
        Assert.Equal("SAFETY", response.PromptFeedback?.BlockReason);
    }

    [Fact]
    public async Task TreatsAnAnswerWithOnlyBlankTextAsNoText()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK,
            """{ "candidates": [{ "content": { "parts": [{ "text": "  " }] }, "finishReason": "MAX_TOKENS" }] }""");

        var response = await CreateClient(handler).GenerateContentAsync(SimpleRequest);

        Assert.Null(response.Text);
        Assert.Equal("MAX_TOKENS", response.Candidates[0].FinishReason);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_ARGUMENT")]
    [InlineData(HttpStatusCode.Forbidden, "PERMISSION_DENIED")]
    [InlineData(HttpStatusCode.TooManyRequests, "RESOURCE_EXHAUSTED")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE")]
    public async Task ThrowsUnavailableWithGeminisReasonOnAnErrorStatus(HttpStatusCode status, string geminiStatus)
    {
        var handler = StubHttpMessageHandler.Returning(status,
            $$"""{ "error": { "code": {{(int)status}}, "message": "Something went wrong.", "status": "{{geminiStatus}}" } }""");

        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CreateClient(handler).GenerateContentAsync(SimpleRequest));

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("Gemini", ex.ServiceName);
        Assert.Contains(geminiStatus, ex.Message);
        Assert.Contains("Something went wrong.", ex.Message);
    }

    [Fact]
    public async Task ThrowsUnavailableWhenAnErrorBodyIsNotGeminiJson()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.BadGateway, "<html>Bad gateway</html>", "text/html");

        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CreateClient(handler).GenerateContentAsync(SimpleRequest));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Equal("Gemini returned 502.", ex.Message);
    }

    [Fact]
    public async Task ThrowsUnavailableWhenASuccessBodyIsNotValidJson()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "not json");

        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CreateClient(handler).GenerateContentAsync(SimpleRequest));

        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    [Fact]
    public async Task ThrowsUnavailableWhenGeminiCannotBeReached()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("No such host is known."));

        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CreateClient(handler).GenerateContentAsync(SimpleRequest));

        Assert.Null(ex.StatusCode);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task ThrowsUnavailableWhenGeminiTimesOut()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() =>
            CreateClient(handler, timeout: TimeSpan.FromMilliseconds(50)).GenerateContentAsync(SimpleRequest));

        Assert.Contains("did not answer within", ex.Message);
    }

    [Fact]
    public async Task LetsACancellationByTheCallerPropagate()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(handler).GenerateContentAsync(SimpleRequest, cancellation.Token));

        Assert.IsNotType<GeminiUnavailableException>(ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ThrowsUnavailableWithoutCallingGeminiWhenNoApiKeyIsSet(string? apiKey)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() =>
            CreateClient(handler, apiKey).GenerateContentAsync(SimpleRequest));

        Assert.Contains("GEMINI_API_KEY", ex.Message);
        Assert.Empty(handler.Requests);
    }
}
