using System.ComponentModel.DataAnnotations;

namespace SmsManagement.Infrastructure.Providers;

/// <summary>Settings shared by HTTP-based SMS providers.</summary>
public abstract class HttpSmsProviderOptions
{
    public const string DefaultBaseUrl = "http://localhost:5000";

    /// <summary>Root URL of the provider API, e.g. <c>http://localhost:5000</c> for the local mock provider.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Per-request timeout. Requests are not retried, to avoid sending the same SMS twice.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>The base URL with a guaranteed trailing slash, so relative request paths resolve under it.</summary>
    public Uri GetBaseAddress() => new(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/", UriKind.Absolute);
}
