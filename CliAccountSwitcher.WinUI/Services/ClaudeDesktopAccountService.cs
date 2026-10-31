using CliAccountSwitcher.Api.Providers.Abstractions;
using CliAccountSwitcher.Api.Providers.ClaudeDesktop;
using CliAccountSwitcher.Api.Providers.ClaudeDesktop.Models;
using CliAccountSwitcher.WinUI.Helpers;
using CliAccountSwitcher.WinUI.Models;
using System.Text.Json;

namespace CliAccountSwitcher.WinUI.Services;

public sealed class ClaudeDesktopAccountService : AccountServiceBase<ClaudeDesktopAccount>
{
    private readonly ClaudeDesktopClient _claudeDesktopClient = new();
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
    private string _activeAccountIdentifier = "";
    private bool _disposed;

    public ClaudeDesktopAccountService(ApplicationSettingsService applicationSettingsService, ApplicationNotificationService applicationNotificationService) : base(applicationSettingsService, applicationNotificationService) { }

    public override CliProviderKind ProviderKind => CliProviderKind.ClaudeDesktop;

    public override string BackupFileNamePrefix => "claude-desktop-accounts";

    public override bool IsRenameSupported => true;

    public async Task<IReadOnlyList<ClaudeDesktopAccountCandidate>> DiscoverAccountsAsync(CancellationToken cancellationToken = default) => await _claudeDesktopClient.GetAccountsAsync(cancellationToken);

    public async Task<int> ImportAccountsAsync(IReadOnlyList<ClaudeDesktopAccountCandidate> accountCandidates, CancellationToken cancellationToken = default)
    {
        var importedAccounts = accountCandidates.Select(CreateAccount).ToArray();
        foreach (var importedAccount in importedAccounts)
        {
            if (FindAccountState(importedAccount.AccountIdentifier) is { } existingAccount)
            {
                importedAccount.CustomAlias = existingAccount.CustomAlias;
                importedAccount.LastProviderUsageSnapshot = CreateProviderUsageSnapshot(existingAccount.LastProviderUsageSnapshot);
                importedAccount.LastUsageRefreshTime = existingAccount.LastUsageRefreshTime;
            }

            UpsertAccountState(importedAccount);
        }

        var existingAccounts = GetAccountStatesSnapshot();
        var preferredAccountIdentifier = accountCandidates.FirstOrDefault(accountCandidate => accountCandidate.IsDesktopCurrentAccount)?.AccountIdentifier ?? "";
        if (string.IsNullOrWhiteSpace(_activeAccountIdentifier) || !existingAccounts.Any(account => string.Equals(account.AccountIdentifier, _activeAccountIdentifier, StringComparison.Ordinal)))
        {
            if (string.IsNullOrWhiteSpace(preferredAccountIdentifier)) preferredAccountIdentifier = importedAccounts.FirstOrDefault()?.AccountIdentifier ?? "";
            if (!string.IsNullOrWhiteSpace(preferredAccountIdentifier)) _activeAccountIdentifier = preferredAccountIdentifier;
        }

        await SynchronizeActiveStatusesAsync(cancellationToken);
        await SaveAccountStatesAsync(cancellationToken);
        NotifyAccountsChanged();
        return importedAccounts.Length;
    }

    public override async Task RenameAccountAsync(string accountIdentifier, string customAlias, CancellationToken cancellationToken = default)
    {
        var claudeDesktopAccount = FindAccountState(accountIdentifier) ?? throw new InvalidOperationException("The account does not exist.");
        claudeDesktopAccount.CustomAlias = customAlias.Trim();
        await SaveAccountStatesAsync(cancellationToken);
        NotifyAccountsChanged();
    }

