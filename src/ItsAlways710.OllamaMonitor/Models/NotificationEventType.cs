namespace ItsAlways710.OllamaMonitor.Models;

[Flags]
public enum NotificationEventType
{
    None = 0,
    OllamaOffline = 1,
    OllamaOnline = 2,
    ModelLoaded = 4,
    ModelUnloaded = 8,
    HighCpuUsage = 16,
    HighMemoryUsage = 32,
    HighGpuUsage = 64,
    ModelOperationSucceeded = 128,
    ModelOperationFailed = 256,
    OllamaStarted = 512
}
