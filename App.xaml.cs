using System.ComponentModel;
using System.Windows;
using ContestMonitor.Configuration;
using ContestMonitor.Services;
using ContestMonitor.ViewModels;
using ContestMonitor.Views;

namespace ContestMonitor;

/// <summary>
/// Composition root. Wires settings and services together by hand (no DI
/// container needed for an app this size), keeps the app alive in the tray,
/// and shows the main window.
/// </summary>
public partial class App : Application
{
    private AppSettings _settings = null!;
    private ToastNotifier _notifier = null!;
    private SchedulerService _scheduler = null!;
    private MonitorService _monitor = null!;
    private MainViewModel _viewModel = null!;
    private TrayService _tray = null!;
    private MainWindow _window = null!;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = AppSettings.Load();
        _settings.Save(); // materialise defaults on first run

        var scraper = new ContestScraper(_settings);
        var seen = SeenStore.Load();
        _notifier = new ToastNotifier(_settings);
        _monitor = new MonitorService(_settings, scraper, seen, _notifier);
        _scheduler = new SchedulerService(_settings, _monitor);
        _viewModel = new MainViewModel(_settings, _monitor, _scheduler, _notifier, seen);

        _tray = new TrayService();
        _tray.OpenRequested += ShowWindow;
        _tray.CheckRequested += () => _viewModel.CheckNowCommand.Execute(null);
        _tray.TestRequested += () => _notifier.ShowTestNotification();
        _tray.ExitRequested += ExitApp;

        // Toast click should bring the window forward.
        _notifier.Acknowledged += (_, _) => Dispatcher.Invoke(ShowWindow);

        _window = new MainWindow { DataContext = _viewModel };
        _window.Closing += OnWindowClosing;
        MainWindow = _window;

        if (_settings.StartMinimized)
        {
            // Start hidden in the tray.
            _window.Show();
            _window.Hide();
        }
        else
        {
            _window.Show();
        }

        _viewModel.Initialize();
    }

    private void ShowWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
    }

    /// <summary>Closing the window hides it to the tray instead of quitting.</summary>
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
            return;
        e.Cancel = true;
        _window.Hide();
        _tray.ShowHiddenHint();
    }

    private void ExitApp()
    {
        _exiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _scheduler?.Dispose();
        _notifier?.Dispose();
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}
