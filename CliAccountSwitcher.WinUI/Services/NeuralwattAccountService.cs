using CliAccountSwitcher.Api.Providers.Abstractions;
using CliAccountSwitcher.Api.Providers.Neuralwatt;
using CliAccountSwitcher.Api.Providers.Neuralwatt.Models;
using CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;
using CliAccountSwitcher.WinUI.Helpers;
using CliAccountSwitcher.WinUI.Models;
using System.Text.Json;

namespace CliAccountSwitcher.WinUI.Services;

public sealed class NeuralwattAccountService : AccountServiceBase<NeuralwattAccount>
{
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly NeuralwattUsageClient _neuralwattUsageClient;
    private string _activeAccountIdentifier = "";
    private bool _disposed;

    public NeuralwattAccountService(ApplicationSettingsService applicationSettingsService, ApplicationNotificationService applicationNotificationService)
        : base(applicationSettingsService, applicationNotificationService)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _neuralwattUsageClient = new NeuralwattUsageClient(_httpClient);
    }

    public override CliProviderKind ProviderKind => CliProviderKind.Neuralwatt;

    public override string BackupFileNamePrefix => "neuralwatt-accounts";

    public override bool IsRenameSupported => true;

    public async Task<NeuralwattAccount> AddApiKeyAsync(string apiKey, string customAlias, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("The Neuralwatt API key is required.", nameof(apiKey));

        var neuralwattUsageSnapshot = await _neuralwattUsageClient.GetUsageAsync(apiKey, cancellationToken);
        var neuralwattAccount = new NeuralwattAccount
        {
            ApiKey = apiKey.Trim(),
            CustomAlias = customAlias.Trim(),
            LastNeuralwattUsageSnapshot = neuralwattUsageSnapshot,
            LastUsageRefreshTime = DateTimeOffset.UtcNow
        };
        neuralwattAccount.MarkAsValid();

        UpsertAccountState(neuralwattAccount);
        if (string.IsNullOrWhiteSpace(_activeAccountIdentifier)) _activeAccountIdentifier = neuralwattAccount.AccountIdentifier;
        await SynchronizeActiveStatusesAsync(cancellationToken);
        await SaveAccountStatesAsync(cancellationToken);
        NotifyAccountsChanged();
        return neuralwattAccount;
    }

    public override async Task RenameAccountAsync(string accountIdentifier, string customAlias, CancellationToken cancellationToken = default)
    {
        var neuralwattAccount = FindAccountState(accountIdentifier) ?? throw new InvalidOperationException("The account does not exist.");
        neuralwattAccount.CustomAlias = customAlias.Trim();
        await SaveAccountStatesAsync(cancellationToken);
        NotifyAccountsChanged();
    }

    public override async Task ExportBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath) ?? Constants.BackupsDirectory);
        if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
        var neuralwattAccountStoreDocument = CreateStoreDocumentSnapshot();
        await using var fileStream = File.Create(backupFilePath);
        await JsonSerializer.SerializeAsync(fileStream, neuralwattAccountStoreDocument, CodexAccountJsonSerializerContext.Default.NeuralwattAccountStoreDocument, cancellationToken);
    }

    public override async Task<ProviderAccountBackupImportResult> ImportBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        var hasExistingAccounts = GetAccountStatesSnapshot().Count > 0;
        var providerAccountBackupImportResult = new ProviderAccountBackupImportResult();
        var importedAccountIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var importedAccounts = new List<NeuralwattAccount>();

        NeuralwattAccountStoreDocument storeDocument;
        try
        {
            using var fileStream = File.OpenRead(backupFilePath);
            storeDocument = await JsonSerializer.DeserializeAsync(fileStream, CodexAccountJsonSerializerContext.Default.NeuralwattAccountStoreDocument, cancellationToken) ?? new NeuralwattAccountStoreDocument();
        }
        catch { return new ProviderAccountBackupImportResult { FailureCount = 1 }; }

        foreach (var candidateAccount in storeDocument.Accounts)
        {
            var accountIdentifier = candidateAccount.AccountIdentifier;
            if (string.IsNullOrWhiteSpace(accountIdentifier) || ContainsAccountState(accountIdentifier) || importedAccountIdentifiers.Contains(accountIdentifier))
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
        _httpClient.Dispose();
        base.Dispose();
    }

    protected override ProviderAccount CreateProviderAccount(NeuralwattAccount neuralwattAccount) => new()
    {
        ProviderKind = CliProviderKind.Neuralwatt,
        AccountIdentifier = neuralwattAccount.AccountIdentifier,
        ProviderAccountIdentifier = neuralwattAccount.AccountIdentifier,
        AccountDetailText = BuildApiKeyPreview(neuralwattAccount.ApiKey),
        CustomAlias = neuralwattAccount.CustomAlias,
        DisplayName = neuralwattAccount.DisplayName,
        EmailAddress = "",
        PlanType = neuralwattAccount.PlanType,
        IsActive = neuralwattAccount.IsActive,
        IsTokenExpired = neuralwattAccount.IsTokenExpired,
        LastProviderUsageSnapshot = CreateProviderUsageSnapshot(neuralwattAccount.LastNeuralwattUsageSnapshot),
        RemainingCreditAmountUsd = neuralwattAccount.LastNeuralwattUsageSnapshot?.RemainingCreditAmountUsd,
        TotalCreditAmountUsd = neuralwattAccount.LastNeuralwattUsageSnapshot?.TotalCreditAmountUsd,
        LastUsageRefreshTime = neuralwattAccount.LastUsageRefreshTime
    };

    protected override string GetAccountIdentifier(NeuralwattAccount neuralwattAccount) => neuralwattAccount.AccountIdentifier;

    protected override string GetDisplayName(NeuralwattAccount neuralwattAccount) => neuralwattAccount.DisplayName;

    protected override bool GetIsActive(NeuralwattAccount neuralwattAccount) => neuralwattAccount.IsActive;

    protected override void SetIsActive(NeuralwattAccount neuralwattAccount, bool isActive) => neuralwattAccount.IsActive = isActive;

    protected override bool GetIsTokenExpired(NeuralwattAccount neuralwattAccount) => neuralwattAccount.IsTokenExpired;

    protected override void MarkAccountAsExpired(NeuralwattAccount neuralwattAccount) => neuralwattAccount.MarkAsExpired();

    protected override ProviderUsageSnapshot GetProviderUsageSnapshot(NeuralwattAccount neuralwattAccount) => CreateProviderUsageSnapshot(neuralwattAccount.LastNeuralwattUsageSnapshot);

    protected override DateTimeOffset? GetLastUsageRefreshTime(NeuralwattAccount neuralwattAccount) => neuralwattAccount.LastUsageRefreshTime;

    protected override Task<IReadOnlyList<NeuralwattAccount>> LoadAccountStatesCoreAsync(CancellationToken cancellationToken)
    {
        var storeDocument = LoadStoreDocument();
        _activeAccountIdentifier = storeDocument.ActiveAccountIdentifier ?? "";
        return Task.FromResult<IReadOnlyList<NeuralwattAccount>>(storeDocument.Accounts);
    }

    protected override async Task SaveAccountStatesAsync(CancellationToken cancellationToken)
    {
        await _saveSemaphore.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Constants.UserDataDirectory);
            var neuralwattAccountStoreDocument = CreateStoreDocumentSnapshot();
            await using var fileStream = File.Create(Constants.NeuralwattAccountsFilePath);
            await JsonSerializer.SerializeAsync(fileStream, neuralwattAccountStoreDocument, CodexAccountJsonSerializerContext.Default.NeuralwattAccountStoreDocument, cancellationToken);
        }
        finally { _saveSemaphore.Release(); }
    }

    protected override Task<string> ReadActiveAccountIdentifierAsync(CancellationToken cancellationToken) => Task.FromResult(_activeAccountIdentifier);

    protected override Task<ProviderActivationFollowUp> ActivateAccountCoreAsync(NeuralwattAccount neuralwattAccount, CancellationToken cancellationToken)
    {
        _activeAccountIdentifier = GetAccountIdentifier(neuralwattAccount);
        return Task.FromResult(ProviderActivationFollowUp.None);
    }

    protected override async Task<ProviderUsageSnapshot> RefreshAccountUsageCoreAsync(NeuralwattAccount neuralwattAccount, CancellationToken cancellationToken)
    {
        var neuralwattUsageSnapshot = await _neuralwattUsageClient.GetUsageAsync(neuralwattAccount.ApiKey, cancellationToken);
        neuralwattAccount.LastNeuralwattUsageSnapshot = neuralwattUsageSnapshot;
        neuralwattAccount.LastUsageRefreshTime = DateTimeOffset.UtcNow;
        neuralwattAccount.MarkAsValid();
        return CreateProviderUsageSnapshot(neuralwattUsageSnapshot);
    }

    protected override bool IsAccountExpiredException(Exception exception) => exception is NeuralwattAuthExpiredException;

    private NeuralwattAccountStoreDocument LoadStoreDocument()
    {
        try
        {
            if (!File.Exists(Constants.NeuralwattAccountsFilePath)) return new NeuralwattAccountStoreDocument();
            using var fileStream = File.OpenRead(Constants.NeuralwattAccountsFilePath);
            return JsonSerializer.Deserialize(fileStream, CodexAccountJsonSerializerContext.Default.NeuralwattAccountStoreDocument) ?? new NeuralwattAccountStoreDocument();
        }
        catch { return new NeuralwattAccountStoreDocument(); }
    }

    private NeuralwattAccountStoreDocument CreateStoreDocumentSnapshot() => new()
    {
        Accounts = [..GetAccountStatesSnapshot()],
        ActiveAccountIdentifier = _activeAccountIdentifier ?? ""
    };

    private static ProviderUsageSnapshot CreateProviderUsageSnapshot(NeuralwattUsageSnapshot neuralwattUsageSnapshot)
    {
        var snapshot = neuralwattUsageSnapshot ?? new NeuralwattUsageSnapshot();
        return new ProviderUsageSnapshot
        {
            ProviderKind = CliProviderKind.Neuralwatt,
            PlanType = snapshot.PlanLevel,
            Monthly = CreateProviderUsageWindow(snapshot.SubscriptionUsage)
        };
    }

    private static ProviderUsageWindow CreateProviderUsageWindow(NeuralwattUsageWindow neuralwattUsageWindow)
    {
        var window = neuralwattUsageWindow ?? new NeuralwattUsageWindow();
        return new ProviderUsageWindow
        {
            UsedPercentage = window.UsedPercentage,
            RemainingPercentage = window.RemainingPercentage,
            ResetAfterSeconds = window.ResetAfterSeconds,
            ResetAt = window.ResetAt
        };
    }

    private static string BuildApiKeyPreview(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "";
        return apiKey.Length <= 18 ? apiKey : $"{apiKey[..8]}...{apiKey[^6..]}";
    }
}
