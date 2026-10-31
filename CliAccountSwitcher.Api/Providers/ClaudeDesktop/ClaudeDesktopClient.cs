using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CliAccountSwitcher.Api.Providers.Abstractions;
using CliAccountSwitcher.Api.Providers.ClaudeCode.Authentication;
using CliAccountSwitcher.Api.Providers.ClaudeCode.Models;
using CliAccountSwitcher.Api.Providers.ClaudeDesktop.Infrastructure;
using CliAccountSwitcher.Api.Providers.ClaudeDesktop.Models;

namespace CliAccountSwitcher.Api.Providers.ClaudeDesktop;

public sealed class ClaudeDesktopClient : IDisposable
{
    private const string OAuthBetaHeaderValue = "oauth-2025-04-20";
    private static readonly Uri s_profileAddress = new("https://api.anthropic.com/api/oauth/profile");
    private static readonly Uri s_usageAddress = new("https://api.anthropic.com/api/oauth/usage");
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ClaudeDesktopTokenCacheReader _tokenCacheReader = new();

    public async Task<IReadOnlyList<ClaudeDesktopAccountCandidate>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var tokenCacheSnapshot = await _tokenCacheReader.ReadAsync(cancellationToken) ?? throw new ProviderInstallNotFoundException("Claude Desktop data was not found. Install Claude Desktop and sign in, then try again.");

        var candidates = new List<ClaudeDesktopAccountCandidate>();
        foreach (var tokenCacheEntries in tokenCacheSnapshot.Entries.GroupBy(entry => entry.AccountIdentifier, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var tokenCacheEntry in SelectTokenCacheEntries(tokenCacheEntries))
            {
                try
                {
                    var candidate = await CreateCandidateAsync(tokenCacheEntry, string.Equals(tokenCacheEntry.AccountUuid, tokenCacheSnapshot.CurrentAccountUuid, StringComparison.OrdinalIgnoreCase), cancellationToken);
                    if (candidate is null) continue;

                    candidates.Add(candidate);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { }
            }
        }

        var usageScopedEntryCount = tokenCacheSnapshot.Entries.Count(entry => entry.HasUsageScopes);
        if (usageScopedEntryCount > 0 && candidates.Count == 0) throw new ProviderActionRequiredException("Claude Desktop accounts were found, but their profile could not be read. Sign in to Claude Desktop again.");

        return [..candidates.OrderByDescending(candidate => candidate.IsDesktopCurrentAccount).ThenBy(candidate => candidate.EmailAddress, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<ProviderUsageSnapshot> GetUsageAsync(ClaudeDesktopAccountCandidate accountCandidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountCandidate);

        var accessToken = await ResolveAccessTokenAsync(accountCandidate, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken.Value)) throw new ProviderActionRequiredException("Claude Desktop data is not available. Open Claude Desktop and try again.");
        var usageSnapshot = await TryGetUsageSnapshotAsync(accessToken.Value, accountCandidate.EmailAddress, cancellationToken);
        if (usageSnapshot is not null) return usageSnapshot;

        accessToken = await ResolveAccessTokenAsync(accountCandidate, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken.Value)) throw new ProviderActionRequiredException("Claude Desktop data is not available. Open Claude Desktop and try again.");
        usageSnapshot = await TryGetUsageSnapshotAsync(accessToken.Value, accountCandidate.EmailAddress, cancellationToken);
        if (usageSnapshot is not null) return usageSnapshot;
        if (ClaudeCodeCredentialDocument.ConvertExpiresAtToDateTimeOffset(accessToken.ExpiresAt) is { } expirationTime && expirationTime <= DateTimeOffset.UtcNow) throw new ProviderActionRequiredException("Claude Desktop has not refreshed its token yet. Open Claude Desktop to refresh the token, then refresh the account.");
        throw new ClaudeDesktopAuthExpiredException("Claude Desktop credentials expired. Import the account from Claude Desktop again.");
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<ClaudeDesktopAccessToken> ResolveAccessTokenAsync(ClaudeDesktopAccountCandidate accountCandidate, CancellationToken cancellationToken)
    {
        try
        {
            var tokenCacheSnapshot = await _tokenCacheReader.ReadAsync(cancellationToken);
            var tokenCacheEntry = tokenCacheSnapshot?.Entries
                .Where(entry => string.Equals(entry.AccountIdentifier, accountCandidate.AccountIdentifier, StringComparison.OrdinalIgnoreCase))
                .Where(entry => entry.HasUsageScopes)
                .OrderByDescending(entry => entry.IsClaudeCodeApplication)
                .ThenByDescending(entry => entry.ExpiresAt)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tokenCacheEntry?.AccessToken)) return new ClaudeDesktopAccessToken(tokenCacheEntry.AccessToken, tokenCacheEntry.ExpiresAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { }

        return new ClaudeDesktopAccessToken(accountCandidate.AccessToken, accountCandidate.ExpiresAt);
    }

    private async Task<ProviderUsageSnapshot?> TryGetUsageSnapshotAsync(string accessToken, string emailAddress, CancellationToken cancellationToken)
    {
        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, s_usageAddress);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequestMessage.Headers.TryAddWithoutValidation("anthropic-beta", OAuthBetaHeaderValue);

        using var httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, cancellationToken);
        var responseText = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        if (httpResponseMessage.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        if (!httpResponseMessage.IsSuccessStatusCode) throw new ProviderActionRequiredException($"Claude Desktop usage request failed. Response: {responseText}");
        if (string.IsNullOrWhiteSpace(responseText)) throw new ProviderActionRequiredException("Claude Desktop usage request failed because the response body is empty.");

        return ClaudeCodeUsageResponse.Parse(responseText, emailAddress, CliProviderKind.ClaudeDesktop);
    }

