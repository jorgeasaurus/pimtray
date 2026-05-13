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
        var cloud = string.IsNullOrWhiteSpace(cfg.Cloud) ? "Public" : cfg.Cloud.Trim();
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

        if (graphResource is not null)
            graphResource = ParseGraphResource(graphResource, nameof(cfg.GraphResource));

        if (graphBase is not null)
            graphBase = ParseAbsoluteUrl(graphBase, nameof(cfg.GraphBaseUrl));

        if (graphResource is null && graphBase is not null)
            graphResource = GetOrigin(graphBase);

        if (graphResource is not null && graphBase is null)
            graphBase = $"{graphResource}/v1.0";

        if (graphResource is not null && graphBase is not null && GetOrigin(graphResource) != GetOrigin(graphBase))
            throw new InvalidOperationException(
                $"AzureAd.{nameof(cfg.GraphResource)} and AzureAd.{nameof(cfg.GraphBaseUrl)} must use the same origin.");

        return new AzureCloudSettings(defaults.AuthorityCloud, graphResource!, graphBase!);
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().TrimEnd('/');
    }

    private static string ParseGraphResource(string value, string fieldName)
    {
        var url = ParseAbsoluteUrl(value, fieldName);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"AzureAd.{fieldName} must be an absolute URL.");

        var path = uri.AbsolutePath;
        if (!string.IsNullOrEmpty(path) && path != "/")
            throw new InvalidOperationException(
                $"AzureAd.{fieldName} must be an origin only (for example https://graph.microsoft.com), without path/query/fragment.");

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException(
                $"AzureAd.{fieldName} must not include query or fragment components.");

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static string ParseAbsoluteUrl(string value, string fieldName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"AzureAd.{fieldName} must be an absolute URL.");

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException($"AzureAd.{fieldName} must not include query or fragment components.");

        return value;
    }

    private static string GetOrigin(string absoluteUrl)
    {
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"'{absoluteUrl}' must be an absolute URL.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
