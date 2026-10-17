using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Integrations.OpenLibrary;

public static class OpenLibraryRegistration
{
    /// <summary>Registers <see cref="IOpenLibraryClient"/> with its HttpClient and the "OpenLibrary" settings.</summary>
    public static IServiceCollection AddOpenLibrary(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenLibraryOptions>()
            .Bind(configuration.GetSection(OpenLibraryOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "OpenLibrary:BaseUrl must be an absolute URL.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserAgent), "OpenLibrary:UserAgent is required.")
            .Validate(o => o.TimeoutSeconds > 0, "OpenLibrary:TimeoutSeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddHttpClient<IOpenLibraryClient, OpenLibraryClient>((serviceProvider, httpClient) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<OpenLibraryOptions>>().Value;
            httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", settings.UserAgent);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        return services;
    }
}
