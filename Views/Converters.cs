using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ContestMonitor.Views;

/// <summary>Maps a status kind string to an accent brush for the status pill.</summary>
public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "ok" => "SuccessBrush",
            "alert" => "AlertBrush",
            "error" => "ErrorBrush",
            "working" => "AccentBrush",
            _ => "MutedBrush",
        };
        return Application.Current.TryFindResource(key) ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>true -> Visible, false -> Collapsed.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        value is Visibility.Visible;
}
