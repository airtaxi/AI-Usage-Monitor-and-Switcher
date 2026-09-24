using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CliAccountSwitcher.Api.Providers.Neuralwatt.Models;
using CliAccountSwitcher.Api.Providers.Neuralwatt.Models.Usage;
using CliAccountSwitcher.Api.Providers.Serialization;

namespace CliAccountSwitcher.Api.Providers.Neuralwatt;

public sealed class NeuralwattUsageClient(HttpClient httpClient)
{
    public async Task<NeuralwattUsageSnapshot> GetUsageAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("The Neuralwatt API key is required.", nameof(apiKey));

        var requestUri = new Uri(NeuralwattApiConventions.ApiBaseUri, NeuralwattApiConventions.QuotaApiPath);
        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequestMessage.Headers.Add("Accept", "application/json");
        httpRequestMessage.Headers.Add("User-Agent", NeuralwattApiConventions.UserAgent);

        using var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
        if (httpResponseMessage.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new NeuralwattAuthExpiredException("The Neuralwatt API key has been rejected.");

        httpResponseMessage.EnsureSuccessStatusCode();
        var responseText = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        return ParseUsageResponse(responseText);
    }

    private static NeuralwattUsageSnapshot ParseUsageResponse(string responseText)
    {
        var snapshot = new NeuralwattUsageSnapshot { RawResponseText = responseText };

        NeuralwattUsageApiResponse response;
        try { response = JsonSerializer.Deserialize(responseText, ProviderJsonSerializerContext.Default.NeuralwattUsageApiResponse) ?? new NeuralwattUsageApiResponse(); }
        catch { return snapshot; }

        var subscription = response.Subscription ?? new NeuralwattSubscriptionApiData();
        var balance = response.Balance ?? new NeuralwattBalanceApiData();
        snapshot.PlanLevel = FormatPlanLevel(subscription.Plan);
        snapshot.SubscriptionUsage = ParseSubscriptionUsage(subscription);
        snapshot.RemainingCreditAmountUsd = balance.CreditsRemainingUsd;
        snapshot.TotalCreditAmountUsd = balance.TotalCreditsUsd;
        return snapshot;
    }

    private static NeuralwattUsageWindow ParseSubscriptionUsage(NeuralwattSubscriptionApiData subscription)
    {
        if (subscription.KwhIncluded is not decimal kwhIncluded || kwhIncluded <= 0) return new NeuralwattUsageWindow();
        if (subscription.KwhUsed is not decimal kwhUsed) return new NeuralwattUsageWindow();

        var usedAmount = Math.Max(0, kwhUsed);
        var usedPercentage = usedAmount >= kwhIncluded ? 100 : (int)Math.Round(usedAmount / kwhIncluded * 100, MidpointRounding.AwayFromZero);
        var resetAt = subscription.CurrentPeriodEnd;

        return new NeuralwattUsageWindow
        {
            UsedPercentage = usedPercentage,
            RemainingPercentage = 100 - usedPercentage,
            ResetAt = resetAt,
            ResetAfterSeconds = resetAt is null ? -1 : Math.Max(0, (long)(resetAt.Value - DateTimeOffset.UtcNow).TotalSeconds)
        };
    }

    private static string FormatPlanLevel(string plan)
    {
        if (string.IsNullOrWhiteSpace(plan)) return "";
        var trimmedPlan = plan.Trim();
        return char.ToUpperInvariant(trimmedPlan[0]) + trimmedPlan[1..];
    }
}