    private static IEnumerable<ClaudeDesktopTokenCacheEntry> SelectTokenCacheEntries(IEnumerable<ClaudeDesktopTokenCacheEntry> tokenCacheEntries) => tokenCacheEntries.Where(entry => entry.HasUsageScopes).OrderByDescending(entry => entry.IsClaudeCodeApplication).ThenByDescending(entry => entry.ExpiresAt);

    private async Task<ClaudeDesktopAccountCandidate?> CreateCandidateAsync(ClaudeDesktopTokenCacheEntry tokenCacheEntry, bool isDesktopCurrentAccount, CancellationToken cancellationToken)
    {
        var accountCandidate = CreateCandidate(tokenCacheEntry, isDesktopCurrentAccount);
        var profile = await ReadProfileAsync(accountCandidate.AccessToken, cancellationToken);
        if (profile is null) return null;

        ApplyProfile(accountCandidate, profile);
        return accountCandidate;
    }

    private async Task<ClaudeDesktopProfile?> ReadProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, s_profileAddress);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequestMessage.Headers.TryAddWithoutValidation("anthropic-beta", OAuthBetaHeaderValue);

        using var httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, cancellationToken);
        if (!httpResponseMessage.IsSuccessStatusCode) return null;

        var responseText = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(responseText) ? null : ParseProfile(responseText);
    }

    private static ClaudeDesktopProfile? ParseProfile(string responseText)
    {
        using var jsonDocument = JsonDocument.Parse(responseText);
        var rootElement = jsonDocument.RootElement;
        if (!TryGetObject(rootElement, "account", out var accountElement)) return null;

        var hasOrganization = TryGetObject(rootElement, "organization", out var organizationElement);
        return new ClaudeDesktopProfile
        {
            EmailAddress = ReadString(accountElement, "email"),
            DisplayName = ReadFirstNonEmptyString(accountElement, "display_name", "full_name"),
            OrganizationName = hasOrganization ? ReadString(organizationElement, "name") : "",
            OrganizationType = hasOrganization ? ReadString(organizationElement, "organization_type") : "",
            RateLimitTier = hasOrganization ? ReadString(organizationElement, "rate_limit_tier") : ""
        };
    }

    private static void ApplyProfile(ClaudeDesktopAccountCandidate accountCandidate, ClaudeDesktopProfile profile)
    {
        accountCandidate.EmailAddress = profile.EmailAddress;
        accountCandidate.DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.EmailAddress : profile.DisplayName;
        accountCandidate.OrganizationName = profile.OrganizationName;
        if (string.IsNullOrWhiteSpace(accountCandidate.SubscriptionType)) accountCandidate.SubscriptionType = CreateSubscriptionType(profile.OrganizationType);
        if (string.IsNullOrWhiteSpace(accountCandidate.RateLimitTier)) accountCandidate.RateLimitTier = profile.RateLimitTier;
        accountCandidate.PlanType = ClaudeCodeCredentialDocument.CreatePlanType(accountCandidate.SubscriptionType, accountCandidate.RateLimitTier);
    }

    private static ClaudeDesktopAccountCandidate CreateCandidate(ClaudeDesktopTokenCacheEntry tokenCacheEntry, bool isDesktopCurrentAccount) => new()
    {
        AccountIdentifier = tokenCacheEntry.AccountIdentifier,
        AccountUuid = tokenCacheEntry.AccountUuid,
        OrganizationUuid = tokenCacheEntry.OrganizationUuid,
        AccessToken = tokenCacheEntry.AccessToken,
        ExpiresAt = tokenCacheEntry.ExpiresAt,
        SubscriptionType = tokenCacheEntry.SubscriptionType,
        RateLimitTier = tokenCacheEntry.RateLimitTier,
        PlanType = ClaudeCodeCredentialDocument.CreatePlanType(tokenCacheEntry.SubscriptionType, tokenCacheEntry.RateLimitTier),
        IsDesktopCurrentAccount = isDesktopCurrentAccount
    };

    private static string CreateSubscriptionType(string organizationType)
    {
        var normalizedOrganizationType = organizationType.Trim().ToLowerInvariant();
        if (normalizedOrganizationType.Contains("enterprise", StringComparison.Ordinal)) return "enterprise";
        if (normalizedOrganizationType.Contains("team", StringComparison.Ordinal)) return "team";
        if (normalizedOrganizationType.Contains("max", StringComparison.Ordinal)) return "max";
        if (normalizedOrganizationType.Contains("pro", StringComparison.Ordinal)) return "pro";
        return "";
    }

    private static string ReadFirstNonEmptyString(JsonElement jsonElement, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var propertyValue = ReadString(jsonElement, propertyName);
            if (!string.IsNullOrWhiteSpace(propertyValue)) return propertyValue;
        }

        return "";
    }

    private static string ReadString(JsonElement jsonElement, string propertyName)
    {
        if (!TryGetProperty(jsonElement, propertyName, out var propertyElement)) return "";
        return propertyElement.ValueKind == JsonValueKind.String ? propertyElement.GetString() ?? "" : propertyElement.ToString();
    }

    private static bool TryGetObject(JsonElement jsonElement, string propertyName, out JsonElement objectElement)
    {
        if (TryGetProperty(jsonElement, propertyName, out var propertyElement) && propertyElement.ValueKind == JsonValueKind.Object)
        {
            objectElement = propertyElement;
            return true;
        }

        objectElement = default;
        return false;
    }

    private static bool TryGetProperty(JsonElement jsonElement, string propertyName, out JsonElement propertyElement)
    {
        foreach (var candidateProperty in jsonElement.EnumerateObject())
        {
            if (!string.Equals(candidateProperty.Name, propertyName, StringComparison.OrdinalIgnoreCase)) continue;
            propertyElement = candidateProperty.Value;
            return true;
        }

        propertyElement = default;
        return false;
    }

    private sealed record ClaudeDesktopAccessToken(string Value, long ExpiresAt);

    private sealed class ClaudeDesktopProfile
    {
        public string EmailAddress { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string OrganizationName { get; set; } = "";

        public string OrganizationType { get; set; } = "";

        public string RateLimitTier { get; set; } = "";
    }
}
