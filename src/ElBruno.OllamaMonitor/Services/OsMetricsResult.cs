namespace ElBruno.OllamaMonitor.Services;

public sealed record OsMetricsResult(
    double? CpuPercent = null,
    long? MemoryTotalBytes = null,
    long? MemoryUsedBytes = null,
    double? MemoryPercent = null);
