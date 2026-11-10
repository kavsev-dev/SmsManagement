using System.ComponentModel.DataAnnotations;

namespace SmsManagement.Infrastructure.Providers.Secondary;

/// <summary>Settings for <see cref="SecondarySmsProvider"/>. Bound from <c>SmsProviders:Secondary</c>.</summary>
public sealed class SecondarySmsProviderOptions : HttpSmsProviderOptions
{
    public const string SectionName = "SmsProviders:Secondary";

    /// <summary>Account identifier; part of the request path and the Basic-auth user name.</summary>
    [Required]
    public string AccountSid { get; set; } = "AC00000000000000000000000000000001";

    /// <summary>API secret used as the Basic-auth password. Must come from configuration or secrets, never code.</summary>
    [Required]
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Sender ID (E.164 number or alphanumeric sender name).</summary>
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string FromNumber { get; set; } = "+97239683000";
}
