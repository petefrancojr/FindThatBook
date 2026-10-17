using FindThatBook.Api.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Middleware;

/// <summary>
/// Turns <see cref="ExternalServiceUnavailableException"/> into a 502 ProblemDetails response.
/// What went wrong is logged, not sent to the client.
/// </summary>
public class ExternalServiceExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ExternalServiceExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ExternalServiceUnavailableException unavailable)
        {
            return false;
        }

        logger.LogWarning(exception, "{Service} is unavailable: {Reason}", unavailable.ServiceName, unavailable.Message);

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "A service this search depends on is unavailable.",
                Detail = $"{unavailable.ServiceName} is unavailable right now. Try again in a moment.",
            },
        });
    }
}
