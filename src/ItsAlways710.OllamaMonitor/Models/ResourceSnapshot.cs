namespace ItsAlways710.OllamaMonitor.Models;

public sealed record ResourceSnapshot
{
    public double? CpuPercent { get; init; }
    public long? MemoryBytes { get; init; }
    public double? MemoryGb { get; init; }
    public long? PrivateMemoryBytes { get; init; }
    public long? DiskReadBytesPerSecond { get; init; }
    public long? DiskWriteBytesPerSecond { get; init; }
    public double? SystemCpuPercent { get; init; }
    public double? SystemMemoryPercent { get; init; }
    public double? GpuPercent { get; init; }
    public long? VramUsedBytes { get; init; }
    public long? VramTotalBytes { get; init; }
    public string? GpuName { get; init; }
    public string? GpuStatus { get; init; }
    public DateTimeOffset? ProcessStartedAt { get; init; }
    public string? ProcessName { get; init; }
}
