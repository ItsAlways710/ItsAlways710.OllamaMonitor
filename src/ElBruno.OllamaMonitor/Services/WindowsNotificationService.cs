using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Models;

namespace ElBruno.OllamaMonitor.Services;

public sealed class WindowsNotificationService : IDisposable
{
    private readonly DiagnosticsLogService _diagnostics;
    private readonly Dictionary<NotificationEventType, DateTime> _lastNotificationTimes = new();
    private readonly object _lock = new();
    private int _debounceSeconds = 30;
    private bool _disposed;

    public WindowsNotificationService(DiagnosticsLogService diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public void SetDebounceSeconds(int seconds)
    {
        _debounceSeconds = Math.Max(1, seconds);
    }

    public bool CanNotify(NotificationEventType eventType)
    {
        lock (_lock)
        {
            if (!_lastNotificationTimes.TryGetValue(eventType, out var lastTime))
            {
                return true;
            }

            var timeSinceLastNotification = DateTime.UtcNow - lastTime;
            return timeSinceLastNotification.TotalSeconds >= _debounceSeconds;
        }
    }

    public void ShowNotification(NotificationEventType eventType, string title, string message)
    {
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!CanNotify(eventType))
            {
                _diagnostics.WriteInfo($"Notification throttled: {eventType}");
                return;
            }

            lock (_lock)
            {
                _lastNotificationTimes[eventType] = DateTime.UtcNow;
            }

            var toastXml = GenerateToastXml(title, message);
            var doc = new XmlDocument();
            doc.LoadXml(toastXml);

            var toast = new ToastNotification(doc)
            {
                ExpirationTime = DateTimeOffset.UtcNow.AddSeconds(10)
            };

            ToastNotificationManager.CreateToastNotifier("ItsAlways710.OllamaMonitor").Show(toast);
            _diagnostics.WriteInfo($"Notification shown: {eventType} - {title}");
        }
        catch (Exception ex)
        {
            _diagnostics.WriteError($"Failed to show notification for {eventType}", ex);
        }
    }

    private static string GenerateToastXml(string title, string message)
    {
        return $@"
<toast>
  <visual>
    <binding template=""ToastText02"">
      <text id=""1"">{EscapeXml(title)}</text>
      <text id=""2"">{EscapeXml(message)}</text>
    </binding>
  </visual>
</toast>";
    }

    private static string EscapeXml(string text)
    {
        return System.Security.SecurityElement.Escape(text) ?? text;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (_lock)
        {
            _lastNotificationTimes.Clear();
        }
    }
}
