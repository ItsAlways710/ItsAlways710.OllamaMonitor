namespace ItsAlways710.OllamaMonitor.Cli;

public sealed record CliCommand(
    CliCommandKind Kind,
    bool ShouldLaunchApp = false,
    string? StringValue = null,
    int? IntValue = null,
    string? ErrorMessage = null);
