using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContestMonitor.Configuration;

/// <summary>
/// User-configurable settings, persisted as JSON under %AppData%\ContestMonitor.
/// A single instance is shared across the app (loaded once, saved on change).
/// </summary>
public sealed class AppSettings
{
    public string ContestsUrl { get; set; } =
        "https://registro.redprogramacioncompetitiva.com/contests";

    /// <summary>Times of day (24h, local) at which the monitor checks the site.</summary>
    public string[] CheckTimes { get; set; } = { "09:00", "21:00" };

    /// <summary>Run a check immediately when the app starts.</summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>Minutes between re-notifications while a new contest stays unseen.</summary>
    public int RepeatIntervalMinutes { get; set; } = 30;

    /// <summary>Total number of times a notification is shown (including the first).</summary>
    public int MaxNotificationRepeats { get; set; } = 4;

    /// <summary>Start the app minimized to the tray/background on launch.</summary>
    public bool StartMinimized { get; set; }

    // ---- persistence -------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    [JsonIgnore]
    public static string ConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ContestMonitor");

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                    return loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable config -> fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
