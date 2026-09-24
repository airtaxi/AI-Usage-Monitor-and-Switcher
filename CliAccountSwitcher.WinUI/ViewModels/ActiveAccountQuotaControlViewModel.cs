using CliAccountSwitcher.WinUI.Helpers;
using CliAccountSwitcher.WinUI.Models;
using CliAccountSwitcher.WinUI.Services;
using CliAccountSwitcher.WinUI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.UI.Dispatching;
using System;
using System.ComponentModel;

namespace CliAccountSwitcher.WinUI.ViewModels;

public sealed partial class ActiveAccountQuotaControlViewModel(LocalizationService localizationService, DispatcherQueue dispatcherQueue) : ObservableObject
{
    private DashboardPageViewModel _dashboardViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManageAccountsButtonColumnSpan))]
    public partial bool ShouldShowRefreshButton { get; set; }

    [ObservableProperty]
    public partial bool ShouldUseMainWindowNavigation { get; set; }

    public DashboardPageViewModel DashboardViewModel
    {
        get => _dashboardViewModel;
        set
        {
            if (ReferenceEquals(_dashboardViewModel, value)) return;
            _dashboardViewModel?.PropertyChanged -= OnDashboardViewModelPropertyChanged;

            SetProperty(ref _dashboardViewModel, value);

            _dashboardViewModel?.PropertyChanged += OnDashboardViewModelPropertyChanged;
            RefreshActiveAccountProperties();
        }
    }

    public int ManageAccountsButtonColumnSpan => ShouldShowRefreshButton ? 1 : 2;

    public bool HasActiveAccount => DashboardViewModel?.HasActiveAccount == true;

    public bool HasNoActiveAccount => DashboardViewModel?.HasNoActiveAccount != false;

    public string ActiveAccountDisplayNameText => DashboardViewModel?.ActiveAccountDisplayNameText ?? "";

    public string ActiveAccountEmailAddressText => DashboardViewModel?.ActiveAccountEmailAddressText ?? "";

    public bool IsActiveAccountEmailAddressVisible => DashboardViewModel?.IsActiveAccountEmailAddressVisible != false;

    public string ActiveAccountPlanText => DashboardViewModel?.ActiveAccountPlanText ?? "";

    public string ActiveAccountPrimaryUsageRemainingText => DashboardViewModel?.ActiveAccountPrimaryUsageRemainingText ?? "";

    public string ActiveAccountPrimaryUsageLabelText => DashboardViewModel?.IsActiveAccountMonthlyUsdUsage == true ? localizationService.GetLocalizedString("DashboardPage_ActiveMonthlyUsdCreditsLabelText") : DashboardViewModel?.IsActiveAccountMonthlyOnlyUsage == true ? localizationService.GetLocalizedString("DashboardPage_ActiveMonthlyUsageLabelTextBlock.Text") : localizationService.GetLocalizedString("DashboardPage_ActivePrimaryUsageLabelTextBlock.Text");

    public bool IsActiveAccountLegacyUsage => DashboardViewModel?.IsActiveAccountMonthlyUsdUsage != true && DashboardViewModel?.IsActiveAccountMonthlyOnlyUsage != true;

    public bool IsActiveAccountMonthlyOnlyUsage => DashboardViewModel?.IsActiveAccountMonthlyOnlyUsage == true;

    public bool IsActiveAccountPrimaryUsagePacemakerVisible => DashboardViewModel?.IsActiveAccountMonthlyUsdUsage != true;

    public int ActiveAccountPrimaryUsageColumnSpan => IsActiveAccountLegacyUsage ? 1 : 2;

    public string ActiveAccountSecondaryUsageRemainingText => DashboardViewModel?.ActiveAccountSecondaryUsageRemainingText ?? "";

    public int ActiveAccountPrimaryUsageRemainingPercentage => DashboardViewModel?.ActiveAccountPrimaryUsageRemainingPercentage ?? 0;

    public int ActiveAccountSecondaryUsageRemainingPercentage => DashboardViewModel?.ActiveAccountSecondaryUsageRemainingPercentage ?? 0;

    public int ActiveAccountPrimaryUsagePacemakerPercentage => DashboardViewModel?.ActiveAccountPrimaryUsagePacemakerPercentage ?? 0;

    public int ActiveAccountSecondaryUsagePacemakerPercentage => DashboardViewModel?.ActiveAccountSecondaryUsagePacemakerPercentage ?? 0;

    public string ActiveAccountPrimaryUsageResetText => DashboardViewModel?.IsActiveAccountMonthlyUsdUsage == true ? DashboardViewModel.ActiveAccountPrimaryUsageDetailText : FormatUsageReset(DashboardViewModel?.ActiveAccountPrimaryUsageResetAt);

    public string ActiveAccountSecondaryUsageResetText => FormatUsageReset(DashboardViewModel?.ActiveAccountSecondaryUsageResetAt);

    public string ActiveAccountLastUsageRefreshText => DashboardViewModel?.ActiveAccountLastUsageRefreshText ?? "";

    public bool IsActiveAccountPrimaryUsageUnderWarningThreshold => DashboardViewModel?.IsActiveAccountPrimaryUsageUnderWarningThreshold == true;

    public bool IsActiveAccountSecondaryUsageUnderWarningThreshold => DashboardViewModel?.IsActiveAccountSecondaryUsageUnderWarningThreshold == true;

    public bool IsActiveAccountPrimaryUsageOverAverageRateLimit => DashboardViewModel?.IsActiveAccountPrimaryUsageOverAverageRateLimit == true;

    public bool IsActiveAccountSecondaryUsageOverAverageRateLimit => DashboardViewModel?.IsActiveAccountSecondaryUsageOverAverageRateLimit == true;

    public bool IsActiveAccountPrimaryUsageAtAverageRateLimit => (IsActiveAccountLegacyUsage || IsActiveAccountMonthlyOnlyUsage) && !IsActiveAccountPrimaryUsageOverAverageRateLimit && (DashboardViewModel?.ActiveAccountPrimaryUsageAverageRateLimitHeadroomPercentage ?? 0) == 0;

    public bool IsActiveAccountSecondaryUsageAtAverageRateLimit => !IsActiveAccountSecondaryUsageOverAverageRateLimit && (DashboardViewModel?.ActiveAccountSecondaryUsageAverageRateLimitHeadroomPercentage ?? 0) == 0;

    public bool HasActiveAccountPrimaryUsageAverageRateLimitHeadroom => (DashboardViewModel?.ActiveAccountPrimaryUsageAverageRateLimitHeadroomPercentage ?? 0) > 0;

    public bool HasActiveAccountSecondaryUsageAverageRateLimitHeadroom => (DashboardViewModel?.ActiveAccountSecondaryUsageAverageRateLimitHeadroomPercentage ?? 0) > 0;

    public bool IsActiveAccountPrimaryUsageBelowPacemaker => (DashboardViewModel?.ActiveAccountPrimaryUsagePacemakerDifferencePercentage ?? 0) < 0;

    public bool IsActiveAccountSecondaryUsageBelowPacemaker => (DashboardViewModel?.ActiveAccountSecondaryUsagePacemakerDifferencePercentage ?? 0) < 0;

    public int ActiveAccountPrimaryUsageRemainingProgressBarZIndex => UsagePacemakerHelper.GetRemainingProgressBarZIndex(ActiveAccountPrimaryUsageRemainingPercentage, DashboardViewModel?.ActiveAccountPrimaryUsagePacemakerPercentage ?? 0, DashboardViewModel?.ActiveAccountPrimaryUsagePacemakerDifferencePercentage ?? 0);

    public int ActiveAccountPrimaryUsagePacemakerProgressBarZIndex => UsagePacemakerHelper.GetPacemakerProgressBarZIndex(ActiveAccountPrimaryUsageRemainingPercentage, DashboardViewModel?.ActiveAccountPrimaryUsagePacemakerPercentage ?? 0, DashboardViewModel?.ActiveAccountPrimaryUsagePacemakerDifferencePercentage ?? 0);

    public int ActiveAccountSecondaryUsageRemainingProgressBarZIndex => UsagePacemakerHelper.GetRemainingProgressBarZIndex(ActiveAccountSecondaryUsageRemainingPercentage, DashboardViewModel?.ActiveAccountSecondaryUsagePacemakerPercentage ?? 0, DashboardViewModel?.ActiveAccountSecondaryUsagePacemakerDifferencePercentage ?? 0);

    public int ActiveAccountSecondaryUsagePacemakerProgressBarZIndex => UsagePacemakerHelper.GetPacemakerProgressBarZIndex(ActiveAccountSecondaryUsageRemainingPercentage, DashboardViewModel?.ActiveAccountSecondaryUsagePacemakerPercentage ?? 0, DashboardViewModel?.ActiveAccountSecondaryUsagePacemakerDifferencePercentage ?? 0);

    public string ActiveAccountPrimaryUsageAverageRateStatusText => FormatUsageAverageRateStatus(DashboardViewModel?.ActiveAccountPrimaryUsageAverageRateLimitExceededPercentage ?? 0, DashboardViewModel?.ActiveAccountPrimaryUsageAverageRateLimitHeadroomPercentage ?? 0);

    public string ActiveAccountSecondaryUsageAverageRateStatusText => FormatUsageAverageRateStatus(DashboardViewModel?.ActiveAccountSecondaryUsageAverageRateLimitExceededPercentage ?? 0, DashboardViewModel?.ActiveAccountSecondaryUsageAverageRateLimitHeadroomPercentage ?? 0);

    public void RefreshTimeSensitiveProperties()
    {
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageResetText));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsageResetText));
    }

    private void OnDashboardViewModelPropertyChanged(object _, PropertyChangedEventArgs __)
    {
        if (dispatcherQueue.HasThreadAccess) RefreshActiveAccountProperties();
        else dispatcherQueue.TryEnqueue(RefreshActiveAccountProperties);
    }

    [RelayCommand]
    private void ManageAccounts()
    {
        if (ShouldUseMainWindowNavigation)
        {
            MainWindow.NavigateToMainPageSection(MainPageNavigationSection.Accounts);
            return;
        }

        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<MainPageNavigationSection>(MainPageNavigationSection.Accounts));
    }

    [RelayCommand]
    private void RefreshActiveAccount() => WeakReferenceMessenger.Default.Send(new ActiveAccountQuotaRefreshRequestedMessage());

    private void RefreshActiveAccountProperties()
    {
        OnPropertyChanged(nameof(HasActiveAccount));
        OnPropertyChanged(nameof(HasNoActiveAccount));
        OnPropertyChanged(nameof(ActiveAccountDisplayNameText));
        OnPropertyChanged(nameof(ActiveAccountEmailAddressText));
        OnPropertyChanged(nameof(IsActiveAccountEmailAddressVisible));
        OnPropertyChanged(nameof(ActiveAccountPlanText));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageRemainingText));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageLabelText));
        OnPropertyChanged(nameof(IsActiveAccountLegacyUsage));
        OnPropertyChanged(nameof(IsActiveAccountMonthlyOnlyUsage));
        OnPropertyChanged(nameof(IsActiveAccountPrimaryUsagePacemakerVisible));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageColumnSpan));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsageRemainingText));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageRemainingPercentage));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsageRemainingPercentage));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageResetText));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsageResetText));
        OnPropertyChanged(nameof(ActiveAccountLastUsageRefreshText));
        OnPropertyChanged(nameof(IsActiveAccountPrimaryUsageUnderWarningThreshold));
        OnPropertyChanged(nameof(IsActiveAccountSecondaryUsageUnderWarningThreshold));
        OnPropertyChanged(nameof(IsActiveAccountPrimaryUsageOverAverageRateLimit));
        OnPropertyChanged(nameof(IsActiveAccountSecondaryUsageOverAverageRateLimit));
        OnPropertyChanged(nameof(IsActiveAccountPrimaryUsageAtAverageRateLimit));
        OnPropertyChanged(nameof(IsActiveAccountSecondaryUsageAtAverageRateLimit));
        OnPropertyChanged(nameof(HasActiveAccountPrimaryUsageAverageRateLimitHeadroom));
        OnPropertyChanged(nameof(HasActiveAccountSecondaryUsageAverageRateLimitHeadroom));
        OnPropertyChanged(nameof(IsActiveAccountPrimaryUsageBelowPacemaker));
        OnPropertyChanged(nameof(IsActiveAccountSecondaryUsageBelowPacemaker));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageRemainingProgressBarZIndex));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsagePacemakerProgressBarZIndex));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsageRemainingProgressBarZIndex));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsagePacemakerProgressBarZIndex));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsageAverageRateStatusText));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsageAverageRateStatusText));
        OnPropertyChanged(nameof(ActiveAccountPrimaryUsagePacemakerPercentage));
        OnPropertyChanged(nameof(ActiveAccountSecondaryUsagePacemakerPercentage));
    }

    private string FormatUsageReset(DateTimeOffset? usageResetTime)
    {
        if (usageResetTime is null) return localizationService.GetLocalizedString("ProviderAccountViewModel_UnknownResetTime");

        var resetAfterSeconds = Math.Max(0, Convert.ToInt64(Math.Ceiling((usageResetTime.Value - DateTimeOffset.UtcNow).TotalSeconds)));
        var resetAfterTimeSpan = TimeSpan.FromSeconds(resetAfterSeconds);
        var wholeDayCount = resetAfterTimeSpan.Days;
        if (wholeDayCount == 1) return localizationService.GetFormattedString("ProviderAccountViewModel_ResetAfterWithSingleDayFormat", resetAfterTimeSpan);
        if (wholeDayCount > 1) return localizationService.GetFormattedString("ProviderAccountViewModel_ResetAfterWithMultipleDaysFormat", wholeDayCount, resetAfterTimeSpan);
        return localizationService.GetFormattedString("ProviderAccountViewModel_ResetAfterFormat", resetAfterTimeSpan);
    }

    private string FormatUsageAverageRateStatus(int exceededPercentage, int headroomPercentage) => exceededPercentage > 0 ? localizationService.GetFormattedString("UsageAverageRateWarningFormat", exceededPercentage) : headroomPercentage > 0 ? localizationService.GetFormattedString("UsageAverageRateHeadroomFormat", headroomPercentage) : localizationService.GetLocalizedString("UsageAverageRateAtLimitText");
}
