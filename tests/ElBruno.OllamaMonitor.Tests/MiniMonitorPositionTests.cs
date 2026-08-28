using ElBruno.OllamaMonitor;

namespace ElBruno.OllamaMonitor.Tests;

/// <summary>
/// Tests the saved-position fallback guard: a saved Left/Top is only restored when the
/// window would still be reachable on a connected screen. A position whose overlap with
/// the virtual screen is smaller than the minimum visible extent (monitor unplugged or
/// resolution shrank since the position was saved) must be rejected so the window falls
/// back to its default placement instead of starting invisible.
/// </summary>
public sealed class MiniMonitorPositionTests
{
    private const double MinVisibleExtent = 50;
    private const double WindowWidth = 320;
    private const double WindowHeight = 300;

    // Single 1920x1080 screen at the origin.
    private const double SLeft = 0;
    private const double STop = 0;
    private const double SWidth = 1920;
    private const double SHeight = 1080;

    private static bool Visible(double left, double top) =>
        MiniMonitorWindow.IsPositionScreenVisible(left, top, WindowWidth, WindowHeight,
            SLeft, STop, SWidth, SHeight, MinVisibleExtent);

    [Fact]
    public void SavedPosition_FullyOnScreen_IsRestored() => Assert.True(Visible(400, 300));

    [Fact]
    public void SavedPosition_AtTopLeftCorner_IsRestored() => Assert.True(Visible(0, 0));

    [Fact]
    public void SavedPosition_PartiallyOffscreen_StillReachable_IsRestored() =>
        // Window straddles the right edge but 220px of it is on-screen.
        Assert.True(Visible(SWidth - 220, 100));

    [Fact]
    public void SavedPosition_JustPastRightEdge_NotRestored() =>
        // 40px overlap - under the 50px minimum visible extent.
        Assert.False(Visible(SWidth - 40, 100));

    [Fact]
    public void SavedPosition_FullyOffscreenRight_NotRestored() => Assert.False(Visible(SWidth + 100, 100));

    [Fact]
    public void SavedPosition_FullyOffscreenTop_NotRestored() => Assert.False(Visible(400, -400));

    [Fact]
    public void SavedPosition_MonitorDisconnected_LeftOfNewVirtualScreen_NotRestored() =>
    // 1920-wide monitor at the origin was a second display to the left and is now gone:
    // the union of connected screens is just the origin screen, so (-960, 100) is off it.
    Assert.False(Visible(-960, 100));

    [Fact]
    public void SavedPosition_TwinLayout_SecondMonitorDisconnected_NotRestored() =>
    // Two 1920px monitors side by side (virtual screen 3840 wide); the right one is
    // unplugged, leaving (1920, 100) beyond the remaining virtual screen.
    Assert.False(MiniMonitorWindow.IsPositionScreenVisible(1950, 100, WindowWidth, WindowHeight,
        0, 0, 1920, 1080, MinVisibleExtent));

    [Fact]
    public void SavedPosition_NegativeCoordinateLayout_MonitorStillConnected_IsRestored() =>
    // Left monitor at x=-1920..0, right at 0..1920; position on the left monitor is fine.
    Assert.True(MiniMonitorWindow.IsPositionScreenVisible(-1500, 100, WindowWidth, WindowHeight,
        -1920, 0, 3840, 1080, MinVisibleExtent));

    [Fact]
    public void SavedPosition_SmallerResolutionSinceSave_NotRestored() =>
    // Screen shrank from 1920 to 1280 wide; saved position past the new edge is rejected.
    Assert.False(MiniMonitorWindow.IsPositionScreenVisible(1400, 100, WindowWidth, WindowHeight,
        0, 0, 1280, 1024, MinVisibleExtent));
}
