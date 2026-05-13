using Microsoft.Identity.Client;

namespace PIMTray;

public sealed record AzureCloudSettings(
    AzureCloudInstance AuthorityCloud,
    string GraphResource,
    string GraphBaseUrl);

public static class AzureCloudSettingsResolver
{
    public static AzureCloudSettings Resolve(AzureAdConfig cfg)
    {
        var cloud = (cfg.Cloud ?? "Public").Trim();
        var defaults = cloud.ToLowerInvariant() switch
        {
            "public" => new AzureCloudSettings(
                AzureCloudInstance.AzurePublic,
                "https://graph.microsoft.com",
                "https://graph.microsoft.com/v1.0"),
            "usgovernment" or "usgov" or "gcc" or "gcch" or "gcc high" or "gcc-high" => new AzureCloudSettings(
                AzureCloudInstance.AzureUsGovernment,
                "https://graph.microsoft.us",
                "https://graph.microsoft.us/v1.0"),
            "usdod" or "dod" or "usgovernmentdod" or "us-government-dod" => new AzureCloudSettings(
                AzureCloudInstance.AzureUsGovernment,
                "https://dod-graph.microsoft.us",
                "https://dod-graph.microsoft.us/v1.0"),
            "china" => new AzureCloudSettings(
                AzureCloudInstance.AzureChina,
                "https://graph.chinacloudapi.cn",
                "https://graph.chinacloudapi.cn/v1.0"),
            "germany" => new AzureCloudSettings(
                AzureCloudInstance.AzureGermany,
                "https://graph.microsoft.de",
                "https://graph.microsoft.de/v1.0"),
            _ => throw new InvalidOperationException(
                $"Unsupported AzureAd.Cloud '{cfg.Cloud}'. Supported values: Public, USGovernment, USDod, China, Germany.")
        };

        var graphResource = NormalizeUrl(cfg.GraphResource);
        var graphBase = NormalizeUrl(cfg.GraphBaseUrl);

        if (graphResource is null && graphBase is null)
            return defaults;

        if (graphResource is null && graphBase is not null)
            graphResource = TryGetOrigin(graphBase) ?? throw new InvalidOperationException(
                $"AzureAd.GraphBaseUrl '{cfg.GraphBaseUrl}' must be an absolute URL.");

        if (graphResource is not null && graphBase is null)
            graphBase = $"{graphResource}/v1.0";

        return new AzureCloudSettings(defaults.AuthorityCloud, graphResource!, graphBase!);
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().TrimEnd('/');
    }

    private static string? TryGetOrigin(string absoluteUrl)
    {
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
            return null;
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
