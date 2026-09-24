using System.Text.Json.Serialization;

namespace CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class NeuralwattBalanceApiData
{
    [JsonPropertyName("credits_remaining_usd")]
    public decimal? CreditsRemainingUsd { get; set; }

    [JsonPropertyName("total_credits_usd")]
    public decimal? TotalCreditsUsd { get; set; }
}
