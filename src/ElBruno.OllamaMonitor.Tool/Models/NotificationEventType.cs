namespace ElBruno.OllamaMonitor.Models;

[Flags]
public enum NotificationEventType
{
    None = 0,
    OllamaOffline = 1 << 0,
    OllamaOnline = 1 << 1,
    ModelLoaded = 1 << 2,
    ModelUnloaded = 1 << 3,
    HighCpuUsage = 1 << 4,
    HighMemoryUsage = 1 << 5,
    HighGpuUsage = 1 << 6,
    ModelOperationSucceeded = 1 << 7,
    ModelOperationFailed = 1 << 8,
    OllamaStarted = 1 << 9,
}
