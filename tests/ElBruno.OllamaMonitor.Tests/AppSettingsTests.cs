using ElBruno.OllamaMonitor.Configuration;
using ElBruno.OllamaMonitor.Models;

namespace ElBruno.OllamaMonitor.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_UseAutoUnloadStrategy()
    {
        var settings = new AppSettings();
        Assert.Equal(ModelUnloadStrategy.Auto, settings.UnloadStrategy);
    }

    [Fact]
    public void Defaults_IncludeModelOperationFailedNotification()
    {
        var settings = new AppSettings();
        Assert.True((settings.NotificationEvents & NotificationEventType.ModelOperationFailed) != 0);
    }
}
