using System.Globalization;
using System.Windows.Threading;
using ContestMonitor.Configuration;

namespace ContestMonitor.Services;

/// <summary>
/// Fires the monitor at the configured times of day (twice daily by default).
/// Uses a DispatcherTimer that re-arms for the next scheduled slot after each
/// tick, so it survives clock drift and DST without accumulating error.
/// </summary>
public sealed class SchedulerService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly MonitorService _monitor;
    private readonly DispatcherTimer _timer;

    public event EventHandler<DateTime>? NextRunScheduled;

    public DateTime? NextRun { get; private set; }

    public SchedulerService(AppSettings settings, MonitorService monitor)
    {
        _settings = settings;
        _monitor = monitor;
        _timer = new DispatcherTimer();
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        ArmNext();
    }

    /// <summary>Recompute the schedule after the user changes check times.</summary>
    public void Reschedule() => ArmNext();

    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        try
        {
            await _monitor.CheckAsync().ConfigureAwait(true);
        }
        finally
        {
            ArmNext();
        }
    }

    private void ArmNext()
    {
        _timer.Stop();

        var next = ComputeNextRun(DateTime.Now, _settings.CheckTimes);
        NextRun = next;

        var delay = next - DateTime.Now;
        if (delay < TimeSpan.FromSeconds(1))
            delay = TimeSpan.FromSeconds(1);

        // DispatcherTimer caps internally; clamp to a safe max and re-arm on tick.
        _timer.Interval = delay > TimeSpan.FromHours(24) ? TimeSpan.FromHours(24) : delay;
        _timer.Start();

        NextRunScheduled?.Invoke(this, next);
    }

    /// <summary>Next future occurrence among the daily time slots.</summary>
    public static DateTime ComputeNextRun(DateTime now, IEnumerable<string> checkTimes)
    {
        var slots = ParseTimes(checkTimes);
        if (slots.Count == 0)
            slots.Add(new TimeSpan(9, 0, 0)); // sensible fallback

        DateTime? best = null;
        foreach (var day in new[] { now.Date, now.Date.AddDays(1) })
        {
            foreach (var slot in slots)
            {
                var candidate = day + slot;
                if (candidate > now && (best is null || candidate < best))
                    best = candidate;
            }
        }

        return best ?? now.AddDays(1);
    }

    private static List<TimeSpan> ParseTimes(IEnumerable<string> raw)
    {
        var result = new List<TimeSpan>();
        foreach (var s in raw)
        {
            if (TimeSpan.TryParseExact(s?.Trim(), new[] { @"hh\:mm", @"h\:mm" },
                    CultureInfo.InvariantCulture, out var ts))
            {
                result.Add(ts);
            }
        }
        return result;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
