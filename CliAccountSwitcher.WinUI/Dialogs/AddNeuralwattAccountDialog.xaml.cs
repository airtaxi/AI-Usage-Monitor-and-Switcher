using CliAccountSwitcher.Api.Providers.Neuralwatt.Models;
using CliAccountSwitcher.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CliAccountSwitcher.WinUI.Dialogs;

public sealed partial class AddNeuralwattAccountDialog : ContentDialog
{
    private readonly ApplicationThemeService _applicationThemeService = App.Services.GetRequiredService<ApplicationThemeService>();
    private readonly LocalizationService _localizationService = App.Services.GetRequiredService<LocalizationService>();
    private readonly NeuralwattAccountService _neuralwattAccountService = App.Services.GetRequiredService<NeuralwattAccountService>();
    private bool _isAddingAccount;

    public AddNeuralwattAccountDialog()
    {
        InitializeComponent();
        _applicationThemeService.ApplyThemeToElement(this);
        _applicationThemeService.ThemeChanged += OnApplicationThemeServiceThemeChanged;
    }

    private async void OnAddAccountButtonClicked(object sender, RoutedEventArgs routedEventArguments)
    {
        ErrorInfoBar.IsOpen = false;
        var apiKey = ApiKeyPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ShowError(_localizationService.GetLocalizedString("AddNeuralwattAccountDialog_ErrorInfoBar.Title"), _localizationService.GetLocalizedString("AddNeuralwattAccountDialog_ErrorInfoBar.Message"));
            return;
        }

        ValidationProgressRing.IsActive = true;
        ValidationProgressRing.Visibility = Visibility.Visible;
        IsEnabled = false;

        try
        {
            await _neuralwattAccountService.AddApiKeyAsync(apiKey, AliasTextBox.Text);
            _isAddingAccount = true;
            Hide();
        }
        catch (NeuralwattAuthExpiredException) { ShowError(_localizationService.GetLocalizedString("AddNeuralwattAccountDialog_ErrorInfoBar_InvalidApiKeyTitle"), _localizationService.GetLocalizedString("AddNeuralwattAccountDialog_ErrorInfoBar_InvalidApiKeyMessage")); }
        catch { ShowError(_localizationService.GetLocalizedString("AddNeuralwattAccountDialog_ErrorInfoBar.Title"), _localizationService.GetLocalizedString("AddNeuralwattAccountDialog_ErrorInfoBar.Message")); }
        finally
        {
            if (!_isAddingAccount) IsEnabled = true;
            ValidationProgressRing.IsActive = false;
            ValidationProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowError(string title, string message)
    {
        ErrorInfoBar.Title = title;
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }

    private void OnApplicationThemeServiceThemeChanged(ElementTheme theme) => _applicationThemeService.ApplyThemeToElement(this);

    private void OnAddNeuralwattAccountDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs contentDialogClosingEventArguments) => _applicationThemeService.ThemeChanged -= OnApplicationThemeServiceThemeChanged;
}
