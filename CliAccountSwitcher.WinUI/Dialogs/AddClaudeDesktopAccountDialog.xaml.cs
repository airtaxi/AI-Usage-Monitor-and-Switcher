using CliAccountSwitcher.Api.Providers.Abstractions;
using CliAccountSwitcher.WinUI.Services;
using CliAccountSwitcher.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System.Threading.Tasks;

namespace CliAccountSwitcher.WinUI.Dialogs;

public sealed partial class AddClaudeDesktopAccountDialog : ContentDialog
{
    private readonly ApplicationThemeService _applicationThemeService = App.Services.GetRequiredService<ApplicationThemeService>();
    private readonly LocalizationService _localizationService = App.Services.GetRequiredService<LocalizationService>();
    private readonly ClaudeDesktopAccountService _claudeDesktopAccountService = App.Services.GetRequiredService<ClaudeDesktopAccountService>();
    private bool _isImportingAccounts;

    public AddClaudeDesktopAccountDialog()
    {
        InitializeComponent();
        _applicationThemeService.ApplyThemeToElement(this);
        _applicationThemeService.ThemeChanged += OnApplicationThemeServiceThemeChanged;
    }

    private async void OnAddClaudeDesktopAccountDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs contentDialogOpenedEventArgs) => await LoadAccountsAsync();

    private async Task LoadAccountsAsync()
    {
        LoadingProgressRing.IsActive = true;
        LoadingProgressRing.Visibility = Visibility.Visible;
        ErrorInfoBar.IsOpen = false;
        EmptyStateTextBlock.Visibility = Visibility.Collapsed;
        ImportButton.IsEnabled = false;

        try
        {
            var claudeDesktopAccountCandidates = await _claudeDesktopAccountService.DiscoverAccountsAsync();
            var claudeDesktopAccountCandidateViewModels = claudeDesktopAccountCandidates.Select(accountCandidate => new ClaudeDesktopAccountCandidateViewModel(accountCandidate)).ToArray();
            AccountsListView.ItemsSource = claudeDesktopAccountCandidateViewModels;
            foreach (var claudeDesktopAccountCandidateViewModel in claudeDesktopAccountCandidateViewModels.Where(candidateViewModel => candidateViewModel.IsDesktopCurrentAccount)) AccountsListView.SelectedItems.Add(claudeDesktopAccountCandidateViewModel);
            if (AccountsListView.SelectedItems.Count == 0 && claudeDesktopAccountCandidateViewModels.Length > 0) AccountsListView.SelectedItems.Add(claudeDesktopAccountCandidateViewModels[0]);

            if (claudeDesktopAccountCandidateViewModels.Length == 0) EmptyStateTextBlock.Visibility = Visibility.Visible;
        }
        catch (ProviderInstallNotFoundException) { EmptyStateTextBlock.Visibility = Visibility.Visible; }
        catch { ShowError(); }
        finally
        {
            LoadingProgressRing.IsActive = false;
            LoadingProgressRing.Visibility = Visibility.Collapsed;
            ImportButton.IsEnabled = AccountsListView.SelectedItems.Count > 0;
        }
    }

    private async void OnImportButtonClicked(object sender, RoutedEventArgs routedEventArguments)
    {
        if (_isImportingAccounts) return;

        var selectedAccountCandidates = AccountsListView.SelectedItems.OfType<ClaudeDesktopAccountCandidateViewModel>().Select(candidateViewModel => candidateViewModel.AccountCandidate).ToArray();
        if (selectedAccountCandidates.Length == 0) return;

        _isImportingAccounts = true;
        ErrorInfoBar.IsOpen = false;
        IsEnabled = false;
        ImportProgressRing.IsActive = true;
        ImportProgressRing.Visibility = Visibility.Visible;

        try
        {
            await _claudeDesktopAccountService.ImportAccountsAsync(selectedAccountCandidates);
            Hide();
        }
        catch { ShowError(); }
        finally
        {
            ImportProgressRing.IsActive = false;
            ImportProgressRing.Visibility = Visibility.Collapsed;
            IsEnabled = true;
            _isImportingAccounts = false;
        }
    }

    private void OnAccountsListViewSelectionChanged(object sender, SelectionChangedEventArgs selectionChangedEventArguments)
    {
        if (_isImportingAccounts) return;
        ImportButton.IsEnabled = AccountsListView.SelectedItems.Count > 0;
    }

    private void ShowError()
    {
        ErrorInfoBar.Title = _localizationService.GetLocalizedString("AddClaudeDesktopAccountDialog_ErrorInfoBar.Title");
        ErrorInfoBar.Message = _localizationService.GetLocalizedString("AddClaudeDesktopAccountDialog_ErrorInfoBar.Message");
        ErrorInfoBar.IsOpen = true;
    }

    private void OnApplicationThemeServiceThemeChanged(ElementTheme theme) => _applicationThemeService.ApplyThemeToElement(this);

    private void OnAddClaudeDesktopAccountDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs contentDialogClosingEventArguments) => _applicationThemeService.ThemeChanged -= OnApplicationThemeServiceThemeChanged;
}
