using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CliAccountSwitcher.Api.Security;

namespace CliAccountSwitcher.Api.Providers.ClaudeDesktop.Infrastructure;

internal sealed class ClaudeDesktopTokenCacheReader
{
    public const string ClaudeCodeApplicationIdentifier = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    private const string AccountCacheKeyPrefix = "acct:";
    private const string DpapiKeyPrefix = "DPAPI";
    private const string EncryptionPrefix = "v10";
    private const int EncryptionPrefixLength = 3;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private readonly WindowsDataProtectionService _windowsDataProtectionService = new();

    public async Task<ClaudeDesktopTokenCacheSnapshot?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var hasFoundDataDirectory = false;
        ClaudeDesktopTokenCacheSnapshot? emptySnapshot = null;
        Exception? lastException = null;

        foreach (var dataDirectory in ClaudeDesktopDataDirectory.Discover())
        {
            hasFoundDataDirectory = true;
            try
            {
                var snapshot = await ReadFromDataDirectoryAsync(dataDirectory, cancellationToken);
                if (snapshot.Entries.Count > 0) return snapshot;
                emptySnapshot ??= snapshot;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { lastException = exception; }
        }

        if (emptySnapshot is not null) return emptySnapshot;
        if (hasFoundDataDirectory) throw new InvalidDataException("Claude Desktop data could not be read.", lastException);
        return null;
    }

    private async Task<ClaudeDesktopTokenCacheSnapshot> ReadFromDataDirectoryAsync(ClaudeDesktopDataDirectory dataDirectory, CancellationToken cancellationToken)
    {
        var encryptionKey = ReadEncryptionKey(await ReadAllTextSharedAsync(dataDirectory.LocalStateFilePath, cancellationToken));
        var configJson = await ReadAllTextSharedAsync(dataDirectory.ConfigFilePath, cancellationToken);

        using var configDocument = JsonDocument.Parse(configJson);
        var currentAccountUuid = ReadString(configDocument.RootElement, "lastKnownAccountUuid");
        var tokenCacheText = ReadString(configDocument.RootElement, "oauth:tokenCacheV2");
        if (string.IsNullOrWhiteSpace(tokenCacheText)) tokenCacheText = ReadString(configDocument.RootElement, "oauth:tokenCache");
        if (string.IsNullOrWhiteSpace(tokenCacheText)) return new ClaudeDesktopTokenCacheSnapshot { CurrentAccountUuid = currentAccountUuid };

        return new ClaudeDesktopTokenCacheSnapshot
        {
            Entries = ParseEntries(Decrypt(tokenCacheText, encryptionKey)),
            CurrentAccountUuid = currentAccountUuid
        };
    }

    private byte[] ReadEncryptionKey(string localStateJson)
    {
        using var localStateDocument = JsonDocument.Parse(localStateJson);
        var encryptedKeyText = ReadNestedString(localStateDocument.RootElement, "os_crypt", "encrypted_key");
        if (string.IsNullOrWhiteSpace(encryptedKeyText)) throw new InvalidDataException("The Claude Desktop encryption key is missing.");

        var encryptedKey = Convert.FromBase64String(encryptedKeyText);
        if (encryptedKey.Length <= DpapiKeyPrefix.Length || Encoding.ASCII.GetString(encryptedKey, 0, DpapiKeyPrefix.Length) != DpapiKeyPrefix) throw new InvalidDataException("The Claude Desktop encryption key is not DPAPI protected.");
        return _windowsDataProtectionService.Unprotect(encryptedKey[DpapiKeyPrefix.Length..], "");
    }

