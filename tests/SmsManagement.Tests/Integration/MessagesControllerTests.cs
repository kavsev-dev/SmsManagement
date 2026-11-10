using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmsManagement.Tests.Integration;

public sealed class MessagesControllerTests(SmsApiFactory factory) : IClassFixture<SmsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_sends_via_mock_and_returns_201_with_location_and_spec_fields()
    {
        var response = await _client.PostAsJsonAsync("/messages",
            new { to = "0501234567", body = "Hello, your schedule has been updated.", provider = "Mock" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        json.GetProperty("to").GetString().Should().Be("0501234567");
        json.GetProperty("body").GetString().Should().Be("Hello, your schedule has been updated.");
        json.GetProperty("status").GetString().Should().Be("Sent");
        json.GetProperty("providerName").GetString().Should().Be("Mock");
        json.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        var id = json.GetProperty("id").GetGuid();
        response.Headers.Location!.AbsolutePath.Should().Be($"/messages/{id}");

        var fetched = await _client.GetAsync(response.Headers.Location);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(fetched)).GetProperty("status").GetString().Should().Be("Sent");
    }

    [Fact]
    public async Task Post_routes_to_the_secondary_provider_when_selected()
    {
        var response = await _client.PostAsJsonAsync("/messages",
            new { to = "0521234567", body = "Via secondary", provider = "secondary" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        json.GetProperty("providerName").GetString().Should().Be("Secondary");
        json.GetProperty("status").GetString().Should().Be("Sent");
        factory.Gateway.Requests.Should().Contain(r =>
            r.RequestUri!.Host == "secondary-provider.test" && r.Body!.Contains("To=%2B972521234567", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Mock")]
    [InlineData("Secondary")]
    public async Task Provider_failure_is_recorded_as_a_failed_message_not_an_http_error(string provider)
    {
        var response = await _client.PostAsJsonAsync("/messages", new { to = "0500000000", body = "Will fail", provider });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        json.GetProperty("status").GetString().Should().Be("Failed");
        json.GetProperty("errorMessage").GetString().Should().Contain(provider);
        json.GetProperty("sentAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Unknown_recipient_is_still_sent()
    {
        var response = await _client.PostAsJsonAsync("/messages", new { to = "+14155550123", body = "Not an employee", provider = "Mock" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        json.GetProperty("status").GetString().Should().Be("Sent");
        json.GetProperty("employeeNames").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Employee_names_are_returned_by_post_get_and_list()
    {
        // 03-968-3400 is a shared office line of two seeded employees.
        var response = await _client.PostAsJsonAsync("/messages", new { to = "03-968-3400", body = "Office line", provider = "Mock" });
        var created = await ReadJsonAsync(response);
        var id = created.GetProperty("id").GetGuid();

        var fetched = await ReadJsonAsync(await _client.GetAsync($"/messages/{id}"));
        var listed = (await ReadListAsync("/messages")).Single(m => m.GetProperty("id").GetGuid() == id);

        foreach (var json in new[] { created, fetched, listed })
        {
            json.GetProperty("employeeNames").EnumerateArray().Select(n => n.GetString())
                .Should().Equal("רחל ביטון", "דוד לוי");
        }
    }

    [Fact]
    public async Task Hebrew_body_round_trips_and_is_not_escaped_in_json()
    {
        const string hebrew = "שלום ישראל, לוח הזמנים שלך עודכן.";

        var response = await _client.PostAsJsonAsync("/messages", new { to = "0501234567", body = hebrew, provider = "Mock" });
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        raw.Should().Contain(hebrew, "Hebrew is emitted as-is, not as \\uXXXX escapes");
    }

    [Fact]
    public async Task Invalid_request_returns_rfc7807_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/messages", new { to = "12345", body = new string('x', 161), provider = "Twilio" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("type").GetString().Should().StartWith("https://tools.ietf.org/html/rfc9110");
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("to", out _).Should().BeTrue();
        errors.TryGetProperty("body", out _).Should().BeTrue();
        errors.TryGetProperty("provider", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Missing_fields_return_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/messages", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("to", "body");
    }

    [Fact]
    public async Task Post_with_only_to_and_body_as_in_the_specification_is_sent_via_mock()
    {
        var response = await _client.PostAsJsonAsync("/messages",
            new { to = "0501234567", body = "Hello, your schedule has been updated." });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        json.GetProperty("providerName").GetString().Should().Be("Mock");
        json.GetProperty("status").GetString().Should().Be("Sent");
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/messages", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_problem()
    {
        var response = await _client.GetAsync($"/messages/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await ReadJsonAsync(response)).GetProperty("title").GetString().Should().Be("Message not found");
    }

    [Fact]
    public async Task List_is_newest_first_and_filters_by_status()
    {
        await _client.PostAsJsonAsync("/messages", new { to = "0501234567", body = "list-sent", provider = "Mock" });
        await _client.PostAsJsonAsync("/messages", new { to = "0500000000", body = "list-failed", provider = "Mock" });

        var all = await ReadListAsync("/messages");
        var sent = await ReadListAsync("/messages?status=Sent");
        var failed = await ReadListAsync("/messages?status=failed");
        var pending = await ReadListAsync("/messages?status=Pending");
        var empty = await ReadListAsync("/messages?status=");

        all.Select(m => m.GetProperty("createdAt").GetDateTimeOffset()).Should().BeInDescendingOrder();
        all.Select(m => m.GetProperty("body").GetString()).Should().Contain(["list-sent", "list-failed"]);
        sent.Should().NotBeEmpty().And.OnlyContain(m => m.GetProperty("status").GetString() == "Sent");
        failed.Should().NotBeEmpty().And.OnlyContain(m => m.GetProperty("status").GetString() == "Failed");
        pending.Should().BeEmpty();
        empty.Should().HaveCount(all.Count);
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("1")]
    [InlineData("Sent,Failed")]
    public async Task List_rejects_unknown_status_values(string status)
    {
        var response = await _client.GetAsync($"/messages?status={Uri.EscapeDataString(status)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("errors").TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Root_serves_the_dashboard()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        html.Should().Contain("id=\"send-form\"").And.Contain("160 / 160 characters remaining");
    }

    [Fact]
    public async Task OpenApi_document_describes_the_messages_endpoints()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var document = await ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paths = document.GetProperty("paths");
        paths.TryGetProperty("/messages", out var messages).Should().BeTrue();
        messages.TryGetProperty("post", out _).Should().BeTrue();
        messages.TryGetProperty("get", out _).Should().BeTrue();
        paths.TryGetProperty("/messages/{id}", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApi_document_shows_send_request_fields_as_strings_with_optional_provider()
    {
        var document = await ReadJsonAsync(await _client.GetAsync("/openapi/v1.json"));
        var schema = document.GetProperty("components").GetProperty("schemas").GetProperty("SendMessageRequest");

        schema.GetProperty("required").EnumerateArray().Select(r => r.GetString())
            .Should().BeEquivalentTo("to", "body");
        foreach (var field in new[] { "to", "body", "provider" })
        {
            // A JSON string, not ["null", "string"]: null is rejected with 400 by the validator.
            schema.GetProperty("properties").GetProperty(field).GetProperty("type").GetString().Should().Be("string");
        }
    }

    [Fact]
    public async Task Scalar_reference_and_health_are_available()
    {
        (await _client.GetAsync("/scalar")).StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await _client.GetAsync("/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    public async Task Swagger_ui_is_served_and_points_at_the_openapi_document(string url)
    {
        var page = await _client.GetAsync(url);
        var config = await _client.GetStringAsync("/swagger/index.js");

        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("swagger-ui");
        config.Should().Contain("/openapi/v1.json");
    }

    [Fact]
    public async Task Unknown_api_path_returns_404_not_the_dashboard()
    {
        var response = await _client.GetAsync("/messages/not-a-guid/extra");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<List<JsonElement>> ReadListAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await ReadJsonAsync(response)).EnumerateArray().ToList();
    }
}
