namespace CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;

public sealed class NeuralwattUsageApiResponse
{
    public NeuralwattSubscriptionApiData Subscription { get; set; } = new();

    public NeuralwattBalanceApiData Balance { get; set; } = new();
}
