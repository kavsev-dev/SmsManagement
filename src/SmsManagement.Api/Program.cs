using Microsoft.OpenApi;
using Scalar.AspNetCore;
using SmsManagement.Api;
using SmsManagement.Api.EmbeddedMock;
using SmsManagement.Core;
using SmsManagement.Core.Contracts;
using SmsManagement.Infrastructure;
using SmsManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSmsManagementCore();
builder.Services.AddSmsManagementInfrastructure(builder.Configuration);

// Simulated external SMS provider on its own port (Development only), so `dotnet run` starts the whole system.
builder.Services.AddEmbeddedMockSmsServer(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));

// RFC 7807 problem details for validation errors, 404s and unhandled exceptions.
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "SMS Management API",
            Version = "v1",
            Description = "Send SMS messages to employees through a selectable provider (Mock or Secondary) and track their delivery status.",
        };
        return Task.CompletedTask;
    });

    // SendMessageRequest's properties are nullable in C# only so that missing values reach SendMessageRequestValidator,
    // which reports them as field errors, so document them as plain strings. "to" and "body" are required (null is
    // rejected with 400); "provider" is optional and defaults to Mock.
    options.AddSchemaTransformer((schema, context, _) =>
    {
        if (context.JsonPropertyInfo?.DeclaringType == typeof(SendMessageRequest) && schema.Type is { } type)
        {
            schema.Type = type & ~JsonSchemaType.Null;
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Dashboard: "/" serves wwwroot/index.html directly.
app.UseDefaultFiles();
app.UseStaticFiles();

// OpenAPI document at /openapi/v1.json, rendered by two interactive UIs: Swagger UI at /swagger and Scalar at /scalar.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "SMS Management API v1");
    options.DocumentTitle = "SMS Management API";
});
// Scalar's "Client Libraries" offers ~20 languages by default. Show only the ones relevant here, C# first:
// C# HttpClient (the project's language), JavaScript fetch (what the dashboard uses), then curl for quick
// copy-paste. Scalar's developer toolbar (Configure, Share, Generate SDK, Deploy) and AI assistant are generic
// extras, so they are turned off.
app.MapScalarApiReference(options =>
{
    options.ShowDeveloperTools = DeveloperToolsVisibility.Never;
    options.Agent = new ScalarAgentOptions { Disabled = true };
    options
        .WithTitle("SMS Management API")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithFeaturedClients(
            new(ScalarTarget.CSharp, ScalarClient.HttpClient),
            new(ScalarTarget.JavaScript, ScalarClient.Fetch),
            new(ScalarTarget.Shell, ScalarClient.Curl));
    options.EnabledTargets = [ScalarTarget.CSharp, ScalarTarget.JavaScript, ScalarTarget.Shell];
    options.EnabledClients = [ScalarClient.HttpClient, ScalarClient.Fetch, ScalarClient.Curl];
});

app.MapHealthChecks("/health");
app.MapControllers();

// Migrate and seed before accepting requests.
await app.Services.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
