using System.Windows;
using ContestMonitor.Configuration;
using ContestMonitor.Services;
using ContestMonitor.ViewModels;
using ContestMonitor.Views;

namespace ContestMonitor;

/// <summary>
/// Composition root. Wires settings and services together by hand (no DI
/// container needed for an app this size) and shows the main window.
/// </summary>
public partial class App : Application
{
    private AppSettings _settings = null!;
    private ToastNotifier _notifier = null!;
    private SchedulerService _scheduler = null!;
    private MonitorService _monitor = null!;
    private MainViewModel _viewModel = null!;

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

        var window = new MainWindow { DataContext = _viewModel };
        MainWindow = window;

        if (_settings.StartMinimized)
            window.WindowState = WindowState.Minimized;

        window.Show();
        _viewModel.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scheduler?.Dispose();
        _notifier?.Dispose();
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}
