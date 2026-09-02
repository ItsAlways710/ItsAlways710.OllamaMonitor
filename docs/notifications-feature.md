# Windows Notifications Feature - Implementation Summary

## Overview
Added comprehensive Windows notification support to ItsAlways710.OllamaMonitor, allowing users to receive desktop notifications for various Ollama events and resource alerts.

## Features Implemented

### 1. **Notification Types** (NotificationEventType enum)
- `OllamaOffline` - When Ollama becomes unreachable
- `OllamaOnline` - When Ollama comes back online
- `ModelLoaded` - When a model is loaded
- `ModelUnloaded` - When a model is unloaded
- `ModelOperationSucceeded` - When a model operation (stop/pull/remove/copy) succeeds
- `ModelOperationFailed` - When a model operation (stop/pull/remove/copy) fails
- `OllamaStarted` - When an Ollama daemon start is requested
- `HighCpuUsage` / `HighMemoryUsage` / `HighGpuUsage` - When resource usage exceeds thresholds

### 2. **Core Components**

#### WindowsNotificationService (`Services/WindowsNotificationService.cs`)
- Sends Windows toast notifications
- Implements **debouncing/throttling** to prevent notification spam
- Configurable debounce interval (default: 30 seconds)
- Thread-safe implementation
- Proper resource cleanup via IDisposable

**Key Features:**
```csharp
// Check if notification can be shown (respects debounce)
bool CanNotify(NotificationEventType eventType)

// Show notification with auto-expiration (10 seconds)
void ShowNotification(NotificationEventType eventType, string title, string message)

// Configure debounce interval
void SetDebounceSeconds(int seconds)
```

#### Enhanced Configuration (`Configuration/AppSettings.cs`)
- `EnableNotifications` - Global toggle for all notifications (default: true)
- `NotificationEvents` - Bitmask of event types to notify (default: OllamaOffline | OllamaOnline | ModelLoaded | ModelUnloaded | ModelOperationFailed)
- `NotificationDebounceSeconds` - Debounce interval (default: 30 seconds)

#### Settings Window (`SettingsWindow.xaml` + `ViewModels/SettingsWindowViewModel.cs`)
- **Notification Management UI** with organized sections:
  - Ollama Status events
  - Model events
  - Resource usage alerts
- **Debounce slider** (5-300 seconds)
- **General settings** preserved
- Real-time checkboxes for fine-grained control

#### Enhanced ViewModel (`ViewModels/MainWindowViewModel.cs`)
- Tracks previous Ollama state for state-change detection
- Tracks model changes to detect loads/unloads
- Validates resource thresholds against current settings
- **OpenSettingsCommand** - Opens settings dialog
- **Dispose** method - Cleans up notification service

### 3. **Main Window Enhancement**
- Added **Settings button** to toolbar
- Settings button opens modal dialog
- Auto-refreshes on save

---

## How It Works

### State Change Detection Flow:

```
RefreshAsync()
    ↓
LoadSettings()
    ↓
GetSnapshotAsync()
    ↓
CheckAndNotifyStateChanges()
    ├─ Ollama state changed? → Notify (with debounce)
    ├─ Models added/removed? → Notify (with debounce)
    └─ Resource threshold exceeded? → Notify (with debounce)
```

### Debouncing Example:
```
Time: 0s    - Event triggered → Notification shown, last_notification_time = 0s
Time: 5s    - Same event triggered → Blocked (< 30s debounce)
Time: 30s   - Same event triggered → Blocked (= 30s, requires >)
Time: 31s   - Same event triggered → Notification shown, last_notification_time = 31s
```

---

## Configuration

### Enable/Disable Notifications
Notifications can be disabled globally in the Settings window, or by setting
`"enableNotifications": false` in `settings.json`. The CLI does not manage this key
(`config set` only supports `endpoint` and `refresh-interval`).

### Select Event Types
Each event type has its own checkbox in Settings UI:
- Combine multiple events via bitmask in config

### Adjust Debounce
Slider in Settings (5-300 seconds):
- Lower = more frequent notifications (but still throttled)
- Higher = less frequent notifications

---

## Default Behavior

**Out of the box:**
- ✅ Notifications enabled
- ✅ Ollama offline/online, model loaded/unloaded, and model-operation-failure events enabled by default
- ✅ 30-second debounce prevents spam
- ✅ Can disable any event type in Settings

**First-time users experience:**
1. Ollama goes offline → 🔴 "Ollama Offline" notification
2. Settings → Notifications → select which events to monitor
3. Save → settings persist to disk and apply on the next refresh cycle
4. Notifications adapt to preferences

---

## Files Created/Modified

### New Files:
- `Models/NotificationEventType.cs` - Enum for notification types
- `Services/WindowsNotificationService.cs` - Notification service
- `SettingsWindow.xaml` - Settings UI
- `SettingsWindow.xaml.cs` - Settings code-behind
- `ViewModels/SettingsWindowViewModel.cs` - Settings logic

### Modified Files:
- `Configuration/AppSettings.cs` - Added notification settings
- `ItsAlways710.OllamaMonitor.csproj` - Added SDK reference
- `ViewModels/MainWindowViewModel.cs` - Added notification logic
- `MainWindow.xaml` - Added Settings button
- `App.xaml.cs` - Dispose notification service on exit

---

## Technical Details

### Thread Safety
- `Dictionary<NotificationEventType, DateTime>` protected by lock
- Safe for concurrent refresh calls

### Resource Management
- Notifications auto-expire after 10 seconds
- WindowsNotificationService implements IDisposable
- Disposed in App.OnExit()

### Notification Format
XML-based Windows toast with:
- Title (localized emoji for quick recognition)
- Description (actionable message)
- Auto-close after 10 seconds

Example:
```xml
<toast>
  <visual>
    <binding template="ToastText02">
      <text id="1">🟢 Ollama Online</text>
      <text id="2">Ollama is running at http://localhost:11434</text>
    </binding>
  </visual>
</toast>
```

---

## Future Enhancements

Potential improvements:
1. **Notification history** - Log past notifications
2. **Custom icons** - Per-event notification icons
3. **Sound alerts** - Optional audio for critical events
4. **Recurring alerts** - Re-notify if event persists
5. **Per-event threshold** - Different thresholds per resource type
6. **CLI notification commands** - `ollamamon notify test`

---

## Testing Checklist

- [x] Build succeeds
- [x] Settings window opens/closes
- [x] Checkboxes toggle notification types
- [x] Debounce slider saves value (5-300s)
- [x] Save persists to config file
- [ ] Ollama state changes trigger notifications
- [ ] Models load/unload trigger notifications
- [ ] Resource alerts trigger at thresholds
- [ ] Debounce prevents duplicate notifications
- [ ] Disable toggle turns off all notifications
