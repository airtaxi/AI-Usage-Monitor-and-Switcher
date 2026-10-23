namespace CliAccountSwitcher.Api.Providers.Codex.Models.Usage;

public sealed class CodexUsageSnapshot
{
    public const long FiveHourWindowSeconds = 18_000;

    public const long SevenDayWindowSeconds = 604_800;

    public string PlanType { get; set; } = "";

    public string EmailAddress { get; set; } = "";

    public int HttpStatusCode { get; set; }

    public string RawResponseText { get; set; } = "";

    public CodexUsageWindow PrimaryWindow { get; set; } = new();

    public CodexUsageWindow SecondaryWindow { get; set; } = new();

    public CodexUsageWindow GetFiveHourWindow() => GetWindowByDuration(FiveHourWindowSeconds, PrimaryWindow);

    public CodexUsageWindow GetSevenDayWindow() => GetWindowByDuration(SevenDayWindowSeconds, SecondaryWindow);

    private CodexUsageWindow GetWindowByDuration(long limitWindowSeconds, CodexUsageWindow legacyPositionalWindow)
    {
        if (PrimaryWindow is { } primaryWindow && primaryWindow.LimitWindowSeconds == limitWindowSeconds) return primaryWindow;
        if (SecondaryWindow is { } secondaryWindow && secondaryWindow.LimitWindowSeconds == limitWindowSeconds) return secondaryWindow;

        // Snapshots cached before the window duration was stored keep the positional mapping until the next refresh.
        if (!HasLimitWindowSeconds(PrimaryWindow) && !HasLimitWindowSeconds(SecondaryWindow)) return legacyPositionalWindow ?? new CodexUsageWindow();
        return new CodexUsageWindow();
    }

    private static bool HasLimitWindowSeconds(CodexUsageWindow codexUsageWindow) => codexUsageWindow is { LimitWindowSeconds: > 0 };
}
