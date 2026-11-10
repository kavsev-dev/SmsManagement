using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SmsManagement.Tests.TestDoubles;

/// <summary>A request as observed by <see cref="StubHttpMessageHandler"/> (content is read eagerly).</summary>
internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    AuthenticationHeaderValue? Authorization,
    string? ContentType,
    string? Body);

/// <summary>HTTP handler that records requests and replies with a scripted response.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = [];

    public static StubHttpMessageHandler Json(HttpStatusCode statusCode, string json) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));

    public static StubHttpMessageHandler Throws(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Never completes until the request is cancelled.</summary>
    public static StubHttpMessageHandler Hangs() =>
        new(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable.");
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization,
            request.Content?.Headers.ContentType?.MediaType,
            body));

        return await responder(request, cancellationToken);
    }
}