    public override async Task ExportBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath) ?? Constants.BackupsDirectory);
        if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
        var claudeDesktopAccountStoreDocument = CreateStoreDocumentSnapshot();
        await using var fileStream = File.Create(backupFilePath);
        await JsonSerializer.SerializeAsync(fileStream, claudeDesktopAccountStoreDocument, CodexAccountJsonSerializerContext.Default.ClaudeDesktopAccountStoreDocument, cancellationToken);
    }

    public override async Task<ProviderAccountBackupImportResult> ImportBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        var hasExistingAccounts = GetAccountStatesSnapshot().Count > 0;
        var providerAccountBackupImportResult = new ProviderAccountBackupImportResult();
        var importedAccountIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var importedAccounts = new List<ClaudeDesktopAccount>();

        ClaudeDesktopAccountStoreDocument storeDocument;
        try
        {
            await using var fileStream = File.OpenRead(backupFilePath);
            storeDocument = await JsonSerializer.DeserializeAsync(fileStream, CodexAccountJsonSerializerContext.Default.ClaudeDesktopAccountStoreDocument, cancellationToken) ?? new ClaudeDesktopAccountStoreDocument();
        }
        catch { return new ProviderAccountBackupImportResult { FailureCount = 1 }; }

        if (storeDocument.Version != 1) return new ProviderAccountBackupImportResult { FailureCount = 1 };

        foreach (var candidateAccount in storeDocument.Accounts)
        {
            var accountIdentifier = candidateAccount.AccountIdentifier;
            if (string.IsNullOrWhiteSpace(accountIdentifier))
            {
                providerAccountBackupImportResult.FailureCount++;
                continue;
            }

            if (ContainsAccountState(accountIdentifier) || importedAccountIdentifiers.Contains(accountIdentifier))
            {
                providerAccountBackupImportResult.DuplicateCount++;
                continue;
            }

            UpsertAccountState(candidateAccount);
            importedAccountIdentifiers.Add(accountIdentifier);
            importedAccounts.Add(candidateAccount);
            providerAccountBackupImportResult.SuccessCount++;
        }

        if (providerAccountBackupImportResult.SuccessCount > 0)
        {
            if (!hasExistingAccounts)
            {
                var autoActivateTarget = PickAutoActivateTarget(importedAccounts);
                if (autoActivateTarget is not null) await ActivateAccountCoreAsync(autoActivateTarget, cancellationToken);
            }
            await SynchronizeActiveStatusesAsync(cancellationToken);
            await SaveAccountStatesAsync(cancellationToken);
            NotifyAccountsChanged();
        }

        return providerAccountBackupImportResult;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _saveSemaphore.Dispose();
        _claudeDesktopClient.Dispose();
        base.Dispose();
    }

    protected override ProviderAccount CreateProviderAccount(ClaudeDesktopAccount claudeDesktopAccount) => new()
    {
        ProviderKind = CliProviderKind.ClaudeDesktop,
        AccountIdentifier = claudeDesktopAccount.AccountIdentifier,
        ProviderAccountIdentifier = claudeDesktopAccount.AccountUuid,
        AccountDetailText = claudeDesktopAccount.OrganizationName,
        CustomAlias = claudeDesktopAccount.CustomAlias,
        DisplayName = GetDisplayName(claudeDesktopAccount),
        EmailAddress = claudeDesktopAccount.EmailAddress,
        PlanType = claudeDesktopAccount.PlanType,
        IsActive = claudeDesktopAccount.IsActive,
        IsTokenExpired = claudeDesktopAccount.IsTokenExpired,
        LastProviderUsageSnapshot = CreateProviderUsageSnapshot(claudeDesktopAccount.LastProviderUsageSnapshot),
        LastUsageRefreshTime = claudeDesktopAccount.LastUsageRefreshTime
    };

    protected override string GetAccountIdentifier(ClaudeDesktopAccount claudeDesktopAccount) => claudeDesktopAccount.AccountIdentifier;

    protected override string GetDisplayName(ClaudeDesktopAccount claudeDesktopAccount) => claudeDesktopAccount.ResolvedDisplayName;

    protected override bool GetIsActive(ClaudeDesktopAccount claudeDesktopAccount) => claudeDesktopAccount.IsActive;

    protected override void SetIsActive(ClaudeDesktopAccount claudeDesktopAccount, bool isActive) => claudeDesktopAccount.IsActive = isActive;

    protected override bool GetIsTokenExpired(ClaudeDesktopAccount claudeDesktopAccount) => claudeDesktopAccount.IsTokenExpired;

    protected override void MarkAccountAsExpired(ClaudeDesktopAccount claudeDesktopAccount) => claudeDesktopAccount.MarkAsExpired();

    protected override ProviderUsageSnapshot GetProviderUsageSnapshot(ClaudeDesktopAccount claudeDesktopAccount) => CreateProviderUsageSnapshot(claudeDesktopAccount.LastProviderUsageSnapshot);

    protected override DateTimeOffset? GetLastUsageRefreshTime(ClaudeDesktopAccount claudeDesktopAccount) => claudeDesktopAccount.LastUsageRefreshTime;

    protected override Task<IReadOnlyList<ClaudeDesktopAccount>> LoadAccountStatesCoreAsync(CancellationToken cancellationToken)
    {
        var storeDocument = LoadStoreDocument();
        _activeAccountIdentifier = storeDocument.ActiveAccountIdentifier ?? "";
        return Task.FromResult<IReadOnlyList<ClaudeDesktopAccount>>(storeDocument.Accounts);
    }

    protected override async Task SaveAccountStatesAsync(CancellationToken cancellationToken)
    {
        await _saveSemaphore.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Constants.UserDataDirectory);
            var claudeDesktopAccountStoreDocument = CreateStoreDocumentSnapshot();
            await using var fileStream = File.Create(Constants.ClaudeDesktopAccountsFilePath);
            await JsonSerializer.SerializeAsync(fileStream, claudeDesktopAccountStoreDocument, CodexAccountJsonSerializerContext.Default.ClaudeDesktopAccountStoreDocument, cancellationToken);
        }
        finally { _saveSemaphore.Release(); }
    }

    protected override Task<string> ReadActiveAccountIdentifierAsync(CancellationToken cancellationToken) => Task.FromResult(_activeAccountIdentifier);

    protected override Task<ProviderActivationFollowUp> ActivateAccountCoreAsync(ClaudeDesktopAccount claudeDesktopAccount, CancellationToken cancellationToken)
    {
        _activeAccountIdentifier = GetAccountIdentifier(claudeDesktopAccount);
        return Task.FromResult(ProviderActivationFollowUp.None);
    }

    protected override async Task<ProviderUsageSnapshot> RefreshAccountUsageCoreAsync(ClaudeDesktopAccount claudeDesktopAccount, CancellationToken cancellationToken)
    {
        try
        {
            var claudeDesktopUsageSnapshot = await _claudeDesktopClient.GetUsageAsync(CreateAccountCandidate(claudeDesktopAccount), cancellationToken);
            claudeDesktopAccount.LastProviderUsageSnapshot = claudeDesktopUsageSnapshot;
            claudeDesktopAccount.LastUsageRefreshTime = DateTimeOffset.UtcNow;
            claudeDesktopAccount.MarkAsValid();
            return claudeDesktopUsageSnapshot;
        }
        catch (ClaudeDesktopAuthExpiredException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return GetProviderUsageSnapshot(claudeDesktopAccount); }
    }

    protected override bool IsAccountExpiredException(Exception exception) => exception is ClaudeDesktopAuthExpiredException;

    private static ClaudeDesktopAccount CreateAccount(ClaudeDesktopAccountCandidate accountCandidate) => new()
    {
        AccountIdentifier = accountCandidate.AccountIdentifier,
        AccountUuid = accountCandidate.AccountUuid,
        OrganizationUuid = accountCandidate.OrganizationUuid,
        OrganizationName = accountCandidate.OrganizationName,
        EmailAddress = accountCandidate.EmailAddress,
        DisplayName = accountCandidate.DisplayName,
        PlanType = accountCandidate.PlanType,
        LastProviderUsageSnapshot = new ProviderUsageSnapshot
        {
            ProviderKind = CliProviderKind.ClaudeDesktop
        }
    };

    private static ClaudeDesktopAccountCandidate CreateAccountCandidate(ClaudeDesktopAccount claudeDesktopAccount) => new()
    {
        AccountIdentifier = claudeDesktopAccount.AccountIdentifier,
        AccountUuid = claudeDesktopAccount.AccountUuid,
        OrganizationUuid = claudeDesktopAccount.OrganizationUuid,
        OrganizationName = claudeDesktopAccount.OrganizationName,
        EmailAddress = claudeDesktopAccount.EmailAddress,
        DisplayName = claudeDesktopAccount.DisplayName,
        PlanType = claudeDesktopAccount.PlanType
    };

    private ClaudeDesktopAccountStoreDocument CreateStoreDocumentSnapshot() => new()
    {
        Accounts = [.. GetAccountStatesSnapshot()],
        ActiveAccountIdentifier = _activeAccountIdentifier ?? ""
    };

    private ClaudeDesktopAccountStoreDocument LoadStoreDocument()
    {
        try
        {
            if (!File.Exists(Constants.ClaudeDesktopAccountsFilePath)) return new ClaudeDesktopAccountStoreDocument();
            using var fileStream = File.OpenRead(Constants.ClaudeDesktopAccountsFilePath);
            return JsonSerializer.Deserialize(fileStream, CodexAccountJsonSerializerContext.Default.ClaudeDesktopAccountStoreDocument) ?? new ClaudeDesktopAccountStoreDocument();
        }
        catch { return new ClaudeDesktopAccountStoreDocument(); }
    }

    private static ProviderUsageSnapshot CreateProviderUsageSnapshot(ProviderUsageSnapshot providerUsageSnapshot)
    {
        if (providerUsageSnapshot is null) return new ProviderUsageSnapshot { ProviderKind = CliProviderKind.ClaudeDesktop };

        return new ProviderUsageSnapshot
        {
            ProviderKind = CliProviderKind.ClaudeDesktop,
            PlanType = providerUsageSnapshot.PlanType,
            EmailAddress = providerUsageSnapshot.EmailAddress,
            RawResponseText = providerUsageSnapshot.RawResponseText,
            FiveHour = CreateProviderUsageWindow(providerUsageSnapshot.FiveHour),
            SevenDay = CreateProviderUsageWindow(providerUsageSnapshot.SevenDay),
            Monthly = CreateProviderUsageWindow(providerUsageSnapshot.Monthly)
        };
    }

    private static ProviderUsageWindow CreateProviderUsageWindow(ProviderUsageWindow providerUsageWindow)
    {
        if (providerUsageWindow is null) return new ProviderUsageWindow();

        return new ProviderUsageWindow
        {
            UsedPercentage = providerUsageWindow.UsedPercentage,
            RemainingPercentage = providerUsageWindow.RemainingPercentage,
            ResetAfterSeconds = providerUsageWindow.ResetAfterSeconds,
            ResetAt = providerUsageWindow.ResetAt
        };
    }
}