    private static string Decrypt(string encryptedText, byte[] encryptionKey)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);
        if (encryptedBytes.Length < EncryptionPrefixLength + NonceLength + TagLength || Encoding.ASCII.GetString(encryptedBytes, 0, EncryptionPrefixLength) != EncryptionPrefix) throw new InvalidDataException("The Claude Desktop token cache encryption format is not supported.");

        var nonce = encryptedBytes.AsSpan(EncryptionPrefixLength, NonceLength);
        var tag = encryptedBytes.AsSpan(encryptedBytes.Length - TagLength, TagLength);
        var cipherText = encryptedBytes.AsSpan(EncryptionPrefixLength + NonceLength, encryptedBytes.Length - EncryptionPrefixLength - NonceLength - TagLength);
        var plainText = new byte[cipherText.Length];

        using var aesGcm = new AesGcm(encryptionKey, TagLength);
        aesGcm.Decrypt(nonce, cipherText, tag, plainText);
        return Encoding.UTF8.GetString(plainText);
    }

    private static IReadOnlyList<ClaudeDesktopTokenCacheEntry> ParseEntries(string tokenCacheJson)
    {
        using var tokenCacheDocument = JsonDocument.Parse(tokenCacheJson);
        if (tokenCacheDocument.RootElement.ValueKind != JsonValueKind.Object) return [];

        var entries = new List<ClaudeDesktopTokenCacheEntry>();
        foreach (var tokenCacheProperty in tokenCacheDocument.RootElement.EnumerateObject())
        {
            var parsedCacheKey = ParseCacheKey(tokenCacheProperty.Name);
            if (parsedCacheKey is null || tokenCacheProperty.Value.ValueKind != JsonValueKind.Object) continue;

            var accessToken = ReadString(tokenCacheProperty.Value, "token");
            if (string.IsNullOrWhiteSpace(accessToken)) continue;

            entries.Add(new ClaudeDesktopTokenCacheEntry
            {
                AccountUuid = parsedCacheKey.AccountUuid,
                OrganizationUuid = parsedCacheKey.OrganizationUuid,
                ApplicationIdentifier = parsedCacheKey.ApplicationIdentifier,
                Scopes = parsedCacheKey.Scopes,
                AccessToken = accessToken,
                ExpiresAt = ReadInt64(tokenCacheProperty.Value, "expiresAt"),
                SubscriptionType = ReadString(tokenCacheProperty.Value, "subscriptionType"),
                RateLimitTier = ReadString(tokenCacheProperty.Value, "rateLimitTier")
            });
        }

        return entries;
    }

    private static ParsedCacheKey? ParseCacheKey(string cacheKey)
    {
        if (!cacheKey.StartsWith(AccountCacheKeyPrefix, StringComparison.Ordinal)) return null;

        var hostMarkerIndex = cacheKey.IndexOf(":https://", StringComparison.Ordinal);
        if (hostMarkerIndex <= AccountCacheKeyPrefix.Length) return null;

        var accountAndApplicationText = cacheKey[AccountCacheKeyPrefix.Length..hostMarkerIndex];
        var applicationSeparatorIndex = accountAndApplicationText.IndexOf('|');
        if (applicationSeparatorIndex <= 0) return null;

        var accountUuid = accountAndApplicationText[..applicationSeparatorIndex];
        var applicationAndOrganizationText = accountAndApplicationText[(applicationSeparatorIndex + 1)..];
        var organizationSeparatorIndex = applicationAndOrganizationText.IndexOf(':');
        if (organizationSeparatorIndex <= 0) return null;

        var schemeIndex = cacheKey.IndexOf("://", hostMarkerIndex, StringComparison.Ordinal);
        var scopeSeparatorIndex = schemeIndex < 0 ? -1 : cacheKey.IndexOf(':', schemeIndex + 3);
        var scopeText = scopeSeparatorIndex < 0 ? "" : cacheKey[(scopeSeparatorIndex + 1)..];

        return new ParsedCacheKey
        {
            AccountUuid = accountUuid,
            OrganizationUuid = applicationAndOrganizationText[(organizationSeparatorIndex + 1)..],
            ApplicationIdentifier = applicationAndOrganizationText[..organizationSeparatorIndex],
            Scopes = scopeText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
    }

    private static async Task<string> ReadAllTextSharedAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var streamReader = new StreamReader(fileStream, Encoding.UTF8, true);
        return await streamReader.ReadToEndAsync(cancellationToken);
    }

    private static string ReadNestedString(JsonElement jsonElement, string objectName, string propertyName)
    {
        if (!TryGetProperty(jsonElement, objectName, out var objectElement) || objectElement.ValueKind != JsonValueKind.Object) return "";
        return ReadString(objectElement, propertyName);
    }

    private static string ReadString(JsonElement jsonElement, string propertyName)
    {
        if (!TryGetProperty(jsonElement, propertyName, out var propertyElement)) return "";
        return propertyElement.ValueKind == JsonValueKind.String ? propertyElement.GetString() ?? "" : propertyElement.ToString();
    }

    private static long ReadInt64(JsonElement jsonElement, string propertyName)
    {
        if (!TryGetProperty(jsonElement, propertyName, out var propertyElement)) return -1;
        if (propertyElement.ValueKind == JsonValueKind.Number && propertyElement.TryGetInt64(out var int64Value)) return int64Value;
        return long.TryParse(propertyElement.ToString(), out var parsedValue) ? parsedValue : -1;
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

    private sealed class ParsedCacheKey
    {
        public string AccountUuid { get; set; } = "";

        public string OrganizationUuid { get; set; } = "";

        public string ApplicationIdentifier { get; set; } = "";

        public IReadOnlyList<string> Scopes { get; set; } = [];
    }
}
