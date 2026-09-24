namespace CliAccountSwitcher.Api.Providers.Neuralwatt;

public static class NeuralwattApiConventions
{
    public static Uri ApiBaseUri { get; } = new("https://api.neuralwatt.com");

    public static string QuotaApiPath => "/v1/quota";

    public static string UserAgent => "AI-Usage-Monitor-and-Switcher";
}
