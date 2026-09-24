namespace CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;

public sealed class NeuralwattUsageSnapshot
{
    public string PlanLevel { get; set; } = "";

    public string RawResponseText { get; set; } = "";

    public NeuralwattUsageWindow SubscriptionUsage { get; set; } = new();

    public decimal? RemainingCreditAmountUsd { get; set; }

    public decimal? TotalCreditAmountUsd { get; set; }
}
