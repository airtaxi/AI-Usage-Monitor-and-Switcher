namespace CliAccountSwitcher.Api.Providers.ClaudeDesktop.Models;

public sealed class ClaudeDesktopAuthExpiredException : Exception
{
    public ClaudeDesktopAuthExpiredException(string message) : base(message) { }

    public ClaudeDesktopAuthExpiredException(string message, Exception innerException) : base(message, innerException) { }
}
