using FindThatBook.Api.Integrations.Gemini;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Tests.Integrations.Gemini;

public class GeminiRegistrationTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void BindsTheGeminiSectionAndResolvesTheClient()
    {
        using var provider = BuildProvider(new()
        {
            ["Gemini:ApiKey"] = "abc",
            ["Gemini:Model"] = "gemini-custom",
            ["Gemini:TimeoutSeconds"] = "7",
        });

        var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;
        Assert.Equal("abc", options.ApiKey);
        Assert.Equal("gemini-custom", options.Model);
        Assert.Equal(7, options.TimeoutSeconds);
        Assert.IsType<GeminiClient>(provider.GetRequiredService<IGeminiClient>());
    }

    [Fact]
    public void UsesDefaultsWhenTheSectionIsMissing()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;
        Assert.Equal("https://generativelanguage.googleapis.com/", options.BaseUrl);
        Assert.False(string.IsNullOrWhiteSpace(options.Model));
        Assert.Equal(10, options.TimeoutSeconds);
        Assert.Null(options.ApiKey);
    }

    [Theory]
    [InlineData("Gemini:BaseUrl", "not a url")]
    [InlineData("Gemini:Model", " ")]
    [InlineData("Gemini:TimeoutSeconds", "0")]
    public void RejectsInvalidSettings(string key, string value)
    {
        using var provider = BuildProvider(new() { [key] = value });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GeminiOptions>>().Value);
    }
}
