using System.Timers;
using ContestMonitor.Configuration;
using ContestMonitor.Models;
using Microsoft.Toolkit.Uwp.Notifications;
using Timer = System.Timers.Timer;

namespace ContestMonitor.Services;

/// <summary>
/// Shows Windows toast ("push") notifications for new contests and re-shows
/// them, up to <see cref="AppSettings.MaxNotificationRepeats"/> times, until
/// the user acknowledges (clicks the toast, opens the window, or marks seen).
/// </summary>
public sealed class ToastNotifier : IDisposable
{
    private const string ActionAcknowledge = "action=ack";

    private readonly AppSettings _settings;
    private readonly object _gate = new();

    private Timer? _repeatTimer;
    private IReadOnlyList<Contest> _pending = Array.Empty<Contest>();
    private int _shownCount;

    /// <summary>Raised when the user acknowledges the notification (toast click or app open).</summary>
    public event EventHandler? Acknowledged;

    public ToastNotifier(AppSettings settings)
    {
        _settings = settings;
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    public bool HasPending
    {
        get { lock (_gate) return _pending.Count > 0; }
    }

    /// <summary>Begin notifying about a fresh batch of contests.</summary>
    public void NotifyNewContests(IReadOnlyList<Contest> contests)
    {
        if (contests.Count == 0)
            return;

        lock (_gate)
        {
            _pending = contests;
            _shownCount = 0;
            StopTimerLocked();

            ShowToastLocked();

            if (_settings.MaxNotificationRepeats > 1 && _settings.RepeatIntervalMinutes > 0)
            {
                _repeatTimer = new Timer(_settings.RepeatIntervalMinutes * 60_000d) { AutoReset = true };
                _repeatTimer.Elapsed += OnRepeatElapsed;
                _repeatTimer.Start();
            }
        }
    }

    /// <summary>Show a one-off toast so the user can confirm notifications work.</summary>
    public void ShowTestNotification()
    {
        new ToastContentBuilder()
            .AddArgument("action", "test")
            .AddText("Contest Monitor")
            .AddText("Notifications are working. You will be alerted when a new contest appears.")
            .Show(toast =>
            {
                toast.Tag = "contest-test";
                toast.Group = "contest-monitor";
            });
    }

    /// <summary>Stop re-notifying; the user has seen the alert.</summary>
    public void Acknowledge()
    {
        bool had;
        lock (_gate)
        {
            had = _pending.Count > 0;
            _pending = Array.Empty<Contest>();
            StopTimerLocked();
            TryClearHistory();
        }

        if (had)
            Acknowledged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRepeatElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_gate)
        {
            if (_pending.Count == 0 || _shownCount >= _settings.MaxNotificationRepeats)
            {
                StopTimerLocked();
                return;
            }

            ShowToastLocked();

            if (_shownCount >= _settings.MaxNotificationRepeats)
                StopTimerLocked();
        }
    }

    private void ShowToastLocked()
    {
        _shownCount++;
        var contests = _pending;
        int remaining = _settings.MaxNotificationRepeats - _shownCount;

        var title = contests.Count == 1
            ? "New contest available"
            : $"{contests.Count} new contests available";

        var body = string.Join(
            Environment.NewLine,
            contests.Take(4).Select(c =>
                string.IsNullOrWhiteSpace(c.Start) ? c.Name : $"{c.Name}  ·  {c.Start}"));

        if (contests.Count > 4)
            body += $"{Environment.NewLine}and {contests.Count - 4} more";

        var builder = new ToastContentBuilder()
            .AddArgument("action", "ack")
            .AddText(title)
            .AddText(body);

        if (_shownCount > 1 && remaining >= 0)
            builder.AddAttributionText($"Reminder {_shownCount} of {_settings.MaxNotificationRepeats}");

        // Button that opens the contests page directly in the browser.
        if (Uri.TryCreate(_settings.ContestsUrl, UriKind.Absolute, out var contestsUri))
        {
            builder.AddButton(new ToastButton()
                .SetContent("View contests")
                .SetProtocolActivation(contestsUri));
        }

        // Reuse a single tag so repeats replace one another instead of stacking.
        builder.Show(toast =>
        {
            toast.Tag = "contest-alert";
            toast.Group = "contest-monitor";
        });
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        // Any interaction with the toast counts as acknowledgement.
        Acknowledge();
    }

    private void StopTimerLocked()
    {
        if (_repeatTimer is null)
            return;
        _repeatTimer.Elapsed -= OnRepeatElapsed;
        _repeatTimer.Stop();
        _repeatTimer.Dispose();
        _repeatTimer = null;
    }

    private static void TryClearHistory()
    {
        try
        {
            ToastNotificationManagerCompat.History.Remove("contest-alert", "contest-monitor");
        }
        catch
        {
            // History API not available in every context; safe to ignore.
        }
    }

    public void Dispose()
    {
        lock (_gate)
            StopTimerLocked();
        ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
    }
}
