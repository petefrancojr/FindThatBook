namespace FindThatBook.Api.Middleware;

public static class ErrorHandlingSetup
{
    /// <summary>
    /// Returns errors as ProblemDetails: an unavailable external service as a 502, and a search
    /// Open Library refused as a 422 with its reasons.
    /// </summary>
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ExternalServiceExceptionHandler>();
        services.AddExceptionHandler<QueryRejectedExceptionHandler>();
        return services;
    }
}
