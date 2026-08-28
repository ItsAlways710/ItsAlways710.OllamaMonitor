using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.PlotStyles;
using ScottPlot.WPF;

namespace ElBruno.OllamaMonitor.Services;

/// <summary>
/// Rolling usage chart for the Details window GPU Status panel (ScottPlot).
/// Fed only by the app's existing refresh tick via <see cref="AddSample"/> — it holds no
/// timer and no history of its own, so the chart starts empty on every launch.
/// </summary>
public sealed class GpuUsageGraph
{
    /// <summary>
    /// Duration of the rolling window shown in the chart. Oldest samples fall off
    /// as new ones arrive; adjust this single constant to change the window.
    /// </summary>
    public const int WindowSeconds = 10 * 60;

    private readonly WpfPlot _control;
    private readonly Plot _plot;
    private readonly DataStreamer _vram;
    private readonly DataStreamer _gpu;
    private readonly double _points;
    private readonly double _newestCoordinate;

    /// <summary>ScottPlot's dateTime X zero is 1899-12-30; unix epoch (1970-01-01) is 25569 days later.</summary>
    private const double OLEDaysAtUnixEpoch = 25569d;

    /// <summary>
    /// Create the chart on the given control. <paramref name="samplePeriodSeconds"/> is the
    /// app's refresh interval; it sizes the sample buffer so the window holds
    /// <see cref="WindowSeconds"/> of data.
    /// </summary>
    public GpuUsageGraph(WpfPlot control, int samplePeriodSeconds)
    {
        _control = control;
        _plot = control.Plot;
        double period = Math.Max(1, samplePeriodSeconds);

        // Display-only: no pan/zoom, no context menu.
        _control.UserInputProcessor.Disable();
        _control.Menu = null;

        _plot.Axes.DateTimeTicksBottom();

        // ScottPlot's dateTime axis measures X in DAYS (days since 1899-12-30);
        // all sample coordinates below are expressed in that unit.
        double periodDays = period / 86400d;

        _points = Math.Max(2, WindowSeconds / period);
        _newestCoordinate = (_points - 1) * periodDays;

        // VRAM used as a percentage of total — primary series: filled green area.
        _vram = _plot.Add.DataStreamer((int)_points, periodDays);
        // The built-in limit manager only ever expands the Y axis to fit data;
        // we keep a fixed 0-100 axis and set the limits ourselves.
        _vram.ManageAxisLimits = false;
        _vram.FillY = true;
        _vram.FillYValue = 0;
        _vram.FillYColor = Colors.Green.WithAlpha(0.3);
        _vram.Color = Colors.Green;
        // Scroll view keeps the newest sample anchored at the right edge ("now");
        // the default Wipe view drifts it leftward until the buffer wraps.
        _vram.ViewScrollLeft();

        // GPU usage percentage — secondary series: plain orange line on top.
        _gpu = _plot.Add.DataStreamer((int)_points, periodDays);
        _gpu.ManageAxisLimits = false;
        _gpu.Color = Colors.Orange;
        _gpu.ViewScrollLeft();

        _plot.Axes.SetLimitsY(0, 100);
    }

    /// <summary>
    /// Shift one poll in. Percentages must already be 0-100; null values (e.g. nvidia-smi
    /// unavailable) are skipped so the buffer keeps its last real readings.
    /// </summary>
    public void AddSample(double? vramPercent, double? gpuPercent)
    {
        bool added = false;
        if (vramPercent is { } v)
        {
            _vram.Add(v);
            added = true;
        }

        if (gpuPercent is { } g)
        {
            _gpu.Add(g);
            added = true;
        }

        if (!added)
        {
            return;
        }

        // Re-anchor the buffer's index coordinates (in OLE days) so the newest sample sits at "now";
        // the window then spans the preceding WindowSeconds.
        double now = DateTimeOffset.Now.ToUnixTimeSeconds() / 86400d + OLEDaysAtUnixEpoch;
        _vram.Data.OffsetX = now - _newestCoordinate;
        _gpu.Data.OffsetX = now - _newestCoordinate;

        _plot.Axes.SetLimitsX(now - WindowSeconds / 86400d, now);
        _plot.Axes.SetLimitsY(0, 100);
        _control.Refresh();
    }

    /// <summary>
    /// Restyle the chart to the app's resolved theme using ScottPlot's built-in styles
    /// (call after any theme change, including startup).
    /// </summary>
    public void ApplyTheme(bool isDark)
    {
        _plot.SetStyle(isDark ? new Dark() : new Light());
    }
}
