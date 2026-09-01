using ItsAlways710.OllamaMonitor.Interop;

namespace ItsAlways710.OllamaMonitor.Tests;

/// <summary>
/// Tests the reference-window forensics the topmost guard logs when it blocks a
/// demotion: the identity must name pid, process, class and title when available,
/// degrade every missing part to n/a instead of throwing, classify the current
/// thread as in-process and foreign thread ids as not, and capture an invalid
/// handle as an all-null identity.
/// </summary>
public sealed class RefWindowForensicsTests
{
    [Fact]
    public void Format_FullIdentity_ContainsEveryField()
    {
        var identity = new RefWindowIdentity
        {
            Handle = new IntPtr(0x831158),
            OwnerPid = 1234,
            OwnerProcessName = "monarch",
            ClassName = "FooClass",
            Title = "Monarch Window"
        };

        string text = RefWindowForensics.Format(identity);

        Assert.Equal("hwnd=0x831158; pid=1234; process=monarch; class=FooClass; title='Monarch Window'", text);
    }

    [Fact]
    public void Format_MissingParts_DegradeToNa()
    {
        var identity = new RefWindowIdentity
        {
            Handle = new IntPtr(0x111A4),
            OwnerPid = 32056,
            ClassName = "CabinetWClass"
        };

        string text = RefWindowForensics.Format(identity);

        Assert.Equal("hwnd=0x111A4; pid=32056; process=n/a; class=CabinetWClass; title=n/a", text);
    }

    [Fact]
    public void Format_NullIdentity_ReturnsNa() =>
        Assert.Equal("n/a", RefWindowForensics.Format(null));

    [Fact]
    public void Capture_InvalidHandle_ReturnsAllNullIdentity()
    {
        RefWindowIdentity identity = RefWindowForensics.Capture(IntPtr.Zero);

        Assert.Equal(IntPtr.Zero, identity.Handle);
        Assert.Null(identity.OwnerPid);
        Assert.Null(identity.OwnerProcessName);
        Assert.Null(identity.ClassName);
        Assert.Null(identity.Title);
    }

    [Fact]
    public void IsOwnProcessThread_CurrentThread_IsOwn() =>
        Assert.True(RefWindowForensics.IsOwnProcessThread(RefWindowForensics.GetCurrentThreadId()));

    [Fact]
    public void IsOwnProcessThread_InvalidThread_IsNotOwn() =>
        Assert.False(RefWindowForensics.IsOwnProcessThread(0));
}
