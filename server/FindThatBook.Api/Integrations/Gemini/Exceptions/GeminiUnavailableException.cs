using System.Net;
using FindThatBook.Api.Common.Exceptions;

namespace FindThatBook.Api.Integrations.Gemini.Exceptions;

/// <summary>Gemini couldn't give an answer. Handled as a 502.</summary>
public class GeminiUnavailableException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : ExternalServiceUnavailableException("Gemini", message, statusCode, innerException);
