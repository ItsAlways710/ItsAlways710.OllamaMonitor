using System.Text.Json;
using ItsAlways710.OllamaMonitor.Configuration;
using ItsAlways710.OllamaMonitor.Models;

namespace ItsAlways710.OllamaMonitor.Tests;

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
    public void Defaults_MiniMonitorPosition_IsNull()
    {
        var settings = new AppSettings();
        Assert.Null(settings.MiniMonitorLeft);
        Assert.Null(settings.MiniMonitorTop);
    }

    [Fact]
    public void Deserialization_PresetJsonWithoutPositionKeys_PositionIsNull()
    {
        // JSON from a settings file written before position persistence existed —
        // the missing keys must fall back to null (window keeps its default placement).
        const string json = """
            {
              "endpoint": "http://localhost:11434",
              "refreshIntervalSeconds": 2,
              "showCpuInMiniMonitor": true
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var settings = JsonSerializer.Deserialize<AppSettings>(json, options)!;

        Assert.True(settings.ShowCpuInMiniMonitor);
        Assert.Null(settings.MiniMonitorLeft);
        Assert.Null(settings.MiniMonitorTop);
    }

    [Fact]
    public void MiniMonitorPosition_RoundTripsThroughJson()
    {
        var settings = new AppSettings
        {
            MiniMonitorLeft = 640.5,
            MiniMonitorTop = 221.5
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(settings, options);
        var restored = JsonSerializer.Deserialize<AppSettings>(json, options)!;

        Assert.Equal(640.5, restored.MiniMonitorLeft);
        Assert.Equal(221.5, restored.MiniMonitorTop);
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
