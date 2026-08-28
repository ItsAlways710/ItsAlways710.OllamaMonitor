using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ElBruno.OllamaMonitor.Configuration;

namespace ElBruno.OllamaMonitor;

public partial class MiniMonitorWindow : Window
{
    private static readonly TimeSpan PositionSaveDebounce = TimeSpan.FromMilliseconds(750);

    private readonly AppSettingsService? _settingsService;
    private bool _allowClose;
    private bool _positionPendingSave;
    private DispatcherTimer? _positionSaveTimer;

    public MiniMonitorWindow() : this(null)
    {
    }

    public MiniMonitorWindow(AppSettingsService? settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        LocationChanged += OnWindowLocationChanged;
        IsVisibleChanged += OnWindowIsVisibleChanged;
    }

    /// <summary>
    /// True when a window at (left, top) with the given size overlaps the virtual screen
    /// area by at least minimumVisibleExtent in both dimensions - i.e. it is actually
    /// reachable. Used to reject a saved position once all monitors that covered it are
    /// gone (disconnected) or the resolution shrank, so the window never starts invisible.
    /// </summary>
    public static bool IsPositionScreenVisible(double left, double top, double width, double height,
        double screenLeft, double screenTop, double screenWidth, double screenHeight,
        double minimumVisibleExtent)
    {
        var overlapWidth = Math.Min(left + width, screenLeft + screenWidth) - Math.Max(left, screenLeft);
        var overlapHeight = Math.Min(top + height, screenTop + screenHeight) - Math.Max(top, screenTop);
        return overlapWidth >= minimumVisibleExtent && overlapHeight >= minimumVisibleExtent;
    }

    public void PrepareForExit() => _allowClose = true;

    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        if (_settingsService is null || !IsVisible)
        {
            return;
        }

        _positionPendingSave = true;
        if (_positionSaveTimer is null)
        {
            _positionSaveTimer = new DispatcherTimer { Interval = PositionSaveDebounce };
            _positionSaveTimer.Tick += (_, _) =>
            {
                _positionSaveTimer!.Stop();
                SavePosition();
            };
        }

        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    private void OnWindowIsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        // Fires when Hide() takes effect - both the X button and the tray toggle route
        // through it - so the pending position is flushed before the window goes away.
        if (!IsVisible)
        {
            FlushPositionSave();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        FlushPositionSave();
        base.OnClosing(e);
    }

    private void OnWindowDrag(object? sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void FlushPositionSave()
    {
        _positionSaveTimer?.Stop();
        SavePosition();
    }

    private void SavePosition()
    {
        if (_settingsService is null || !_positionPendingSave)
        {
            return;
        }

        try
        {
            _settingsService.UpdateMiniMonitorPosition(Left, Top);
            _positionPendingSave = false;
        }
        catch
        {
            // Best effort: a failed save must not break the window lifecycle; the
            // position stays pending so a later hide/close retries the write.
        }
    }
}
