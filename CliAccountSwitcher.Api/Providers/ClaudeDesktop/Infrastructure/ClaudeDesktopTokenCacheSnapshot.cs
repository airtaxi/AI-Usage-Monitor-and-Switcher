namespace CliAccountSwitcher.Api.Providers.ClaudeDesktop.Infrastructure;

internal sealed class ClaudeDesktopTokenCacheSnapshot
{
    public IReadOnlyList<ClaudeDesktopTokenCacheEntry> Entries { get; set; } = [];

    public string CurrentAccountUuid { get; set; } = "";
}
