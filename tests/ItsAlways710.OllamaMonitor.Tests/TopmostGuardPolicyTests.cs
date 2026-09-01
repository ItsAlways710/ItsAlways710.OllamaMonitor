using ItsAlways710.OllamaMonitor.Interop;

namespace ItsAlways710.OllamaMonitor.Tests;

/// <summary>
/// Tests the topmost guard's demotion decision: WINDOWPOS.hwndInsertAfter values that
/// would move the window out of the topmost band (HWND_BOTTOM, HWND_NOTOPMOST, any real
/// window handle, and unrecognized sentinels) must be flagged so the guard can rewrite
/// them to HWND_TOPMOST, while band-preserving positions (HWND_TOP, HWND_TOPMOST) must
/// pass through untouched so WPF's own moves are not disturbed.
/// </summary>
public sealed class TopmostGuardPolicyTests
{
    [Theory]
    [InlineData(TopmostGuardPolicy.HwndTop)]
    [InlineData(TopmostGuardPolicy.HwndTopmost)]
    public void BandPreservingPositions_AreNotDemoting(long hwndInsertAfter) =>
        Assert.False(TopmostGuardPolicy.IsDemotingPosition(hwndInsertAfter));

    [Theory]
    [InlineData(TopmostGuardPolicy.HwndBottom)]
    [InlineData(TopmostGuardPolicy.HwndNotopmost)]
    public void TopmostStrippingSentinels_AreDemoting(long hwndInsertAfter) =>
        Assert.True(TopmostGuardPolicy.IsDemotingPosition(hwndInsertAfter));

    [Theory]
    [InlineData(0x00030042L)]
    [InlineData(0x001200080012L)]
    public void RealWindowHandles_AreDemoting(long hwndInsertAfter) =>
        // Inserting before any non-topmost window strips topmost state (MSDN SetWindowPos).
        Assert.True(TopmostGuardPolicy.IsDemotingPosition(hwndInsertAfter));

    [Fact]
    public void UnrecognizedSentinel_AreDemoting() =>
        // Conservative: anything we do not explicitly recognize as band-preserving is rewritten.
        Assert.True(TopmostGuardPolicy.IsDemotingPosition(-3));
}
