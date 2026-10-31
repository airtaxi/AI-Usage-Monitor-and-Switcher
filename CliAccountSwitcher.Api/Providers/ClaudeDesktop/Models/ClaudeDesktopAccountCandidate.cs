namespace CliAccountSwitcher.Api.Providers.ClaudeDesktop.Models;

public sealed class ClaudeDesktopAccountCandidate
{
    public string AccountIdentifier { get; set; } = "";

    public string AccountUuid { get; set; } = "";

    public string OrganizationUuid { get; set; } = "";

    public string EmailAddress { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string OrganizationName { get; set; } = "";

    public string PlanType { get; set; } = "Unknown";

    public string AccessToken { get; set; } = "";

    public long ExpiresAt { get; set; }

    public string SubscriptionType { get; set; } = "";

    public string RateLimitTier { get; set; } = "";

    public bool IsDesktopCurrentAccount { get; set; }
}
