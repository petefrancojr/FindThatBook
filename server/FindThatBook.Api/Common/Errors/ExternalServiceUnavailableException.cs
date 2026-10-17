using System.Net;

namespace FindThatBook.Api.Common.Errors;

/// <summary>
/// An external service we depend on (Gemini, Open Library) couldn't give us an answer:
/// it was unreachable, too slow, rejected our credentials or returned an error status.
/// The API turns this into a 502 Bad Gateway.
/// </summary>
public class ExternalServiceUnavailableException(
    string serviceName,
    string message,
    HttpStatusCode? statusCode = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>A name that is safe to show users, for example "Gemini".</summary>
    public string ServiceName { get; } = serviceName;

    /// <summary>The status the service returned, when it returned one.</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
