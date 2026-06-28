namespace ElBruno.OllamaMonitor.Services;

public interface IOllamaLogService
{
    event Action<string>? LogLineReceived;
    IReadOnlyList<string> RecentLines { get; }
    void Start();
    void Stop();
}
