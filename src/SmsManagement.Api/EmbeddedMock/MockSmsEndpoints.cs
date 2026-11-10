using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace SmsManagement.Api.EmbeddedMock;

/// <summary>
/// HTTP endpoints of the simulated external SMS provider, hosted by <see cref="EmbeddedMockSmsServer"/> on its own
/// port. It simulates two provider APIs:
/// <list type="bullet">
///   <item>Mock: <c>POST /api/sms/send</c> (JSON) and <c>GET /api/sms/{messageId}</c>.</item>
///   <item>Secondary (Twilio-style, form body, Basic auth): <c>POST /2010-04-01/Accounts/{AccountSid}/Messages.json</c>
///   and <c>GET /2010-04-01/Accounts/{AccountSid}/Messages/{sid}.json</c>.</item>
/// </list>
/// Test numbers, by the last digits of the recipient:
/// <list type="bullet">
///   <item><c>...0000</c>: Mock returns 503 (carrier outage); Secondary returns 400 "not a valid phone number" (21211).</item>
///   <item><c>...9999</c>: Mock accepts; Secondary returns 201 with status <c>failed</c> (30003).</item>
/// </list>
/// Message bodies are never logged, because they may contain personal data.
/// </summary>
internal static partial class MockSmsEndpoints
{
    public const string LoggerCategory = "SmsManagement.Api.EmbeddedMock.MockSmsProvider";

    public static void MapMockSmsProvider(this IEndpointRouteBuilder endpoints, EmbeddedMockSmsServerOptions options, ILogger log)
    {
        var expectedToken = options.SecondaryAuthToken;
        var latency = TimeSpan.FromMilliseconds(options.LatencyMilliseconds);
        var store = new ConcurrentDictionary<string, StoredMessage>(StringComparer.Ordinal);

        endpoints.MapGet("/", () => Results.Text(
            """
            Mock SMS provider (embedded in the SMS Management API)
              Mock      POST /api/sms/send   {"to":"...","body":"..."}
              Secondary POST /2010-04-01/Accounts/{AccountSid}/Messages.json  (form: To, From, Body; Basic auth)
            Failure test numbers: ...0000 (rejected / outage), ...9999 (Secondary reports status 'failed').
            """));

        endpoints.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

        // ------------------------------------------------------------ Mock provider (JSON)
        endpoints.MapPost("/api/sms/send", async (MockSendRequest? request, CancellationToken ct) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.To) || string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.Json(new { error = "Request must contain non-empty 'to' and 'body'." }, statusCode: 400);
            }

            await Task.Delay(latency, ct);

            if (request.To.EndsWith("0000", StringComparison.Ordinal))
            {
                LogMockOutage(log, request.To);
                return Results.Json(new { error = "Simulated carrier outage for test numbers ending in 0000." }, statusCode: 503);
            }

            var id = $"mock-{Guid.NewGuid():N}";
            store[id] = new StoredMessage(id, request.To, request.Body, "delivered", DateTimeOffset.UtcNow);
            LogMockAccepted(log, id, request.To);
            return Results.Ok(new { messageId = id, status = "accepted" });
        });

        endpoints.MapGet("/api/sms/{messageId}", (string messageId) =>
            store.TryGetValue(messageId, out var m)
                ? Results.Ok(new { messageId = m.Id, status = m.Status, to = m.To })
                : Results.Json(new { error = $"Message '{messageId}' not found." }, statusCode: 404));

        // ------------------------------------------------------------ Secondary provider (Twilio-style)
        endpoints.MapPost("/2010-04-01/Accounts/{accountSid}/Messages.json", async (string accountSid, HttpRequest http, CancellationToken ct) =>
        {
            if (!IsAuthorized(http, accountSid, expectedToken))
            {
                return TwilioError(401, 20003, "Authenticate");
            }

            if (!http.HasFormContentType)
            {
                return TwilioError(400, 20001, "Content-Type must be application/x-www-form-urlencoded.");
            }

            var form = await http.ReadFormAsync(ct);
            string to = form["To"].ToString(), from = form["From"].ToString(), body = form["Body"].ToString();

            if (string.IsNullOrWhiteSpace(to)) return TwilioError(400, 21604, "A 'To' phone number is required.");
            if (string.IsNullOrWhiteSpace(from)) return TwilioError(400, 21603, "A 'From' phone number is required.");
            if (string.IsNullOrWhiteSpace(body)) return TwilioError(400, 21602, "Message body is required.");

            await Task.Delay(latency, ct);

            if (!to.StartsWith('+') || to.EndsWith("0000", StringComparison.Ordinal))
            {
                LogSecondaryRejected(log, to);
                return TwilioError(400, 21211, $"The 'To' number {to} is not a valid phone number.");
            }

            var sid = "SM" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            var undeliverable = to.EndsWith("9999", StringComparison.Ordinal);
            var status = undeliverable ? "failed" : "queued";
            store[sid] = new StoredMessage(sid, to, body, undeliverable ? "failed" : "delivered", DateTimeOffset.UtcNow);
            LogSecondaryAccepted(log, sid, to, status);

            return Results.Json(
                new SecondaryResource(sid, accountSid, to, from, body, status,
                    undeliverable ? 30003 : null,
                    undeliverable ? "Unreachable destination handset" : null,
                    DateTimeOffset.UtcNow.ToString("R")),
                statusCode: 201);
        });

        endpoints.MapGet("/2010-04-01/Accounts/{accountSid}/Messages/{sid}.json", (string accountSid, string sid, HttpRequest http) =>
        {
            if (!IsAuthorized(http, accountSid, expectedToken)) return TwilioError(401, 20003, "Authenticate");
            return store.TryGetValue(sid, out var m)
                ? Results.Ok(new SecondaryResource(m.Id, accountSid, m.To, null, m.Body, m.Status, null, null, m.CreatedAt.ToString("R")))
                : TwilioError(404, 20404, $"The requested resource {http.Path} was not found");
        });
    }

    private static bool IsAuthorized(HttpRequest request, string accountSid, string? expectedToken)
    {
        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header)
            || !string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(header.Parameter))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
        }
        catch (FormatException)
        {
            return false;
        }

        var separator = decoded.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0) return false;

        var user = decoded[..separator];
        var token = decoded[(separator + 1)..];
        return user == accountSid
            && token.Length > 0
            && (string.IsNullOrEmpty(expectedToken) || CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expectedToken)));
    }

    private static IResult TwilioError(int status, int code, string message) =>
        Results.Json(new { code, message, more_info = $"https://example.invalid/docs/errors/{code}", status }, statusCode: status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[Mock] Simulated outage for {To}")]
    private static partial void LogMockOutage(ILogger logger, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "[Mock] Accepted {Id} to {To}")]
    private static partial void LogMockAccepted(ILogger logger, string id, string to);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[Secondary] Rejected invalid recipient {To}")]
    private static partial void LogSecondaryRejected(ILogger logger, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "[Secondary] {Sid} to {To} -> {Status}")]
    private static partial void LogSecondaryAccepted(ILogger logger, string sid, string to, string status);

    private sealed record MockSendRequest(string? To, string? Body);

    private sealed record StoredMessage(string Id, string To, string Body, string Status, DateTimeOffset CreatedAt);

    private sealed record SecondaryResource(
        [property: JsonPropertyName("sid")] string Sid,
        [property: JsonPropertyName("account_sid")] string AccountSid,
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("from")] string? From,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("error_code")] int? ErrorCode,
        [property: JsonPropertyName("error_message")] string? ErrorMessage,
        [property: JsonPropertyName("date_created")] string DateCreated);
}
