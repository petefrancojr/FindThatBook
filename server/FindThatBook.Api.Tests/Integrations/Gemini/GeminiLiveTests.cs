using System.Text.Json;
using System.Text.Json.Nodes;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FindThatBook.Api.Tests.Integrations.Gemini;

/// <summary>
/// Calls the real Gemini API. Skipped unless LIVE_TESTS=true and GEMINI_API_KEY is set:
/// docker compose run --rm -e LIVE_TESTS=true server-tests dotnet test FindThatBook.Api.Tests --filter Category=Live
/// </summary>
[Trait("Category", "Live")]
public class GeminiLiveTests
{
    private static IGeminiClient CreateClient() => LiveTests.CreateGeminiClient();

    [LiveFact]
    public async Task AnswersAPlainTextPrompt()
    {
        var response = await CreateClient().GenerateContentAsync(new GenerateContentRequest
        {
            Contents = [new Content { Role = "user", Parts = [new Part { Text = "Reply with the single word: pong" }] }],
        });

        Assert.Contains("pong", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [LiveFact]
    public async Task ReturnsJsonThatFollowsTheSchema()
    {
        var response = await CreateClient().GenerateContentAsync(new GenerateContentRequest
        {
            Contents = [new Content { Role = "user", Parts = [new Part { Text = "Who wrote Pride and Prejudice?" }] }],
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0,
                ResponseMimeType = "application/json",
                ResponseJsonSchema = JsonNode.Parse("""
                    {
                      "type": "object",
                      "properties": { "author": { "type": "string" } },
                      "required": ["author"]
                    }
                    """),
            },
        });

        using var json = JsonDocument.Parse(response.Text!);
        Assert.Contains("Austen", json.RootElement.GetProperty("author").GetString());
    }
}

/// <summary>A fact that only runs when live tests are switched on and a key is available.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        Skip = LiveTests.SkipReason();
    }
}

/// <summary>A theory that only runs when live tests are switched on and a key is available.</summary>
public sealed class LiveTheoryAttribute : TheoryAttribute
{
    public LiveTheoryAttribute()
    {
        Skip = LiveTests.SkipReason();
    }
}

public static class LiveTests
{
    /// <summary>Why live tests are skipped, or null when they can run.</summary>
    public static string? SkipReason()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("LIVE_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return "Live tests are off. Set LIVE_TESTS=true to run them.";
        }

        return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
            ? "GEMINI_API_KEY is not set."
            : null;
    }

    /// <summary>The real Gemini client, configured from GEMINI_API_KEY, with a longer timeout than the app's.</summary>
    public static IGeminiClient CreateGeminiClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
                ["Gemini:TimeoutSeconds"] = "30",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(configuration);
        return services.BuildServiceProvider().GetRequiredService<IGeminiClient>();
    }
}
