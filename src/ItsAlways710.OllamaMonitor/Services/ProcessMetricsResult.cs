namespace ItsAlways710.OllamaMonitor.Services;

public sealed record ProcessMetricsResult(
    bool IsProcessFound,
    double? CpuPercent = null,
    long? WorkingSetBytes = null,
    long? PrivateMemoryBytes = null,
    long? DiskReadBytesPerSecond = null,
    long? DiskWriteBytesPerSecond = null,
    DateTimeOffset? StartedAt = null,
    string? ProcessName = null,
    string? ErrorMessage = null);
