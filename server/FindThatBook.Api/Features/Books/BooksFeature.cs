using FindThatBook.Api.Features.Books.Search.Services;

namespace FindThatBook.Api.Features.Books;

public static class BooksFeature
{
    /// <summary>Registers the services the Books feature needs.</summary>
    public static IServiceCollection AddBooksFeature(this IServiceCollection services)
    {
        services.AddScoped<IBookSearchService, BookSearchService>();
        services.AddScoped<IQueryReader, GeminiQueryReader>();
        services.AddScoped<ICandidateFinder, OpenLibraryCandidateFinder>();
        services.AddScoped<ICandidateExplainer, GeminiCandidateExplainer>();

        return services;
    }
}
