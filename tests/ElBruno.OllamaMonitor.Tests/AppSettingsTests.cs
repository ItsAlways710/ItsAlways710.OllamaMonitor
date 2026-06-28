using System.Text.Json;
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

    [Fact]
    public void Defaults_ShowCpuInMiniMonitor_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ShowCpuInMiniMonitor);
    }

    [Fact]
    public void Defaults_ShowMemoryInMiniMonitor_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ShowMemoryInMiniMonitor);
    }

    [Fact]
    public void Defaults_ShowOllamaLogsInMiniMonitor_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ShowOllamaLogsInMiniMonitor);
    }

    [Fact]
    public void Deserialization_MissingMiniMonitorKeys_DefaultsToFalse()
    {
        // JSON that predates the mini-monitor toggles — simulates an existing settings file
        // that does not contain the new keys. Missing keys must fall back to false.
        const string json = """
            {
              "endpoint": "http://localhost:11434",
              "unloadStrategy": 0,
              "refreshIntervalSeconds": 2
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var settings = JsonSerializer.Deserialize<AppSettings>(json, options)!;

        Assert.False(settings.ShowCpuInMiniMonitor);
        Assert.False(settings.ShowMemoryInMiniMonitor);
        Assert.False(settings.ShowOllamaLogsInMiniMonitor);
    }
}
