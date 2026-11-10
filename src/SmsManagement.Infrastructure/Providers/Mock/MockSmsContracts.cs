namespace SmsManagement.Infrastructure.Providers.Mock;

// Wire contract of the local mock SMS server (camelCase JSON):
//   POST /api/sms/send   { "to": "...", "body": "..." }
//     200 -> { "messageId": "mock-...", "status": "accepted" }
//     4xx/5xx -> { "error": "..." }
//   GET  /api/sms/{messageId} -> { "messageId": "...", "status": "..." }

internal sealed record MockSendSmsRequest(string To, string Body);

internal sealed record MockSendSmsResponse(string? MessageId, string? Status);

internal sealed record MockErrorResponse(string? Error);
