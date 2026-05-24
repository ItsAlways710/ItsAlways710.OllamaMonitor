namespace ElBruno.OllamaMonitor.Models;

public sealed record OllamaModelSnapshot
{
    public string Name { get; init; } = string.Empty;
    public string? Size { get; init; }
    public long? SizeVram { get; init; }
    public string? Processor { get; init; }
    public string? ProcessorUsage { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string ExpiresAtDisplay => ExpiresAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "n/a";
    public bool IsActive => ExpiresAt == null || DateTime.UtcNow < ExpiresAt.Value.UtcDateTime;
}
