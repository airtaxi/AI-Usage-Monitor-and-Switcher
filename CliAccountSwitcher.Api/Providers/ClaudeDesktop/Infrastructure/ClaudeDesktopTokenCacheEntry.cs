using CliAccountSwitcher.Api.Providers.ClaudeCode.Authentication;

namespace CliAccountSwitcher.Api.Providers.ClaudeDesktop.Infrastructure;

internal sealed class ClaudeDesktopTokenCacheEntry
{
    public string AccountUuid { get; set; } = "";

    public string OrganizationUuid { get; set; } = "";

    public string ApplicationIdentifier { get; set; } = "";

    public IReadOnlyList<string> Scopes { get; set; } = [];

    public string AccessToken { get; set; } = "";

    public long ExpiresAt { get; set; }

    public string SubscriptionType { get; set; } = "";

    public string RateLimitTier { get; set; } = "";

    public string AccountIdentifier => $"{AccountUuid}|{OrganizationUuid}";

    public bool HasUsageScopes => Scopes.Contains(ClaudeCodeOAuthScopes.Inference, StringComparer.Ordinal) && Scopes.Contains(ClaudeCodeOAuthScopes.Profile, StringComparer.Ordinal);

    public bool IsClaudeCodeApplication => string.Equals(ApplicationIdentifier, ClaudeDesktopTokenCacheReader.ClaudeCodeApplicationIdentifier, StringComparison.OrdinalIgnoreCase);
}
