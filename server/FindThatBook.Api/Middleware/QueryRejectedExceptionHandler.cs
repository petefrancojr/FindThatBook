using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Middleware;

/// <summary>
/// Turns <see cref="OpenLibraryQueryRejectedException"/> into a 422 ProblemDetails response that passes
/// Open Library's reasons on to the client, since they say what to change in the search.
/// </summary>
public class QueryRejectedExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<QueryRejectedExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not OpenLibraryQueryRejectedException rejected)
        {
            return false;
        }

        logger.LogInformation("Open Library refused the search: {Reasons}", string.Join("; ", rejected.Reasons));

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Open Library refused this search.",
                Detail = rejected.Reasons.Count > 0
                    ? string.Join(" ", rejected.Reasons.Select(reason => reason.EndsWith('.') ? reason : $"{reason}."))
                    : "Try different search terms.",
            },
        });
    }
}
