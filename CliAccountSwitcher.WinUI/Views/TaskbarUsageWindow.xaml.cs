using CliAccountSwitcher.WinUI.Helpers;
using CliAccountSwitcher.WinUI.Models;
using CliAccountSwitcher.WinUI.Services;
using Deskband11Lib.Core;
using Deskband11Lib.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.Versioning;

namespace CliAccountSwitcher.WinUI.Views;

[SupportedOSPlatform("windows10.0.22000.0")]
public sealed partial class TaskbarUsageWindow : Window
{
    private readonly LocalizationService _localizationService = App.Services.GetRequiredService<LocalizationService>();

    public TaskbarContentHost TaskbarContentHost { get; }

    public TaskbarUsageWindow()
    {
        InitializeComponent();

        RefreshLocalizedText();
        _localizationService.LanguageChanged += RefreshLocalizedText;

        var applicationSettings = App.Services.GetRequiredService<ApplicationSettings>();
        TaskbarContentHost = new TaskbarContentHost(this, (FrameworkElement)Content, new()
        {
            PreferredWidth = TaskbarHelper.PreferredTaskbarContentWidth,
            PreferredMonitorIdentity = applicationSettings.PreferredMonitorIdentity,
            ManualSlotPriority = applicationSettings.ManualSlotPriority
        });
    }

    public async Task PrepareTaskbarContentAsync() => await TaskbarContentHost.AttachWhenLayoutReadyAsync();

    private void RefreshLocalizedText() => Title = _localizationService.GetLocalizedString("AppDisplayName");

    private void OnTaskbarUsageWindowClosed(object sender, WindowEventArgs e)
    {
        _localizationService.LanguageChanged -= RefreshLocalizedText;
        TaskbarContentHost.Dispose();
        TaskbarUsageContent.Dispose();
    }
}
