using System.Diagnostics;
using System.Windows;
using ContestMonitor.ViewModels;

namespace ContestMonitor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        // Bringing the window to the foreground counts as seeing the alert,
        // which stops the repeat notifications.
        (DataContext as MainViewModel)?.OnWindowActivated();
    }

    private void OnEnrollClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ContestRow { EnrollUri: { } uri })
            OpenUrl(uri.AbsoluteUri);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Nothing sensible to do if the shell can't open the browser.
        }
    }
}
