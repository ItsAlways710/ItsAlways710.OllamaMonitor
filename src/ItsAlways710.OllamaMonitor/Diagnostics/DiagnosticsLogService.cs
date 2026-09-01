namespace ItsAlways710.OllamaMonitor.Diagnostics;

public sealed class DiagnosticsLogService
{
    private readonly string _logDirectoryPath;
    private readonly Lock _syncRoot = new();

    public DiagnosticsLogService(string logDirectoryPath)
    {
        _logDirectoryPath = logDirectoryPath;
    }

    public void WriteInfo(string message) => Write("INFO", message);

    public void WriteWarning(string message) => Write("WARN", message);

    public void WriteError(string message, Exception? exception = null)
    {
        var fullMessage = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", fullMessage);
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(_logDirectoryPath);
            var logPath = Path.Combine(_logDirectoryPath, $"{DateTime.UtcNow:yyyyMMdd}.log");
            var line = $"[{DateTimeOffset.Now:O}] [{level}] {message}{Environment.NewLine}";

            lock (_syncRoot)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Best-effort logging only.
        }
    }
}
