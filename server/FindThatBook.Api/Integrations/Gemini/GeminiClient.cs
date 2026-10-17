using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FindThatBook.Api.Integrations.Gemini.Exceptions;
using FindThatBook.Api.Integrations.Gemini.Models;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Integrations.Gemini;

public class GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger) : IGeminiClient
{
    private const string ApiKeyHeader = "x-goog-api-key";

    // Gemini's REST API uses camelCase. Unset optional fields are left out of the request.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new GeminiUnavailableException("No Gemini API key is configured. Set GEMINI_API_KEY.");
        }

        // The key goes in a header, never the URL, so it can't end up in request logs.
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(settings.Model)}:generateContent")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Add(ApiKeyHeader, settings.ApiKey);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new GeminiUnavailableException("Gemini could not be reached.", innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout fired. A cancellation by the caller is left to propagate.
            throw new GeminiUnavailableException($"Gemini did not answer within {settings.TimeoutSeconds} seconds.", innerException: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, cancellationToken);
                var reason = error is null ? "" : $" {error.Status}: {error.Message}";
                throw new GeminiUnavailableException($"Gemini returned {(int)response.StatusCode}.{reason}", response.StatusCode);
            }

            GenerateContentResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(JsonOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                // ReadFromJsonAsync doesn't check the content type, so an HTML page from a proxy with a 200 status lands here too.
                throw new GeminiUnavailableException("Gemini returned a response that isn't valid JSON.", response.StatusCode, ex);
            }

            if (result is null)
            {
                throw new GeminiUnavailableException("Gemini returned an empty response.", response.StatusCode);
            }

            logger.LogInformation(
                "Gemini {Model} answered in {ElapsedMs} ms using {TotalTokens} tokens (finish reason {FinishReason}, block reason {BlockReason})",
                settings.Model,
                stopwatch.ElapsedMilliseconds,
                result.UsageMetadata?.TotalTokenCount,
                result.Candidates.FirstOrDefault()?.FinishReason,
                result.PromptFeedback?.BlockReason);

            return result;
        }
    }

    private static async Task<GeminiError?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<GeminiErrorResponse>(JsonOptions, cancellationToken);
            return body?.Error;
        }
        catch (JsonException)
        {
            // Not Gemini's usual error JSON, for example an HTML page from a proxy.
            return null;
        }
    }
}
