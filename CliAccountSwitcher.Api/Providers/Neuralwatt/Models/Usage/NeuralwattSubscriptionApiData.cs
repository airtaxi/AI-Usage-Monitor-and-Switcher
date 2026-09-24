using System.Text.Json.Serialization;

namespace CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class NeuralwattSubscriptionApiData
{
    [JsonPropertyName("plan")]
    public string Plan { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("billing_interval")]
    public string BillingInterval { get; set; } = "";

    [JsonPropertyName("current_period_start")]
    public DateTimeOffset? CurrentPeriodStart { get; set; }

    [JsonPropertyName("current_period_end")]
    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    [JsonPropertyName("kwh_included")]
    public decimal? KwhIncluded { get; set; }

    [JsonPropertyName("kwh_used")]
    public decimal? KwhUsed { get; set; }

    [JsonPropertyName("kwh_remaining")]
    public decimal? KwhRemaining { get; set; }

    [JsonPropertyName("auto_renew")]
    public bool? AutoRenew { get; set; }

    [JsonPropertyName("in_overage")]
    public bool? InOverage { get; set; }
}
