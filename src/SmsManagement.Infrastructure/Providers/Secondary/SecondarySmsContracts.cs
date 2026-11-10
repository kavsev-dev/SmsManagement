using System.Text.Json.Serialization;

namespace SmsManagement.Infrastructure.Providers.Secondary;

// Wire contract simulating a Twilio-style Messages API:
//   POST /2010-04-01/Accounts/{AccountSid}/Messages.json
//     Authorization: Basic base64(AccountSid:AuthToken)
//     Content-Type: application/x-www-form-urlencoded    To=+972...&From=...&Body=...
//     201 -> { "sid": "SM...", "status": "queued", "error_code": null, "error_message": null }
//     4xx/5xx -> { "code": 21211, "message": "...", "status": 400 }

internal sealed record SecondaryMessageResource(
    [property: JsonPropertyName("sid")] string? Sid,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("error_code")] int? ErrorCode,
    [property: JsonPropertyName("error_message")] string? ErrorMessage);

internal sealed record SecondaryErrorResponse(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("status")] int? Status);
