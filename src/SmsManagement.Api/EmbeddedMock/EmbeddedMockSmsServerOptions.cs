using System.ComponentModel.DataAnnotations;

namespace SmsManagement.Api.EmbeddedMock;

/// <summary>Settings for <see cref="EmbeddedMockSmsServer"/>. Bound from <c>EmbeddedMockSmsServer</c>.</summary>
public sealed class EmbeddedMockSmsServerOptions
{
    public const string SectionName = "EmbeddedMockSmsServer";

    /// <summary>
    /// Starts the simulated SMS provider inside the API process. Enabled only in the Development configuration,
    /// so production and the integration tests never open the mock port.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Address the mock listens on. It must match <c>SmsProviders:Mock:BaseUrl</c> and
    /// <c>SmsProviders:Secondary:BaseUrl</c>. Port 0 picks a free port (used by tests).
    /// </summary>
    [Required]
    [Url]
    public string Url { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Basic-auth password the simulated Secondary API expects. If empty, any non-empty token is accepted.
    /// </summary>
    public string? SecondaryAuthToken { get; set; }

    /// <summary>Simulated processing delay per send.</summary>
    [Range(0, 10_000)]
    public int LatencyMilliseconds { get; set; } = 150;
}
