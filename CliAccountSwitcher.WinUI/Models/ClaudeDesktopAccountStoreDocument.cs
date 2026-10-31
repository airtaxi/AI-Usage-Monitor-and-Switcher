namespace CliAccountSwitcher.WinUI.Models;

public sealed class ClaudeDesktopAccountStoreDocument
{
    public int Version { get; set; } = 1;

    public List<ClaudeDesktopAccount> Accounts { get; set; } = [];

    public string ActiveAccountIdentifier { get; set; } = "";
}
