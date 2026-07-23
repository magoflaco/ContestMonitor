using System.Windows;
using System.Windows.Forms;
using Drawing = System.Drawing;

namespace ContestMonitor.Services;

/// <summary>
/// Owns the system-tray icon and its menu, so the app can keep running in the
/// background after the window is closed. Raises events the composition root
/// wires to the appropriate actions (DRY: the tray has no app logic itself).
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private bool _balloonShown;

    public event Action? OpenRequested;
    public event Action? CheckRequested;
    public event Action? TestRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Check now", null, (_, _) => CheckRequested?.Invoke());
        menu.Items.Add("Test notification", null, (_, _) => TestRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Contest Monitor",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    /// <summary>Show a tray balloon (used once, when the window hides to tray).</summary>
    public void ShowHiddenHint()
    {
        if (_balloonShown)
            return;
        _balloonShown = true;
        _icon.BalloonTipTitle = "Contest Monitor";
        _icon.BalloonTipText = "Still running. Right-click the tray icon to open or exit.";
        _icon.ShowBalloonTip(3000);
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app.ico"));
            if (info is not null)
                return new Drawing.Icon(info.Stream, new Drawing.Size(32, 32));
        }
        catch
        {
            // fall through to the system default below
        }
        return Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
