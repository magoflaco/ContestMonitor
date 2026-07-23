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
}
