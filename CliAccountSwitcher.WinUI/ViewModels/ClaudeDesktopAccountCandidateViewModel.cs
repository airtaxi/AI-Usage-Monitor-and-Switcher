using CliAccountSwitcher.Api.Providers.ClaudeDesktop.Models;

namespace CliAccountSwitcher.WinUI.ViewModels;

public sealed class ClaudeDesktopAccountCandidateViewModel(ClaudeDesktopAccountCandidate accountCandidate)
{
    public ClaudeDesktopAccountCandidate AccountCandidate { get; } = accountCandidate;

    public string DisplayName => string.IsNullOrWhiteSpace(AccountCandidate.DisplayName) ? AccountCandidate.EmailAddress : AccountCandidate.DisplayName;

    public string EmailAddress => AccountCandidate.EmailAddress;

    public string OrganizationName => AccountCandidate.OrganizationName;

    public bool IsDesktopCurrentAccount => AccountCandidate.IsDesktopCurrentAccount;
}
