namespace ItsAlways710.OllamaMonitor.Models;

public sealed record OllamaMonitorSnapshot
{
    public OllamaMonitorState State { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string? Version { get; init; }
    public bool IsApiReachable { get; init; }
    public IReadOnlyList<OllamaModelSnapshot> Models { get; init; } = [];
    public IReadOnlyList<ContextWindowSample> ContextWindows { get; init; } = [];
    public ResourceSnapshot? Resources { get; init; }
    public DateTimeOffset LastChecked { get; init; }
    public string? ErrorMessage { get; init; }
}
