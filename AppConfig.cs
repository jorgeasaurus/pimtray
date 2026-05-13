using System.Text.Json;

namespace PIMTray;

public sealed class AppConfig
{
    public AzureAdConfig AzureAd { get; set; } = new();
    public PimConfig Pim { get; set; } = new();

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PIMTray");

    public static string ConfigPath =>
        Path.Combine(ConfigDirectory, "appsettings.json");

    public static AppConfig Load()
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigPath))
        {
            var defaults = BuildDefaults();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(ConfigPath);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (cfg is null)
        {
            var defaults = BuildDefaults();
            Save(defaults);
            return defaults;
        }

        return cfg;
    }

    private static void Save(AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(ConfigPath, json);
    }

    private static AppConfig BuildDefaults() => new()
    {
        AzureAd = new AzureAdConfig
        {
            TenantId = "common",
            ClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e",
            RedirectUri = "http://localhost",
            Cloud = "Public"
        },
        Pim = new PimConfig
        {
            DefaultDurationHours = 1,
            DurationOptionsHours = new[] { 1, 2, 4, 8 }
        }
    };
}

public sealed class AzureAdConfig
{
    public string TenantId { get; set; } = "common";
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "http://localhost";
    public string Cloud { get; set; } = "Public";
    public string? GraphResource { get; set; }
    public string? GraphBaseUrl { get; set; }
}

public sealed class PimConfig
{
    public int DefaultDurationHours { get; set; } = 1;
    public int[] DurationOptionsHours { get; set; } = new[] { 1, 2, 4, 8 };
}
