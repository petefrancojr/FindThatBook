using System.Net;
using System.Text;

namespace FindThatBook.Api.Tests.TestSupport;

/// <summary>
/// Stands in for the network in HttpClient tests. Records every request (with its body read
/// up front, since HttpClient disposes it) and answers with whatever the test supplies.
/// </summary>
public class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static StubHttpMessageHandler Returning(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest
        {
            Method = request.Method,
            Uri = request.RequestUri!,
            Headers = request.Headers,
            Body = body,
        });
        return await respond(request, cancellationToken);
    }
}

public class RecordedRequest
{
    public required HttpMethod Method { get; init; }
    public required Uri Uri { get; init; }
    public required System.Net.Http.Headers.HttpRequestHeaders Headers { get; init; }
    public string? Body { get; init; }
}
