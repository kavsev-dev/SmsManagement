namespace SmsManagement.Core.Providers;

/// <summary>Well-known provider keys accepted in the <c>provider</c> field of <c>POST /messages</c>.</summary>
public static class SmsProviderNames
{
    /// <summary>Primary provider backed by the local mock SMS server.</summary>
    public const string Mock = "Mock";

    /// <summary>Alternative provider simulating a Twilio-style REST API.</summary>
    public const string Secondary = "Secondary";
}
