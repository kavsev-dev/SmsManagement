using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace SmsManagement.Api;

/// <summary>JSON conventions shared by controllers, minimal endpoints and the OpenAPI document.</summary>
internal static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        // Enums travel as names ("Sent"), matching the specification and the OpenAPI schema.
        options.Converters.Add(new JsonStringEnumConverter());

        // Emit Hebrew as-is instead of "א"-style escapes. HTML-sensitive characters (<, >, &, ') are still escaped.
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Hebrew);
    }
}
