using System.Text;
using ItsAlways710.OllamaMonitor.Diagnostics;

namespace ItsAlways710.OllamaMonitor.Services;

/// <summary>
/// Captures Ollama server log lines using a hybrid strategy:
///   1. If OllamaCliService owns the ollama process (started via StartOllama), receives
///      lines forwarded from redirected stdout/stderr (SetProcessOwned + OnOwnedProcessOutput).
///   2. Otherwise tails Ollama's server.log every 2 s, resolving the active file among the
///      known locations: %USERPROFILE%\.ollama\logs\server.log (CLI/server install) and
///      %LOCALAPPDATA%\Ollama\server.log (Ollama for Windows desktop app).
/// Maintains a ring buffer of the 5 most-recent lines. Thread-safe.
/// </summary>
public sealed class OllamaLogService : IOllamaLogService, IDisposable
{
    private const int MaxLines = 5;
    private const int PollIntervalMs = 2000;

    private readonly DiagnosticsLogService _diagnostics;
    private readonly string _logFilePath;
    private readonly Lock _syncRoot = new();
    private readonly List<string> _recentLines = new(MaxLines + 1);

    private System.Threading.Timer? _pollTimer;
    private long _fileOffset;
    private bool _processOwned;
    private bool _running;

    public event Action<string>? LogLineReceived;

    public IReadOnlyList<string> RecentLines
    {
        get
        {
            lock (_syncRoot)
            {
                return _recentLines.ToArray();
            }
        }
    }

    public OllamaLogService(DiagnosticsLogService diagnostics)
    {
        _diagnostics = diagnostics;
        _logFilePath = ResolveLogPath(GetDefaultLogFilePaths());
    }

    internal OllamaLogService(DiagnosticsLogService diagnostics, string logFilePath)
    {
        _diagnostics = diagnostics;
        _logFilePath = logFilePath;
    }

    private static IReadOnlyList<string> GetDefaultLogFilePaths()
    {
        return
        [
            // Ollama CLI / server installation.
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ollama", "logs", "server.log"),
            // Ollama for Windows (desktop app) installation.
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ollama", "server.log")
        ];
    }

    /// <summary>
    /// Chooses which log file to tail among the candidates: the most recently written file that
    /// exists, or — when none exist yet — the first candidate (the CLI location, so that a log
    /// created by a fresh install is picked up as soon as it appears).
    /// </summary>
    internal static string ResolveLogPath(IReadOnlyList<string> candidates)
    {
        string? bestPath = null;
        var bestWriteTime = DateTimeOffset.MinValue;
        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var writeTime = File.GetLastWriteTimeUtc(path);
                if (bestPath is null || writeTime > bestWriteTime)
                {
                    bestPath = path;
                    bestWriteTime = writeTime;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return bestPath ?? candidates.FirstOrDefault()
            ?? throw new InvalidOperationException("No Ollama log file candidates were provided.");
    }

    /// <summary>Starts log capture. Idempotent.</summary>
    public void Start()
    {
        lock (_syncRoot)
        {
            if (_running) return;
            _running = true;
            if (_processOwned) return; // process-owned path active; no file polling needed
        }
        StartFilePoll();
    }

    /// <summary>Stops log capture. Idempotent.</summary>
    public void Stop()
    {
        lock (_syncRoot)
        {
            if (!_running) return;
            _running = false;
        }
        StopFilePoll();
    }

    /// <summary>
    /// Called by OllamaCliService when it has started ollama serve with redirected output.
    /// Switches the service to process-owned mode and stops file polling if active.
    /// </summary>
    internal void SetProcessOwned()
    {
        lock (_syncRoot)
        {
            _processOwned = true;
        }
        StopFilePoll();
    }

    /// <summary>
    /// Called by OllamaCliService for each stdout/stderr line from the managed ollama process.
    /// </summary>
    internal void OnOwnedProcessOutput(string? line)
    {
        if (string.IsNullOrEmpty(line)) return;
        AppendLine(line);
    }

    private void StartFilePoll()
    {
        // Seek to the current end of the file so we only emit newly appended lines.
        try
        {
            if (File.Exists(_logFilePath))
                _fileOffset = new FileInfo(_logFilePath).Length;
            else
                _fileOffset = 0;
        }
        catch
        {
            _fileOffset = 0;
        }

        _pollTimer = new System.Threading.Timer(_ => PollFile(), null, PollIntervalMs, PollIntervalMs);
    }

    private void StopFilePoll()
    {
        var timer = _pollTimer;
        _pollTimer = null;
        timer?.Dispose();
    }

    private void PollFile()
    {
        try
        {
            if (!File.Exists(_logFilePath)) return;

            using var fs = new FileStream(
                _logFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            // Detect log rotation / truncation.
            if (fs.Length < _fileOffset)
                _fileOffset = 0;

            if (fs.Length == _fileOffset) return;

            fs.Seek(_fileOffset, SeekOrigin.Begin);

            using var reader = new StreamReader(fs, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

            var content = reader.ReadToEnd();
            _fileOffset = fs.Position;

            foreach (var raw in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(line))
                    AppendLine(line);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _diagnostics.WriteWarning($"OllamaLogService: cannot read log file: {ex.Message}");
        }
        catch (Exception ex)
        {
            _diagnostics.WriteWarning($"OllamaLogService: unexpected error polling log: {ex.Message}");
        }
    }

    private void AppendLine(string line)
    {
        Action<string>? handler;
        lock (_syncRoot)
        {
            _recentLines.Add(line);
            while (_recentLines.Count > MaxLines)
                _recentLines.RemoveAt(0);
            handler = LogLineReceived;
        }

        try
        {
            handler?.Invoke(line);
        }
        catch (Exception ex)
        {
            _diagnostics.WriteWarning($"OllamaLogService: error in LogLineReceived handler: {ex.Message}");
        }
    }

    public void Dispose() => Stop();
}
