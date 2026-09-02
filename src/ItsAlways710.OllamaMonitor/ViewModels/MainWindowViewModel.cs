using System.Collections.ObjectModel;
using ItsAlways710.OllamaMonitor.Configuration;
using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Helpers;
using ItsAlways710.OllamaMonitor.Models;
using ItsAlways710.OllamaMonitor.Ollama;
using ItsAlways710.OllamaMonitor.Services;

namespace ItsAlways710.OllamaMonitor.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly OllamaStatusService _statusService;
    private readonly AppSettingsService _settingsService;
    private readonly DiagnosticsLogService _diagnostics;
    private readonly IOllamaLogService _ollamaLogService;
    private readonly Action<string> _copyToClipboard;
    private readonly Action<string> _openUrl;
    private readonly AutoLaunchService _autoLaunchService;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<string, OllamaModelSnapshot> _modelCache = new();
    private readonly WindowsNotificationService _notificationService;
    private OllamaMonitorSnapshot? _latestSnapshot;
    private OllamaMonitorState? _previousState;

    private string _stateText = "Starting";
    private string _stateForeground = "White";
    private string _endpoint = "http://localhost:11434";
    private string _versionText = "Version: Unavailable";
    private string _appVersionText = "v0.13.0";
    private string _lastCheckedText = "Not checked yet";
    private string _apiReachableText = "API Reachable: Unknown";
    private string _processStatusText = "Process: Detecting";
    private string _cpuText = "CPU: Unavailable";
    private string _memoryText = "Memory: Unavailable";
    private string _privateMemoryText = "Private Memory: Unavailable";
    private string _diskReadText = "Disk Read: Unavailable";
    private string _diskWriteText = "Disk Write: Unavailable";
    private string _gpuText = "GPU: Unavailable";
    private string _compactGpuText = "GPU: Unavailable";
    private string _gpuMemoryText = "VRAM: Unavailable";
    private string _contextText = "Context: Unavailable";
    private IReadOnlyList<MiniContextLine> _miniContextLines = new[] { new MiniContextLine("Context: Unavailable", "Context: Unavailable") };
    private string _modelsSummaryText = "No loaded models.";
    private string _primaryModelText = "Model: No loaded models";
    private string _compactModelsText = "Models: No loaded models";
    private string _pullModelName = string.Empty;
    private string _removeModelName = string.Empty;
    private string _copySourceModelName = string.Empty;
    private string _copyTargetModelName = string.Empty;
    private OllamaModelSnapshot? _selectedModel;
    private string? _errorText;
    private bool _showCpuInMiniMonitor;
    private bool _showMemoryInMiniMonitor;
    private bool _showContextInMiniMonitor;
    private bool _showOllamaLogsInMiniMonitor;
    private bool _isLogsPanelExpanded;

    public MainWindowViewModel(
        OllamaStatusService statusService,
        AppSettingsService settingsService,
        DiagnosticsLogService diagnostics,
        IOllamaLogService ollamaLogService,
        Action<string> copyToClipboard,
        Action<string> openUrl,
        AutoLaunchService autoLaunchService)
    {
        _statusService = statusService;
        _settingsService = settingsService;
        _diagnostics = diagnostics;
        _ollamaLogService = ollamaLogService;
        _copyToClipboard = copyToClipboard;
        _openUrl = openUrl;
        _autoLaunchService = autoLaunchService;
        _notificationService = new WindowsNotificationService(diagnostics);

        Models = new ObservableCollection<OllamaModelSnapshot>();
        OllamaLogLines = new ObservableCollection<string>();

        _ollamaLogService.LogLineReceived += AppendLogLine;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None));
        CopyStatusCommand = new RelayCommand(CopyStatus);
        OpenEndpointCommand = new RelayCommand(OpenEndpoint);
        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        UnloadAllModelsCommand = new AsyncRelayCommand(
            () => UnloadAllModelsAsync(CancellationToken.None),
            () => Models.Count > 0);
        StopSelectedModelCommand = new AsyncRelayCommand(
            () => StopSelectedModelAsync(CancellationToken.None),
            () => SelectedModel is not null);
        PullModelCommand = new AsyncRelayCommand(
            () => PullModelAsync(CancellationToken.None),
            () => !string.IsNullOrWhiteSpace(PullModelName));
        RemoveModelCommand = new AsyncRelayCommand(
            () => RemoveModelAsync(CancellationToken.None),
            () => !string.IsNullOrWhiteSpace(RemoveModelName));
        CopyModelCommand = new AsyncRelayCommand(
            () => CopyModelAsync(CancellationToken.None),
            () => !string.IsNullOrWhiteSpace(CopySourceModelName) && !string.IsNullOrWhiteSpace(CopyTargetModelName));
        StartOllamaCommand = new AsyncRelayCommand(() => StartOllamaAsync(CancellationToken.None));
    }

    public event EventHandler<OllamaMonitorSnapshot>? SnapshotUpdated;

    public ObservableCollection<OllamaModelSnapshot> Models { get; }
    public ObservableCollection<string> OllamaLogLines { get; }

    public bool ShowCpuInMiniMonitor
    {
        get => _showCpuInMiniMonitor;
        private set => SetProperty(ref _showCpuInMiniMonitor, value);
    }

    public bool ShowMemoryInMiniMonitor
    {
        get => _showMemoryInMiniMonitor;
        private set => SetProperty(ref _showMemoryInMiniMonitor, value);
    }

    public bool ShowContextInMiniMonitor
    {
        get => _showContextInMiniMonitor;
        private set => SetProperty(ref _showContextInMiniMonitor, value);
    }

    public bool ShowOllamaLogsInMiniMonitor
    {
        get => _showOllamaLogsInMiniMonitor;
        private set
        {
            if (SetProperty(ref _showOllamaLogsInMiniMonitor, value) && value)
            {
                // Log capture is app-managed (context-window tracking always
                // depends on it); here we only refresh the log panel content.
                _ollamaLogService.Start();
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    OllamaLogLines.Clear();
                    foreach (var line in _ollamaLogService.RecentLines)
                        OllamaLogLines.Add(line);
                    while (OllamaLogLines.Count > 5)
                        OllamaLogLines.RemoveAt(0);
                });
            }
        }
    }

    public bool IsLogsPanelExpanded
    {
        get => _isLogsPanelExpanded;
        set => SetProperty(ref _isLogsPanelExpanded, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand CopyStatusCommand { get; }
    public RelayCommand OpenEndpointCommand { get; }
    public AsyncRelayCommand OpenSettingsCommand { get; }
    public AsyncRelayCommand UnloadAllModelsCommand { get; }
    public AsyncRelayCommand StopSelectedModelCommand { get; }
    public AsyncRelayCommand PullModelCommand { get; }
    public AsyncRelayCommand RemoveModelCommand { get; }
    public AsyncRelayCommand CopyModelCommand { get; }
    public AsyncRelayCommand StartOllamaCommand { get; }

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public string StateForeground
    {
        get => _stateForeground;
        private set => SetProperty(ref _stateForeground, value);
    }

    public string Endpoint
    {
        get => _endpoint;
        private set => SetProperty(ref _endpoint, value);
    }

    public string VersionText
    {
        get => _versionText;
        private set => SetProperty(ref _versionText, value);
    }

    public string AppVersionText
    {
        get => _appVersionText;
        private set => SetProperty(ref _appVersionText, value);
    }

    public string LastCheckedText
    {
        get => _lastCheckedText;
        private set => SetProperty(ref _lastCheckedText, value);
    }

    public string ApiReachableText
    {
        get => _apiReachableText;
        private set => SetProperty(ref _apiReachableText, value);
    }

    public string ProcessStatusText
    {
        get => _processStatusText;
        private set => SetProperty(ref _processStatusText, value);
    }

    public string CpuText
    {
        get => _cpuText;
        private set => SetProperty(ref _cpuText, value);
    }

    public string MemoryText
    {
        get => _memoryText;
        private set => SetProperty(ref _memoryText, value);
    }

    public string PrivateMemoryText
    {
        get => _privateMemoryText;
        private set => SetProperty(ref _privateMemoryText, value);
    }

    public string DiskReadText
    {
        get => _diskReadText;
        private set => SetProperty(ref _diskReadText, value);
    }

    public string DiskWriteText
    {
        get => _diskWriteText;
        private set => SetProperty(ref _diskWriteText, value);
    }

    public string GpuText
    {
        get => _gpuText;
        private set => SetProperty(ref _gpuText, value);
    }

    public string CompactGpuText
    {
        get => _compactGpuText;
        private set => SetProperty(ref _compactGpuText, value);
    }

    public string GpuMemoryText
    {
        get => _gpuMemoryText;
        private set => SetProperty(ref _gpuMemoryText, value);
    }

    public string ContextText
    {
        get => _contextText;
        private set => SetProperty(ref _contextText, value);
    }

    public IReadOnlyList<MiniContextLine> MiniContextLines
    {
        get => _miniContextLines;
        private set => SetProperty(ref _miniContextLines, value);
    }

    public string ModelsSummaryText
    {
        get => _modelsSummaryText;
        private set => SetProperty(ref _modelsSummaryText, value);
    }

    public string PrimaryModelText
    {
        get => _primaryModelText;
        private set => SetProperty(ref _primaryModelText, value);
    }

    public string CompactModelsText
    {
        get => _compactModelsText;
        private set => SetProperty(ref _compactModelsText, value);
    }

    public string PullModelName
    {
        get => _pullModelName;
        set
        {
            if (SetProperty(ref _pullModelName, value))
            {
                PullModelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RemoveModelName
    {
        get => _removeModelName;
        set
        {
            if (SetProperty(ref _removeModelName, value))
            {
                RemoveModelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CopySourceModelName
    {
        get => _copySourceModelName;
        set
        {
            if (SetProperty(ref _copySourceModelName, value))
            {
                CopyModelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CopyTargetModelName
    {
        get => _copyTargetModelName;
        set
        {
            if (SetProperty(ref _copyTargetModelName, value))
            {
                CopyModelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OllamaModelSnapshot? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value))
            {
                StopSelectedModelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorText
    {
        get => _errorText ?? string.Empty;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(_errorText);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            _diagnostics.IsVerboseEnabled = settings.EnableVerboseLogging;
            _notificationService.SetDebounceSeconds(settings.NotificationDebounceSeconds);
            ShowCpuInMiniMonitor = settings.ShowCpuInMiniMonitor;
            ShowMemoryInMiniMonitor = settings.ShowMemoryInMiniMonitor;
            ShowContextInMiniMonitor = settings.ShowContextInMiniMonitor;
            ShowOllamaLogsInMiniMonitor = settings.ShowOllamaLogsInMiniMonitor;

            var snapshot = await _statusService.GetSnapshotAsync(settings, cancellationToken);
            CheckAndNotifyStateChanges(snapshot, settings);
            ApplySnapshot(snapshot);
            SnapshotUpdated?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException)
        {
            // Shutdown path.
        }
        catch (Exception exception)
        {
            _diagnostics.WriteError("Snapshot refresh failed.", exception);
            ErrorText = exception.Message;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void CheckAndNotifyStateChanges(OllamaMonitorSnapshot snapshot, AppSettings settings)
    {
        if (!settings.EnableNotifications)
            return;

        if (_previousState != snapshot.State)
        {
            if (snapshot.State == OllamaMonitorState.NotReachable || snapshot.State == OllamaMonitorState.Error)
            {
                if ((settings.NotificationEvents & NotificationEventType.OllamaOffline) != 0)
                {
                    _notificationService.ShowNotification(
                        NotificationEventType.OllamaOffline,
                        "🔴 Ollama Offline",
                        $"Ollama is no longer reachable at {snapshot.Endpoint}\nCheck the service and logs.");
                }
            }
            else if (_previousState.HasValue &&
                     (_previousState == OllamaMonitorState.NotReachable || _previousState == OllamaMonitorState.Error))
            {
                if ((settings.NotificationEvents & NotificationEventType.OllamaOnline) != 0)
                {
                    _notificationService.ShowNotification(
                        NotificationEventType.OllamaOnline,
                        "🟢 Ollama Online",
                        $"Ollama is running at {snapshot.Endpoint}");
                }
            }

            _previousState = snapshot.State;
        }

        if (_latestSnapshot is not null)
        {
            var previousModelNames = new HashSet<string>(_latestSnapshot.Models.Select(m => m.Name));
            var currentModelNames = new HashSet<string>(snapshot.Models.Select(m => m.Name));

            foreach (var newModel in currentModelNames.Except(previousModelNames))
            {
                if ((settings.NotificationEvents & NotificationEventType.ModelLoaded) != 0)
                {
                    _notificationService.ShowNotification(
                        NotificationEventType.ModelLoaded,
                        "📦 Model Loaded",
                        $"Model '{newModel}' is now loaded");
                }
            }

            foreach (var unloadedModel in previousModelNames.Except(currentModelNames))
            {
                if ((settings.NotificationEvents & NotificationEventType.ModelUnloaded) != 0)
                {
                    _notificationService.ShowNotification(
                        NotificationEventType.ModelUnloaded,
                        "📭 Model Unloaded",
                        $"Model '{unloadedModel}' has been unloaded");
                }
            }
        }

        if (snapshot.Resources is not null && settings.EnableGpuMetrics)
        {
            if (snapshot.Resources.CpuPercent > settings.HighCpuThresholdPercent)
            {
                if ((settings.NotificationEvents & NotificationEventType.HighCpuUsage) != 0)
                {
                    _notificationService.ShowNotification(
                        NotificationEventType.HighCpuUsage,
                        "⚠️ High CPU Usage",
                        $"CPU usage is at {snapshot.Resources.CpuPercent:F1}% (threshold: {settings.HighCpuThresholdPercent}%)");
                }
            }

            var memoryGb = (snapshot.Resources.MemoryBytes ?? 0) / (1024.0 * 1024.0 * 1024.0);
            if (memoryGb > settings.HighMemoryThresholdGb)
            {
                if ((settings.NotificationEvents & NotificationEventType.HighMemoryUsage) != 0)
                {
                    _notificationService.ShowNotification(
                        NotificationEventType.HighMemoryUsage,
                        "⚠️ High Memory Usage",
                        $"Memory usage is at {memoryGb:F1} GB (threshold: {settings.HighMemoryThresholdGb} GB)");
                }
            }

            if (snapshot.Resources.GpuPercent > settings.HighGpuThresholdPercent)
            {
                if ((settings.NotificationEvents & NotificationEventType.HighGpuUsage) != 0)
                {
                    _notificationService.ShowNotification(
                        NotificationEventType.HighGpuUsage,
                        "⚠️ High GPU Usage",
                        $"GPU usage is at {snapshot.Resources.GpuPercent:F1}% (threshold: {settings.HighGpuThresholdPercent}%)");
                }
            }
        }
    }

    private void ApplySnapshot(OllamaMonitorSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        StateText = StatusTextHelper.GetStateLabel(snapshot.State);
        StateForeground = StatusTextHelper.GetStateForeground(snapshot.State);
        Endpoint = snapshot.Endpoint;
        VersionText = $"Version: {snapshot.Version ?? "Unavailable"}";
        LastCheckedText = snapshot.LastChecked.ToString("yyyy-MM-dd HH:mm:ss");
        ApiReachableText = $"API Reachable: {(snapshot.IsApiReachable ? "Yes" : "No")}";
        ProcessStatusText = $"Process: {snapshot.Resources?.ProcessName ?? "Not detected"}";
        CpuText = $"CPU: {StatusTextHelper.FormatPercent(snapshot.Resources?.CpuPercent)}";
        MemoryText = $"Memory: {StatusTextHelper.FormatBytes(snapshot.Resources?.MemoryBytes)}";
        PrivateMemoryText = $"Private Memory: {StatusTextHelper.FormatBytes(snapshot.Resources?.PrivateMemoryBytes)}";
        DiskReadText = $"Disk Read: {StatusTextHelper.FormatBytesPerSecond(snapshot.Resources?.DiskReadBytesPerSecond)}";
        DiskWriteText = $"Disk Write: {StatusTextHelper.FormatBytesPerSecond(snapshot.Resources?.DiskWriteBytesPerSecond)}";
        CpuText = StatusTextHelper.BuildCpuLine(snapshot.Resources?.CpuPercent, snapshot.Resources?.SystemCpuPercent);
        MemoryText = StatusTextHelper.BuildMemoryLine(snapshot.Resources?.MemoryBytes, snapshot.Resources?.SystemMemoryPercent);
        GpuText = $"GPU: {(snapshot.Resources is null ? "Unavailable" : StatusTextHelper.BuildGpuSummary(snapshot.Resources))}";
        CompactGpuText = $"GPU: {StatusTextHelper.BuildCompactGpuSummary(snapshot.Resources)}";
        GpuMemoryText = $"VRAM: {StatusTextHelper.FormatBytes(snapshot.Resources?.VramUsedBytes)} / {StatusTextHelper.FormatBytes(snapshot.Resources?.VramTotalBytes)}";
        ContextText = $"Context: {StatusTextHelper.BuildContextSummary(snapshot.ContextWindows)}";
        MiniContextLines = StatusTextHelper.BuildMiniModelContextLines(snapshot.ContextWindows);
        ModelsSummaryText = snapshot.Models.Count == 0
            ? "No loaded models."
            : $"{snapshot.Models.Count} loaded model(s).";
        PrimaryModelText = snapshot.Models.Count == 0
            ? "Model: No loaded models"
            : $"Model: {snapshot.Models[0].Name}";
        CompactModelsText = BuildCompactModelsText(snapshot.Models);
        ErrorText = snapshot.ErrorMessage ?? string.Empty;

        var currentModelNames = new List<string>();
        foreach (var newModel in snapshot.Models)
        {
            currentModelNames.Add(newModel.Name);
            GetOrUpdateModel(newModel, snapshot.Resources);
        }

        var currentModelSet = new HashSet<string>(currentModelNames);
        var staleKeys = _modelCache.Keys.Except(currentModelSet).ToList();
        foreach (var staleKey in staleKeys)
        {
            _modelCache.Remove(staleKey);
        }

        var previousSelectedModelName = SelectedModel?.Name;
        Models.Clear();
        foreach (var modelName in currentModelNames)
        {
            if (_modelCache.TryGetValue(modelName, out var cachedModel))
            {
                Models.Add(cachedModel);
            }
        }

        if (previousSelectedModelName is not null)
        {
            SelectedModel = Models.FirstOrDefault(model => model.Name.Equals(previousSelectedModelName, StringComparison.OrdinalIgnoreCase));
        }

        UnloadAllModelsCommand.RaiseCanExecuteChanged();
        StopSelectedModelCommand.RaiseCanExecuteChanged();
        PullModelCommand.RaiseCanExecuteChanged();
        RemoveModelCommand.RaiseCanExecuteChanged();
        CopyModelCommand.RaiseCanExecuteChanged();
    }

    private async Task UnloadAllModelsAsync(CancellationToken cancellationToken)
    {
        var modelsToUnload = Models.Select(model => model.Name).ToArray();
        if (modelsToUnload.Length == 0)
        {
            return;
        }

        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var failures = new List<string>();
            foreach (var modelName in modelsToUnload)
            {
                var result = await _statusService.UnloadModelAsync(settings, modelName, cancellationToken);
                if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    failures.Add(result.ErrorMessage);
                    ShowOperationNotification(
                        settings,
                        NotificationEventType.ModelOperationFailed,
                        "❌ Stop Failed",
                        $"Could not stop '{modelName}'. {result.ErrorMessage}");
                }
                else
                {
                    ShowOperationNotification(
                        settings,
                        NotificationEventType.ModelOperationSucceeded,
                        "✅ Model Stopped",
                        $"Stopped '{modelName}'.");
                }
            }

            if (failures.Count > 0)
            {
                ErrorText = string.Join(" | ", failures.Distinct());
            }
            else
            {
                ErrorText = string.Empty;
            }

            await VerifyStoppedModelsAsync(settings, modelsToUnload, cancellationToken);
            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown path.
        }
        catch (Exception exception)
        {
            _diagnostics.WriteError("Unload models request failed.", exception);
            ErrorText = exception.Message;
        }
    }

    private async Task StopSelectedModelAsync(CancellationToken cancellationToken)
    {
        if (SelectedModel is null)
        {
            return;
        }

        var modelName = SelectedModel.Name;
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var result = await _statusService.UnloadModelAsync(settings, modelName, cancellationToken);
            if (!result.IsSuccess)
            {
                ErrorText = result.ErrorMessage ?? $"Unable to stop '{modelName}'.";
                ShowOperationNotification(
                    settings,
                    NotificationEventType.ModelOperationFailed,
                    "❌ Stop Failed",
                    $"Could not stop '{modelName}'. {result.ErrorMessage}");
                return;
            }

            ErrorText = string.Empty;
            ShowOperationNotification(
                settings,
                NotificationEventType.ModelOperationSucceeded,
                "✅ Model Stopped",
                $"Stopped '{modelName}'.");
            await VerifyStoppedModelsAsync(settings, [modelName], cancellationToken);
            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown path.
        }
        catch (Exception exception)
        {
            _diagnostics.WriteError("Stop selected model request failed.", exception);
            ErrorText = exception.Message;
        }
    }

    private async Task PullModelAsync(CancellationToken cancellationToken)
    {
        var modelName = PullModelName.Trim();
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return;
        }

        await RunModelOperationAsync(
            cancellationToken,
            settings => _statusService.PullModelAsync(settings, modelName, cancellationToken),
            $"Pulled '{modelName}'.",
            $"Pull failed for '{modelName}'.");
    }

    private async Task RemoveModelAsync(CancellationToken cancellationToken)
    {
        var modelName = RemoveModelName.Trim();
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return;
        }

        await RunModelOperationAsync(
            cancellationToken,
            settings => _statusService.RemoveModelAsync(settings, modelName, cancellationToken),
            $"Removed '{modelName}'.",
            $"Remove failed for '{modelName}'.");
    }

    private async Task CopyModelAsync(CancellationToken cancellationToken)
    {
        var sourceModel = CopySourceModelName.Trim();
        var targetModel = CopyTargetModelName.Trim();
        if (string.IsNullOrWhiteSpace(sourceModel) || string.IsNullOrWhiteSpace(targetModel))
        {
            return;
        }

        await RunModelOperationAsync(
            cancellationToken,
            settings => _statusService.CopyModelAsync(settings, sourceModel, targetModel, cancellationToken),
            $"Copied '{sourceModel}' to '{targetModel}'.",
            $"Copy failed for '{sourceModel}' to '{targetModel}'.");
    }

    private async Task StartOllamaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var result = _statusService.StartOllama(settings);
            if (!result.IsSuccess)
            {
                ErrorText = result.ErrorMessage ?? "Could not start Ollama.";
                ShowOperationNotification(
                    settings,
                    NotificationEventType.ModelOperationFailed,
                    "❌ Ollama Start Failed",
                    ErrorText);
                return;
            }

            ErrorText = string.Empty;
            ShowOperationNotification(
                settings,
                NotificationEventType.OllamaStarted,
                "🚀 Ollama Start Requested",
                "Ollama daemon start command was sent.");
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown path.
        }
        catch (Exception exception)
        {
            _diagnostics.WriteError("Start Ollama request failed.", exception);
            ErrorText = exception.Message;
        }
    }

    private async Task RunModelOperationAsync(
        CancellationToken cancellationToken,
        Func<AppSettings, Task<OllamaApiCallResult<bool>>> operation,
        string successMessage,
        string failureMessage)
    {
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var result = await operation(settings);
            if (!result.IsSuccess)
            {
                ErrorText = string.IsNullOrWhiteSpace(result.ErrorMessage) ? failureMessage : result.ErrorMessage;
                ShowOperationNotification(
                    settings,
                    NotificationEventType.ModelOperationFailed,
                    "❌ Model Operation Failed",
                    ErrorText);
                return;
            }

            ErrorText = string.Empty;
            ShowOperationNotification(
                settings,
                NotificationEventType.ModelOperationSucceeded,
                "✅ Model Operation Completed",
                successMessage);
            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown path.
        }
        catch (Exception exception)
        {
            _diagnostics.WriteError("Model operation failed.", exception);
            ErrorText = exception.Message;
        }
    }

    private async Task VerifyStoppedModelsAsync(
        AppSettings settings,
        IReadOnlyCollection<string> requestedModels,
        CancellationToken cancellationToken)
    {
        var remainingModels = new HashSet<string>(requestedModels, StringComparer.OrdinalIgnoreCase);
        if (remainingModels.Count == 0)
        {
            return;
        }

        for (var attempt = 0; attempt < 5 && remainingModels.Count > 0; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

            var runningResult = await _statusService.GetRunningModelNamesAsync(settings, cancellationToken);
            if (!runningResult.IsSuccess || runningResult.Value is null)
            {
                continue;
            }

            remainingModels.IntersectWith(runningResult.Value);
        }

        if (remainingModels.Count > 0)
        {
            ErrorText = $"Still running after stop request: {string.Join(", ", remainingModels)}";
        }
    }

    private void ShowOperationNotification(
        AppSettings settings,
        NotificationEventType eventType,
        string title,
        string message)
    {
        if (!settings.EnableNotifications)
        {
            return;
        }

        if ((settings.NotificationEvents & eventType) == 0)
        {
            return;
        }

        _notificationService.ShowNotification(eventType, title, message);
    }

    private OllamaModelSnapshot GetOrUpdateModel(OllamaModelSnapshot newModel, ResourceSnapshot? resources)
    {
        if (_modelCache.TryGetValue(newModel.Name, out var existingModel))
        {
            return existingModel;
        }

        _modelCache[newModel.Name] = newModel;
        return newModel;
    }

    private void CopyStatus()
    {
        if (_latestSnapshot is null)
        {
            return;
        }

        _copyToClipboard(SnapshotFormatter.ToMultilineStatus(_latestSnapshot));
    }

    private void OpenEndpoint()
    {
        if (_latestSnapshot is null)
        {
            return;
        }

        _openUrl(_latestSnapshot.Endpoint);
    }

    private async Task OpenSettingsAsync()
    {
        var settings = await _settingsService.LoadAsync(CancellationToken.None);
        var viewModel = new SettingsWindowViewModel(settings, _settingsService, _autoLaunchService);
        var settingsWindow = new SettingsWindow { DataContext = viewModel };

        if (settingsWindow.ShowDialog() == true)
        {
            await viewModel.SaveAsync(CancellationToken.None);
            _diagnostics.WriteInfo("Settings saved successfully.");
            await RefreshAsync(CancellationToken.None);
        }
    }

    private static string BuildCompactModelsText(IReadOnlyList<OllamaModelSnapshot> models)
    {
        if (models.Count == 0)
        {
            return "Models: No loaded models";
        }

        var displayedModels = models
            .Take(3)
            .Select(model => model.Name)
            .ToList();

        if (models.Count > 3)
        {
            displayedModels.Add($"+{models.Count - 3} more");
        }

        return $"Models: {string.Join(Environment.NewLine, displayedModels)}";
    }

    public void Dispose()
    {
        _ollamaLogService.LogLineReceived -= AppendLogLine;
        _notificationService?.Dispose();
    }

    private void AppendLogLine(string line)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            OllamaLogLines.Add(line.Trim());
            while (OllamaLogLines.Count > 5)
                OllamaLogLines.RemoveAt(0);
        });
    }
}
