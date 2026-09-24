using CliAccountSwitcher.WinUI.Dialogs;
using CliAccountSwitcher.WinUI.Models;
using CliAccountSwitcher.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.System;

namespace CliAccountSwitcher.WinUI.Pages;

public sealed partial class AboutPage : Page
{
    private const string ApplicationRepositoryAddress = "https://github.com/airtaxi/AI-Usage-Monitor-and-Switcher";
    private const string CreatorGitHubAddress = "https://github.com/airtaxi";
    private const string InspirationRepositoryAddress = "https://github.com/isxlan0/Codex_AccountSwitch";
    private const string ClaudeSwapRepositoryAddress = "https://github.com/realiti4/claude-swap";

    private static readonly Lazy<LocalizationService> s_localizationServiceLazy = new(() => App.Services.GetRequiredService<LocalizationService>());
    private static LocalizationService s_localizationService => s_localizationServiceLazy.Value;

    private static readonly List<ThirdPartyLicensePackage> s_thirdPartyLicensePackages = CreateThirdPartyLicensePackages();

    public string ApplicationVersionText { get; } = GetCurrentApplicationVersion();

    public string CopyrightText { get; } = "Copyright (c) 2026 AI Usage Monitor & Switcher contributors";

#pragma warning disable CA1822 // Mark members as static => Used in XAML binding, which doesn't support static members
    public List<ThirdPartyLicensePackage> ThirdPartyLicensePackages => s_thirdPartyLicensePackages;
#pragma warning restore CA1822 // Mark members as static => Used in XAML binding, which doesn't support static members

    public AboutPage()
    {
        InitializeComponent();
    }

    private async void OnGitHubRepositoryButtonClicked(object sender, RoutedEventArgs routedEventArguments) => await OpenAddressAsync(ApplicationRepositoryAddress);

    private async void OnCreatorGitHubButtonClicked(object sender, RoutedEventArgs routedEventArguments) => await OpenAddressAsync(CreatorGitHubAddress);

    private async void OnInspirationRepositoryButtonClicked(object sender, RoutedEventArgs routedEventArguments) => await OpenAddressAsync(InspirationRepositoryAddress);

    private async void OnClaudeSwapRepositoryButtonClicked(object sender, RoutedEventArgs routedEventArguments) => await OpenAddressAsync(ClaudeSwapRepositoryAddress);

    private async void OnThirdPartyLicensesButtonClicked(object sender, RoutedEventArgs routedEventArguments)
    {
        var thirdPartyLicensesDialog = new ThirdPartyLicensesDialog(ThirdPartyLicensePackages) { XamlRoot = XamlRoot };

        await thirdPartyLicensesDialog.ShowAsync();
    }

    private static async Task OpenAddressAsync(string address) => await Launcher.LaunchUriAsync(new Uri(address));

    private static List<ThirdPartyLicensePackage> CreateThirdPartyLicensePackages()
    {
        var windowsSoftwareDevelopmentKitLicenseText = s_localizationService.GetLocalizedString("ThirdPartyLicensePackage_WindowsSoftwareDevelopmentKitLicense");
        var windowsAppSoftwareDevelopmentKitLicenseText = s_localizationService.GetLocalizedString("ThirdPartyLicensePackage_WindowsAppSoftwareDevelopmentKitLicense");

        return
        [
            new("CommunityToolkit.Mvvm", "8.4.2", "MIT", "Microsoft", "https://github.com/CommunityToolkit/dotnet"),
            new("CommunityToolkit.WinUI.Converters", "8.2.251219", "MIT", "Microsoft.Toolkit", "https://github.com/CommunityToolkit/Windows"),
            new("Deskband11Lib.WinUI", "1.4.2", "MIT", "airtaxi", "https://github.com/airtaxi/Deskband11Lib"),
            new("DevWinUI", "10.4.1", "MIT", "Mahdi Hosseini", "https://github.com/ghost1372/DevWinUI"),
            new("H.NotifyIcon.WinUI", "2.4.1", "MIT", "havendv", "https://github.com/HavenDV/H.NotifyIcon"),
            new("HtmlAgilityPack", "1.13.0", "MIT", "ZZZ Projects, Simon Mourrier, Jeff Klawiter, Stephan Grell", "http://html-agility-pack.net/"),
            new("Microsoft.Extensions.DependencyInjection", "10.0.12", "MIT", "Microsoft", "https://dot.net/"),
            new("Microsoft.Windows.SDK.BuildTools", "10.0.28000.2705", windowsSoftwareDevelopmentKitLicenseText, "Microsoft", "https://aka.ms/WinSDKProjectURL"),
            new("Microsoft.WindowsAppSDK", "2.5.1", windowsAppSoftwareDevelopmentKitLicenseText, "Microsoft", "https://github.com/microsoft/windowsappsdk"),
            new("WinUIEx", "2.9.3", "MIT", "Morten Nielsen - https://xaml.dev", "https://dotmorten.github.io/WinUIEx")
        ];
    }

    private static string FormatCurrentApplicationVersion(PackageVersion packageVersion) => $"v{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";

    public static string GetCurrentApplicationVersion() => FormatCurrentApplicationVersion(Package.Current.Id.Version);

}
