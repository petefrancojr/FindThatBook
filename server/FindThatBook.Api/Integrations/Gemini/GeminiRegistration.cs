using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Integrations.Gemini;

public static class GeminiRegistration
{
    /// <summary>Registers <see cref="IGeminiClient"/> with its HttpClient and the "Gemini" settings.</summary>
    public static IServiceCollection AddGemini(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "Gemini:BaseUrl must be an absolute URL.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "Gemini:Model is required.")
            .Validate(o => o.TimeoutSeconds > 0, "Gemini:TimeoutSeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddHttpClient<IGeminiClient, GeminiClient>((serviceProvider, httpClient) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        return services;
    }
}
