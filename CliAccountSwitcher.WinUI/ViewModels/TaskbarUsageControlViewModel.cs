using CliAccountSwitcher.Api.Providers.Abstractions;
using CliAccountSwitcher.WinUI.Helpers;
using CliAccountSwitcher.WinUI.Managers;
using CliAccountSwitcher.WinUI.Models;
using CliAccountSwitcher.WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.UI.Dispatching;

namespace CliAccountSwitcher.WinUI.ViewModels;

public sealed partial class TaskbarUsageControlViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan s_primaryUsageWindowDuration = TimeSpan.FromHours(5);
    private static readonly TimeSpan s_secondaryUsageWindowDuration = TimeSpan.FromDays(7);

    private readonly AccountServiceManager _accountServiceManager;
    private readonly ApplicationSettings _applicationSettings;
    private readonly LocalizationService _localizationService;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _hasRequestedInitialUsageRefresh;
    private bool _disposed;

    public TaskbarUsageControlViewModel(AccountServiceManager accountServiceManager, ApplicationSettings applicationSettings, LocalizationService localizationService, DispatcherQueue dispatcherQueue)
    {
        _accountServiceManager = accountServiceManager;
        _applicationSettings = applicationSettings;
        _localizationService = localizationService;
        _dispatcherQueue = dispatcherQueue;

        _localizationService.LanguageChanged += OnLocalizationServiceLanguageChanged;
        WeakReferenceMessenger.Default.Register<ValueChangedMessage<ProviderAccountsChangedMessage>>(this, OnProviderAccountsChangedMessageReceived);
        WeakReferenceMessenger.Default.Register<ValueChangedMessage<CliProviderKind>>(this, OnProviderKindChangedMessageReceived);
        ReloadUsage();
    }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool HasPrimaryUsagePercentage { get; set; }

    [ObservableProperty]
    public partial bool IsMonthlyUsage { get; set; }

    public bool IsLegacyUsageVisible => !IsMonthlyUsage;

    public string MonthlyUsageLabelText => _localizationService.GetLocalizedString("TaskbarUsageControl_MonthlyCreditLabel");

    public string MonthlyUsageAmountText { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasSecondaryUsagePercentage { get; set; }

    [ObservableProperty]
    public partial int PrimaryUsageRemainingPercentage { get; set; }

    [ObservableProperty]
    public partial int PrimaryUsagePacemakerPercentage { get; set; }

    [ObservableProperty]
    public partial int PrimaryUsagePacemakerDifferencePercentage { get; set; }

    [ObservableProperty]
    public partial int SecondaryUsageRemainingPercentage { get; set; }

    [ObservableProperty]
    public partial int SecondaryUsagePacemakerPercentage { get; set; }

    [ObservableProperty]
    public partial int SecondaryUsagePacemakerDifferencePercentage { get; set; }

    public string PrimaryUsageRemainingPercentageText => FormatUsagePercentage(HasPrimaryUsagePercentage, PrimaryUsageRemainingPercentage);

    public string PrimaryUsagePacemakerPercentageText => FormatUsageDifferencePercentage(HasPrimaryUsagePercentage, PrimaryUsagePacemakerDifferencePercentage);

    public string SecondaryUsageRemainingPercentageText => FormatUsagePercentage(HasSecondaryUsagePercentage, SecondaryUsageRemainingPercentage);

    public string SecondaryUsagePacemakerPercentageText => FormatUsageDifferencePercentage(HasSecondaryUsagePercentage, SecondaryUsagePacemakerDifferencePercentage);

    public bool IsPrimaryUsageBelowPacemaker => PrimaryUsagePacemakerDifferencePercentage < 0;

    public bool IsSecondaryUsageBelowPacemaker => SecondaryUsagePacemakerDifferencePercentage < 0;

    public int PrimaryUsageRemainingProgressBarZIndex => UsagePacemakerHelper.GetRemainingProgressBarZIndex(PrimaryUsageRemainingPercentage, PrimaryUsagePacemakerPercentage, PrimaryUsagePacemakerDifferencePercentage);

    public int PrimaryUsagePacemakerProgressBarZIndex => UsagePacemakerHelper.GetPacemakerProgressBarZIndex(PrimaryUsageRemainingPercentage, PrimaryUsagePacemakerPercentage, PrimaryUsagePacemakerDifferencePercentage);

    public int SecondaryUsageRemainingProgressBarZIndex => UsagePacemakerHelper.GetRemainingProgressBarZIndex(SecondaryUsageRemainingPercentage, SecondaryUsagePacemakerPercentage, SecondaryUsagePacemakerDifferencePercentage);

    public int SecondaryUsagePacemakerProgressBarZIndex => UsagePacemakerHelper.GetPacemakerProgressBarZIndex(SecondaryUsageRemainingPercentage, SecondaryUsagePacemakerPercentage, SecondaryUsagePacemakerDifferencePercentage);

    public string RefreshButtonToolTipText => _localizationService.GetLocalizedString("TaskbarUsageControl_RefreshButtonToolTip");

    public void ReloadUsage()
    {
        var activeAccount = GetActiveAccount();
        SetUsage(activeAccount);
    }

    public async Task ReloadUsageOrRefreshMissingActiveUsageAsync()
    {
        ReloadUsage();
        if (_hasRequestedInitialUsageRefresh || IsRefreshing || HasPrimaryUsagePercentage && (IsMonthlyUsage || HasSecondaryUsagePercentage)) return;
        if (GetActiveAccount() is null) return;

        _hasRequestedInitialUsageRefresh = true;
        await RefreshActiveAccountAsync();
    }

    private ProviderAccount GetActiveAccount() => _accountServiceManager.GetAccounts(_applicationSettings.SelectedProviderKind).FirstOrDefault(providerAccount => providerAccount.IsActive);

    private void SetUsage(ProviderAccount activeAccount)
    {
        var snapshot = activeAccount?.LastProviderUsageSnapshot;
        IsMonthlyUsage = activeAccount?.MonthlyUsedAmountUsd.HasValue == true && activeAccount.MonthlyLimitAmountUsd.HasValue && snapshot?.Monthly.RemainingPercentage >= 0 && snapshot.FiveHour.RemainingPercentage < 0 && snapshot.SevenDay.RemainingPercentage < 0;
        MonthlyUsageAmountText = IsMonthlyUsage && activeAccount.MonthlyUsedAmountUsd is { } usedAmount && activeAccount.MonthlyLimitAmountUsd is { } limitAmount ? _localizationService.GetFormattedString("TaskbarUsageControl_MonthlyCreditAmountFormat", FormattableString.Invariant($"${usedAmount:0.00}"), FormattableString.Invariant($"${limitAmount:0.00}")) : "";
        SetPrimaryUsage(IsMonthlyUsage ? snapshot.Monthly : snapshot?.FiveHour);
        SetSecondaryUsage(IsMonthlyUsage ? null : snapshot?.SevenDay);
        RefreshComputedProperties();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLocalizationServiceLanguageChanged;
        WeakReferenceMessenger.Default.Unregister<ValueChangedMessage<ProviderAccountsChangedMessage>>(this);
        WeakReferenceMessenger.Default.Unregister<ValueChangedMessage<CliProviderKind>>(this);
    }

    [RelayCommand]
    private async Task RefreshActiveAccountAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        try { await _accountServiceManager.RefreshActiveAccountAsync(_applicationSettings.SelectedProviderKind); }
        catch { }
        finally
        {
            ReloadUsage();
            IsRefreshing = false;
        }
    }

    private void SetPrimaryUsage(ProviderUsageWindow providerUsageWindow)
    {
        if (IsMonthlyUsage)
        {
            HasPrimaryUsagePercentage = providerUsageWindow?.RemainingPercentage >= 0;
            PrimaryUsageRemainingPercentage = HasPrimaryUsagePercentage ? Math.Clamp(providerUsageWindow.RemainingPercentage, 0, 100) : 0;
            PrimaryUsagePacemakerPercentage = 0;
            PrimaryUsagePacemakerDifferencePercentage = 0;
            return;
        }

        var hasUsagePercentage = UsagePacemakerHelper.TryGetUsagePercentages(providerUsageWindow, s_primaryUsageWindowDuration, out var remainingPercentage, out var pacemakerPercentage, out var pacemakerDifferencePercentage);
        HasPrimaryUsagePercentage = hasUsagePercentage;
        PrimaryUsageRemainingPercentage = remainingPercentage;
        PrimaryUsagePacemakerPercentage = pacemakerPercentage;
        PrimaryUsagePacemakerDifferencePercentage = pacemakerDifferencePercentage;
    }

    private void SetSecondaryUsage(ProviderUsageWindow providerUsageWindow)
    {
        var hasUsagePercentage = UsagePacemakerHelper.TryGetUsagePercentages(providerUsageWindow, s_secondaryUsageWindowDuration, out var remainingPercentage, out var pacemakerPercentage, out var pacemakerDifferencePercentage);
        HasSecondaryUsagePercentage = hasUsagePercentage;
        SecondaryUsageRemainingPercentage = remainingPercentage;
        SecondaryUsagePacemakerPercentage = pacemakerPercentage;
        SecondaryUsagePacemakerDifferencePercentage = pacemakerDifferencePercentage;
    }

    private void OnProviderAccountsChangedMessageReceived(object recipient, ValueChangedMessage<ProviderAccountsChangedMessage> valueChangedMessage)
    {
        if (_applicationSettings.SelectedProviderKind != valueChangedMessage.Value.ProviderKind) return;
        QueueReloadUsage();
    }

    private void OnProviderKindChangedMessageReceived(object recipient, ValueChangedMessage<CliProviderKind> valueChangedMessage) => QueueReloadUsage();

    private void OnLocalizationServiceLanguageChanged()
    {
        if (_dispatcherQueue.HasThreadAccess) RefreshLocalizedProperties();
        else _dispatcherQueue.TryEnqueue(RefreshLocalizedProperties);
    }

    private void QueueReloadUsage()
    {
        if (_dispatcherQueue.HasThreadAccess) ReloadUsage();
        else _dispatcherQueue.TryEnqueue(ReloadUsage);
    }

    private void RefreshLocalizedProperties()
    {
        RefreshComputedProperties();
        OnPropertyChanged(nameof(RefreshButtonToolTipText));
    }

    private void RefreshComputedProperties()
    {
        OnPropertyChanged(nameof(IsLegacyUsageVisible));
        OnPropertyChanged(nameof(MonthlyUsageLabelText));
        OnPropertyChanged(nameof(MonthlyUsageAmountText));
        OnPropertyChanged(nameof(PrimaryUsageRemainingPercentageText));
        OnPropertyChanged(nameof(PrimaryUsagePacemakerPercentageText));
        OnPropertyChanged(nameof(SecondaryUsageRemainingPercentageText));
        OnPropertyChanged(nameof(SecondaryUsagePacemakerPercentageText));
        OnPropertyChanged(nameof(IsPrimaryUsageBelowPacemaker));
        OnPropertyChanged(nameof(IsSecondaryUsageBelowPacemaker));
        OnPropertyChanged(nameof(PrimaryUsageRemainingProgressBarZIndex));
        OnPropertyChanged(nameof(PrimaryUsagePacemakerProgressBarZIndex));
        OnPropertyChanged(nameof(SecondaryUsageRemainingProgressBarZIndex));
        OnPropertyChanged(nameof(SecondaryUsagePacemakerProgressBarZIndex));
    }

    private string FormatUsagePercentage(bool hasUsagePercentage, int usagePercentage) => hasUsagePercentage ? _localizationService.GetFormattedString("TaskbarUsageControl_PercentageFormat", Math.Clamp(usagePercentage, 0, 100)) : _localizationService.GetLocalizedString("TaskbarUsageControl_UnknownPercentageText");

    private string FormatUsageDifferencePercentage(bool hasUsagePercentage, int usagePercentage) => hasUsagePercentage ? _localizationService.GetFormattedString("TaskbarUsageControl_PercentageDifferenceFormat", Math.Clamp(usagePercentage, -100, 100)) : _localizationService.GetLocalizedString("TaskbarUsageControl_UnknownPercentageText");

}
