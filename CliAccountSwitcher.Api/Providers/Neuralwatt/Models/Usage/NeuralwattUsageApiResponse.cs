namespace CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;

public sealed class NeuralwattUsageApiResponse
{
    public NeuralwattSubscriptionApiData Subscription { get; set; } = new();
}
