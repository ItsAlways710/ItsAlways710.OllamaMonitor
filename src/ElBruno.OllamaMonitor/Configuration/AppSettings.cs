namespace ElBruno.OllamaMonitor.Configuration;

using ElBruno.OllamaMonitor.Models;

public sealed record AppSettings
{
    public string Endpoint { get; init; } = "http://localhost:11434";
    public ModelUnloadStrategy UnloadStrategy { get; init; } = ModelUnloadStrategy.Auto;
    public int RefreshIntervalSeconds { get; init; } = 2;
    public bool ShowFloatingWindowOnStart { get; init; } = false;
    public bool EnableGpuMetrics { get; init; } = true;
    public bool EnableDiskMetrics { get; init; } = true;
    public double HighCpuThresholdPercent { get; init; } = 80;
    public double HighMemoryThresholdGb { get; init; } = 16;
    public double HighGpuThresholdPercent { get; init; } = 85;
    
    // Mini Monitor display toggles
    public bool ShowCpuInMiniMonitor { get; init; } = false;
    public bool ShowMemoryInMiniMonitor { get; init; } = false;
    public bool ShowContextInMiniMonitor { get; init; } = false;
    public bool ShowOllamaLogsInMiniMonitor { get; init; } = false;

    // Notification settings
    public bool EnableNotifications { get; init; } = true;
    public NotificationEventType NotificationEvents { get; init; } = 
        NotificationEventType.OllamaOffline | NotificationEventType.OllamaOnline |
        NotificationEventType.ModelLoaded | NotificationEventType.ModelUnloaded |
        NotificationEventType.ModelOperationFailed;
    public int NotificationDebounceSeconds { get; init; } = 30;
}
