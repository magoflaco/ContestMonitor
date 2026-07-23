namespace ContestMonitor.Models;

/// <summary>
/// A single contest scraped from the "Upcoming contests" table.
/// </summary>
public sealed class Contest
{
    public string Name { get; init; } = string.Empty;
    public string Start { get; init; } = string.Empty;
    public string RegistrationEnd { get; init; } = string.Empty;

    /// <summary>
    /// Stable identity used to detect whether a contest has already been seen.
    /// Name + start date keeps it unique even if two editions share a name.
    /// </summary>
    public string Key => $"{Name}|{Start}";

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Start) ? Name : $"{Name} ({Start})";
}
