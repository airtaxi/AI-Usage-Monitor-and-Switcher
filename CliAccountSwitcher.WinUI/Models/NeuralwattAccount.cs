using CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace CliAccountSwitcher.WinUI.Models;

public sealed class NeuralwattAccount
{
    public string ApiKey { get; set; } = "";

    public bool IsActive { get; set; }

    public string CustomAlias { get; set; } = "";

    public NeuralwattUsageSnapshot LastNeuralwattUsageSnapshot { get; set; } = new();

    public bool IsTokenExpired { get; set; }

    public DateTimeOffset? LastUsageRefreshTime { get; set; }

    [JsonIgnore]
    public string AccountIdentifier => ComputeAccountIdentifier(ApiKey);

    [JsonIgnore]
    public string PlanType => LastNeuralwattUsageSnapshot?.PlanLevel ?? "";

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(CustomAlias) ? "Neuralwatt" : CustomAlias;

    public void MarkAsExpired() => IsTokenExpired = true;

    public void MarkAsValid() => IsTokenExpired = false;

    public static string ComputeAccountIdentifier(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexStringLower(hashBytes)[..32];
    }
}
