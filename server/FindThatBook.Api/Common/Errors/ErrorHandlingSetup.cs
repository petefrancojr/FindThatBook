namespace FindThatBook.Api.Common.Errors;

public static class ErrorHandlingSetup
{
    /// <summary>Returns errors as ProblemDetails, and an unavailable external service as a 502.</summary>
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ExternalServiceExceptionHandler>();
        return services;
    }
}
