using CliAccountSwitcher.Api.Providers.Abstractions;
using System.Text.Json.Serialization;

namespace CliAccountSwitcher.WinUI.Models;

public sealed class ClaudeDesktopAccount
{
    public string AccountIdentifier { get; set; } = "";

    public string AccountUuid { get; set; } = "";

    public string OrganizationUuid { get; set; } = "";

    public string OrganizationName { get; set; } = "";

    public string EmailAddress { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string CustomAlias { get; set; } = "";

    public string PlanType { get; set; } = "";

    public bool IsActive { get; set; }

    public bool IsTokenExpired { get; set; }

    public DateTimeOffset? LastUsageRefreshTime { get; set; }

    public ProviderUsageSnapshot LastProviderUsageSnapshot { get; set; } = new();

    [JsonIgnore]
    public string ResolvedDisplayName => string.IsNullOrWhiteSpace(CustomAlias) ? string.IsNullOrWhiteSpace(DisplayName) ? EmailAddress : DisplayName : CustomAlias;

    public void MarkAsExpired() => IsTokenExpired = true;

    public void MarkAsValid() => IsTokenExpired = false;
}
