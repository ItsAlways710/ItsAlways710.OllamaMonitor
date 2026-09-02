using ItsAlways710.OllamaMonitor.Configuration;
using ItsAlways710.OllamaMonitor.Models;
using ItsAlways710.OllamaMonitor.Services;

namespace ItsAlways710.OllamaMonitor.ViewModels;

public sealed class SettingsWindowViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly AppSettingsService _settingsService;
    private readonly AutoLaunchService _autoLaunchService;

    private bool _enableNotifications;
    private bool _notifyOllamaOffline;
    private bool _notifyOllamaOnline;
    private bool _notifyModelLoaded;
    private bool _notifyModelUnloaded;
    private bool _notifyHighCpu;
    private bool _notifyHighMemory;
    private bool _notifyHighGpu;
    private bool _notifyModelOperationSucceeded;
    private bool _notifyModelOperationFailed;
    private bool _notifyOllamaStarted;
    private int _notificationDebounceSeconds;
    private bool _showFloatingWindowOnStart;
    private bool _launchAtWindowsStartup;
    private ModelUnloadStrategy _selectedUnloadStrategy;
    private bool _showCpuInMiniMonitor;
    private bool _showMemoryInMiniMonitor;
    private bool _showContextInMiniMonitor;
    private bool _showOllamaLogsInMiniMonitor;
    private bool _enableVerboseLogging;

    public SettingsWindowViewModel(AppSettings settings, AppSettingsService settingsService, AutoLaunchService autoLaunchService)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _autoLaunchService = autoLaunchService ?? throw new ArgumentNullException(nameof(autoLaunchService));

        LoadSettings();
    }

    public bool EnableNotifications
    {
        get => _enableNotifications;
        set => SetProperty(ref _enableNotifications, value);
    }

    public bool NotifyOllamaOffline
    {
        get => _notifyOllamaOffline;
        set => SetProperty(ref _notifyOllamaOffline, value);
    }

    public bool NotifyOllamaOnline
    {
        get => _notifyOllamaOnline;
        set => SetProperty(ref _notifyOllamaOnline, value);
    }

    public bool NotifyModelLoaded
    {
        get => _notifyModelLoaded;
        set => SetProperty(ref _notifyModelLoaded, value);
    }

    public bool NotifyModelUnloaded
    {
        get => _notifyModelUnloaded;
        set => SetProperty(ref _notifyModelUnloaded, value);
    }

    public bool NotifyHighCpu
    {
        get => _notifyHighCpu;
        set => SetProperty(ref _notifyHighCpu, value);
    }

    public bool NotifyHighMemory
    {
        get => _notifyHighMemory;
        set => SetProperty(ref _notifyHighMemory, value);
    }

    public bool NotifyHighGpu
    {
        get => _notifyHighGpu;
        set => SetProperty(ref _notifyHighGpu, value);
    }

    public bool NotifyModelOperationSucceeded
    {
        get => _notifyModelOperationSucceeded;
        set => SetProperty(ref _notifyModelOperationSucceeded, value);
    }

    public bool NotifyModelOperationFailed
    {
        get => _notifyModelOperationFailed;
        set => SetProperty(ref _notifyModelOperationFailed, value);
    }

    public bool NotifyOllamaStarted
    {
        get => _notifyOllamaStarted;
        set => SetProperty(ref _notifyOllamaStarted, value);
    }

    public int NotificationDebounceSeconds
    {
        get => _notificationDebounceSeconds;
        set => SetProperty(ref _notificationDebounceSeconds, Math.Max(5, value));
    }

    public bool ShowFloatingWindowOnStart
    {
        get => _showFloatingWindowOnStart;
        set => SetProperty(ref _showFloatingWindowOnStart, value);
    }

    public bool LaunchAtWindowsStartup
    {
        get => _launchAtWindowsStartup;
        set => SetProperty(ref _launchAtWindowsStartup, value);
    }

    public bool ShowCpuInMiniMonitor
    {
        get => _showCpuInMiniMonitor;
        set => SetProperty(ref _showCpuInMiniMonitor, value);
    }

    public bool ShowMemoryInMiniMonitor
    {
        get => _showMemoryInMiniMonitor;
        set => SetProperty(ref _showMemoryInMiniMonitor, value);
    }

    public bool ShowContextInMiniMonitor
    {
        get => _showContextInMiniMonitor;
        set => SetProperty(ref _showContextInMiniMonitor, value);
    }

    public bool ShowOllamaLogsInMiniMonitor
    {
        get => _showOllamaLogsInMiniMonitor;
        set => SetProperty(ref _showOllamaLogsInMiniMonitor, value);
    }

    public bool EnableVerboseLogging
    {
        get => _enableVerboseLogging;
        set => SetProperty(ref _enableVerboseLogging, value);
    }

    public IReadOnlyList<ModelUnloadStrategy> UnloadStrategies { get; } = Enum.GetValues<ModelUnloadStrategy>();

    public ModelUnloadStrategy SelectedUnloadStrategy
    {
        get => _selectedUnloadStrategy;
        set => SetProperty(ref _selectedUnloadStrategy, value);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        var notificationEvents = BuildNotificationFlags();

        var updatedSettings = _settings with
        {
            EnableNotifications = EnableNotifications,
            NotificationEvents = notificationEvents,
            NotificationDebounceSeconds = NotificationDebounceSeconds,
            ShowFloatingWindowOnStart = ShowFloatingWindowOnStart,
            UnloadStrategy = SelectedUnloadStrategy,
            ShowCpuInMiniMonitor = ShowCpuInMiniMonitor,
            ShowMemoryInMiniMonitor = ShowMemoryInMiniMonitor,
            ShowContextInMiniMonitor = ShowContextInMiniMonitor,
            ShowOllamaLogsInMiniMonitor = ShowOllamaLogsInMiniMonitor,
            EnableVerboseLogging = EnableVerboseLogging
        };
        await _settingsService.SaveAsync(updatedSettings, cancellationToken);

        // The Run-key registration is applied here (idempotent; the save path
        // may call SaveAsync twice) and is NOT mirrored into settings.json —
        // the registry entry itself is the setting.
        _autoLaunchService.SetEnabled(LaunchAtWindowsStartup);
    }

    private void LoadSettings()
    {
        EnableNotifications = _settings.EnableNotifications;
        NotificationDebounceSeconds = _settings.NotificationDebounceSeconds;
        ShowFloatingWindowOnStart = _settings.ShowFloatingWindowOnStart;

        // Live state from the registry, so a registration removed externally
        // (Task Manager Startup apps, etc.) is reflected here on open.
        LaunchAtWindowsStartup = _autoLaunchService.IsEnabled();
        SelectedUnloadStrategy = _settings.UnloadStrategy;

        NotifyOllamaOffline = (_settings.NotificationEvents & NotificationEventType.OllamaOffline) != 0;
        NotifyOllamaOnline = (_settings.NotificationEvents & NotificationEventType.OllamaOnline) != 0;
        NotifyModelLoaded = (_settings.NotificationEvents & NotificationEventType.ModelLoaded) != 0;
        NotifyModelUnloaded = (_settings.NotificationEvents & NotificationEventType.ModelUnloaded) != 0;
        NotifyHighCpu = (_settings.NotificationEvents & NotificationEventType.HighCpuUsage) != 0;
        NotifyHighMemory = (_settings.NotificationEvents & NotificationEventType.HighMemoryUsage) != 0;
        NotifyHighGpu = (_settings.NotificationEvents & NotificationEventType.HighGpuUsage) != 0;
        NotifyModelOperationSucceeded = (_settings.NotificationEvents & NotificationEventType.ModelOperationSucceeded) != 0;
        NotifyModelOperationFailed = (_settings.NotificationEvents & NotificationEventType.ModelOperationFailed) != 0;
        NotifyOllamaStarted = (_settings.NotificationEvents & NotificationEventType.OllamaStarted) != 0;

        ShowCpuInMiniMonitor = _settings.ShowCpuInMiniMonitor;
        ShowMemoryInMiniMonitor = _settings.ShowMemoryInMiniMonitor;
        ShowContextInMiniMonitor = _settings.ShowContextInMiniMonitor;
        ShowOllamaLogsInMiniMonitor = _settings.ShowOllamaLogsInMiniMonitor;
        EnableVerboseLogging = _settings.EnableVerboseLogging;
    }

    private NotificationEventType BuildNotificationFlags()
    {
        var flags = NotificationEventType.None;

        if (NotifyOllamaOffline)
            flags |= NotificationEventType.OllamaOffline;
        if (NotifyOllamaOnline)
            flags |= NotificationEventType.OllamaOnline;
        if (NotifyModelLoaded)
            flags |= NotificationEventType.ModelLoaded;
        if (NotifyModelUnloaded)
            flags |= NotificationEventType.ModelUnloaded;
        if (NotifyHighCpu)
            flags |= NotificationEventType.HighCpuUsage;
        if (NotifyHighMemory)
            flags |= NotificationEventType.HighMemoryUsage;
        if (NotifyHighGpu)
            flags |= NotificationEventType.HighGpuUsage;
        if (NotifyModelOperationSucceeded)
            flags |= NotificationEventType.ModelOperationSucceeded;
        if (NotifyModelOperationFailed)
            flags |= NotificationEventType.ModelOperationFailed;
        if (NotifyOllamaStarted)
            flags |= NotificationEventType.OllamaStarted;

        return flags;
    }

    public void Dispose()
    {
        // Cleanup if needed in the future
    }
}
