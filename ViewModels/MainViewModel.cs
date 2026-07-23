using System.Collections.ObjectModel;
using System.Windows;
using ContestMonitor.Configuration;
using ContestMonitor.Models;
using ContestMonitor.Services;

namespace ContestMonitor.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly MonitorService _monitor;
    private readonly SchedulerService _scheduler;
    private readonly ToastNotifier _notifier;
    private readonly SeenStore _seen;

    private string _statusText = "Idle";
    private string _statusKind = "idle"; // idle | ok | alert | error | working
    private string _lastCheck = "Never";
    private string _nextCheck = "—";
    private bool _hasAlert;
    private int _newCount;
    private bool _isListEmpty = true;
    private string _checkTimesText;

    public MainViewModel(
        AppSettings settings,
        MonitorService monitor,
        SchedulerService scheduler,
        ToastNotifier notifier,
        SeenStore seen)
    {
        _settings = settings;
        _monitor = monitor;
        _scheduler = scheduler;
        _notifier = notifier;
        _seen = seen;
        _checkTimesText = string.Join(", ", settings.CheckTimes);

        CheckNowCommand = new RelayCommand(CheckNowAsync);
        MarkSeenCommand = new RelayCommand(MarkSeen, () => HasAlert);
        SaveTimesCommand = new RelayCommand(SaveTimes);

        _monitor.CheckCompleted += OnCheckCompleted;
        _scheduler.NextRunScheduled += (_, next) => UpdateNextCheck(next);
        _notifier.Acknowledged += (_, _) => Application.Current.Dispatcher.Invoke(ClearAlert);
    }

    // ---- bound state -------------------------------------------------------

    public ObservableCollection<ContestRow> Contests { get; } = new();

    public RelayCommand CheckNowCommand { get; }
    public RelayCommand MarkSeenCommand { get; }
    public RelayCommand SaveTimesCommand { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusKind
    {
        get => _statusKind;
        private set => SetProperty(ref _statusKind, value);
    }

    public string LastCheck
    {
        get => _lastCheck;
        private set => SetProperty(ref _lastCheck, value);
    }

    public string NextCheck
    {
        get => _nextCheck;
        private set => SetProperty(ref _nextCheck, value);
    }

    public bool HasAlert
    {
        get => _hasAlert;
        private set
        {
            if (SetProperty(ref _hasAlert, value))
                MarkSeenCommand.RaiseCanExecuteChanged();
        }
    }

    public int NewCount
    {
        get => _newCount;
        private set => SetProperty(ref _newCount, value);
    }

    public bool IsListEmpty
    {
        get => _isListEmpty;
        private set => SetProperty(ref _isListEmpty, value);
    }

    public string CheckTimesText
    {
        get => _checkTimesText;
        set => SetProperty(ref _checkTimesText, value);
    }

    // ---- lifecycle ---------------------------------------------------------

    public async void Initialize()
    {
        _scheduler.Start();
        UpdateNextCheck(_scheduler.NextRun);

        if (_settings.CheckOnStartup)
            await CheckNowAsync();
    }

    /// <summary>Called when the window is shown/focused — counts as seeing the alert.</summary>
    public void OnWindowActivated()
    {
        if (_notifier.HasPending)
            _notifier.Acknowledge();
    }

    // ---- commands ----------------------------------------------------------

    private async Task CheckNowAsync()
    {
        StatusKind = "working";
        StatusText = "Checking…";
        await _monitor.CheckAsync();
    }

    private void MarkSeen() => _notifier.Acknowledge();

    private void SaveTimes()
    {
        var parsed = CheckTimesText
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (parsed.Length == 0)
            return;

        _settings.CheckTimes = parsed;
        _settings.Save();
        _scheduler.Reschedule();
        UpdateNextCheck(_scheduler.NextRun);
    }

    // ---- event handlers ----------------------------------------------------

    private void OnCheckCompleted(object? sender, CheckResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LastCheck = result.TimestampLocal.ToString("dd MMM yyyy, HH:mm");

            switch (result.Status)
            {
                case CheckStatus.Success:
                    RebuildList(result.AllContests, result.NewContests);
                    NewCount = result.NewContests.Count;
                    HasAlert = true;
                    StatusKind = "alert";
                    StatusText = result.NewContests.Count == 1
                        ? "1 new contest found"
                        : $"{result.NewContests.Count} new contests found";
                    break;

                case CheckStatus.NoNewContests:
                    RebuildList(result.AllContests, result.NewContests);
                    StatusKind = "ok";
                    StatusText = result.AllContests.Count == 0
                        ? "No upcoming contests"
                        : $"Up to date · {result.AllContests.Count} upcoming";
                    break;

                case CheckStatus.Failed:
                    StatusKind = "error";
                    StatusText = $"Check failed · {result.Error}";
                    break;
            }
        });
    }

    private void RebuildList(IReadOnlyList<Contest> all, IReadOnlyList<Contest> fresh)
    {
        var freshKeys = fresh.Select(c => c.Key).ToHashSet();
        Contests.Clear();
        foreach (var c in all)
            Contests.Add(new ContestRow(c, freshKeys.Contains(c.Key)));
        IsListEmpty = Contests.Count == 0;
    }

    private void UpdateNextCheck(DateTime? next) =>
        Application.Current.Dispatcher.Invoke(() =>
            NextCheck = next?.ToString("dd MMM, HH:mm") ?? "—");

    private void ClearAlert()
    {
        HasAlert = false;
        NewCount = 0;
        if (StatusKind == "alert")
        {
            StatusKind = "ok";
            StatusText = Contests.Count == 0 ? "No upcoming contests" : $"Up to date · {Contests.Count} upcoming";
        }

        foreach (var row in Contests)
            row.IsNew = false;
    }

    public void Dispose()
    {
        _monitor.CheckCompleted -= OnCheckCompleted;
    }
}
