using System.IO;
using System.Text.Json;
using ContestMonitor.Configuration;
using ContestMonitor.Models;

namespace ContestMonitor.Services;

/// <summary>
/// Persists the set of contest keys already seen, mirroring the Python
/// seen_contests.json behaviour. Lives next to the app settings.
/// </summary>
public sealed class SeenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string FilePath => Path.Combine(AppSettings.ConfigDirectory, "seen_contests.json");

    private readonly HashSet<string> _seen;

    private SeenStore(HashSet<string> seen) => _seen = seen;

    public static SeenStore Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var keys = JsonSerializer.Deserialize<List<string>>(json);
                if (keys is not null)
                    return new SeenStore(new HashSet<string>(keys));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt file -> start fresh.
        }

        return new SeenStore(new HashSet<string>());
    }

    public bool IsSeen(Contest contest) => _seen.Contains(contest.Key);

    /// <summary>Returns only the contests that have not been recorded yet.</summary>
    public IReadOnlyList<Contest> FilterNew(IEnumerable<Contest> contests) =>
        contests.Where(c => !_seen.Contains(c.Key)).ToList();

    public void MarkSeen(IEnumerable<Contest> contests)
    {
        foreach (var c in contests)
            _seen.Add(c.Key);
        Save();
    }

    private void Save()
    {
        Directory.CreateDirectory(AppSettings.ConfigDirectory);
        var ordered = _seen.OrderBy(k => k, StringComparer.Ordinal).ToList();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(ordered, JsonOptions));
    }
}
