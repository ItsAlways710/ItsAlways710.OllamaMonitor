namespace ItsAlways710.OllamaMonitor.Services;

public sealed record GpuMetricsResult(
    bool IsAvailable,
    double? GpuPercent = null,
    long? VramUsedBytes = null,
    long? VramTotalBytes = null,
    string? GpuName = null,
    string? StatusMessage = null);
