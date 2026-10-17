using System.Net;
using FindThatBook.Api.Common.Exceptions;

namespace FindThatBook.Api.Integrations.OpenLibrary.Exceptions;

/// <summary>Open Library couldn't give an answer. Handled as a 502.</summary>
public class OpenLibraryUnavailableException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : ExternalServiceUnavailableException("Open Library", message, statusCode, innerException);
