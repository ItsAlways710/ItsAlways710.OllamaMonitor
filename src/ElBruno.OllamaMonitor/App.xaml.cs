using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using ElBruno.OllamaMonitor.Cli;
using ElBruno.OllamaMonitor.Configuration;
using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Helpers;
using ElBruno.OllamaMonitor.Ollama;
using ElBruno.OllamaMonitor.Services;
using ElBruno.OllamaMonitor.ViewModels;

namespace ElBruno.OllamaMonitor;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly CliCommandParser _commandParser = new();
    private AppSettingsService? _settingsService;
    private DiagnosticsLogService? _diagnostics;
    private OllamaLogService? _ollamaLogService;
    private ContextTrackingService? _contextTrackingService;
    private HttpClient? _httpClient;
    private DispatcherTimer? _refreshTimer;
    private TrayIconService? _trayIconService;
    private MainWindow? _mainWindow;
    private MiniMonitorWindow? _miniMonitorWindow;
    private MainWindowViewModel? _mainWindowViewModel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsService = new AppSettingsService();
        _diagnostics = new DiagnosticsLogService(AppPaths.LogsDirectoryPath);
        RegisterGlobalExceptionHandlers(_diagnostics);

        // Initialize theme
        var savedTheme = ThemeService.GetSavedThemePreference();
        ThemeService.ApplyTheme(savedTheme);

        _diagnostics.WriteInfo("Application startup.");

        var command = _commandParser.Parse(e.Args);
        if (!command.ShouldLaunchApp)
        {
            var exitCode = await new CliCommandRunner(_settingsService, _diagnostics).RunAsync(command, CancellationToken.None);
            Shutdown(exitCode);
            return;
        }

        await LaunchTrayApplicationAsync(_settingsService, _diagnostics, _shutdownTokenSource.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindowViewModel?.Dispose();
        _contextTrackingService?.Dispose();
        _ollamaLogService?.Dispose();
        _trayIconService?.Dispose();
        _mainWindow?.PrepareForExit();
        _mainWindow?.Close();
        _miniMonitorWindow?.PrepareForExit();
        _miniMonitorWindow?.Close();
        _httpClient?.Dispose();
        _shutdownTokenSource.Cancel();
        _shutdownTokenSource.Dispose();
        _diagnostics?.WriteInfo("Application exit.");
        base.OnExit(e);
    }

    private async Task LaunchTrayApplicationAsync(AppSettingsService settingsService, DiagnosticsLogService diagnostics, CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var ollamaClient = new OllamaClient(_httpClient, diagnostics);
        _ollamaLogService = new OllamaLogService(diagnostics);
        // Context-window tracking depends on log capture, so it runs regardless of
        // the "Show Ollama logs in Mini Monitor" toggle.
        _ollamaLogService.Start();
        _contextTrackingService = new ContextTrackingService(_ollamaLogService);
        var ollamaCliService = new OllamaCliService(diagnostics, _ollamaLogService);
        var processMetricsService = new ProcessMetricsService(diagnostics);
        var gpuMetricsService = new NvidiaSmiMetricsService(diagnostics);
        var statusService = new OllamaStatusService(ollamaClient, ollamaCliService, processMetricsService, gpuMetricsService, _contextTrackingService, diagnostics);

        _mainWindow = new MainWindow();
        _miniMonitorWindow = new MiniMonitorWindow();
        _mainWindowViewModel = new MainWindowViewModel(
            statusService,
            settingsService,
            diagnostics,
            _ollamaLogService,
            () => _mainWindow.Hide(),
            text => System.Windows.Clipboard.SetText(text),
            url => ProcessLauncher.Open(url, diagnostics));

        _mainWindow.DataContext = _mainWindowViewModel;
        _miniMonitorWindow.DataContext = _mainWindowViewModel;

        _trayIconService = new TrayIconService(
            _mainWindow,
            _miniMonitorWindow,
            _mainWindowViewModel,
            settingsService,
            diagnostics,
            ExitApplication);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(1, settings.RefreshIntervalSeconds))
        };

        _refreshTimer.Tick += async (_, _) =>
        {
            if (_mainWindowViewModel is not null)
            {
                await _mainWindowViewModel.RefreshAsync(cancellationToken);
            }
        };

        if (settings.ShowFloatingWindowOnStart && !settings.StartMinimizedToTray)
        {
            _mainWindow.Show();
        }

        await _mainWindowViewModel.RefreshAsync(cancellationToken);
        _refreshTimer.Start();
    }

    private void ExitApplication()
    {
        _mainWindow?.PrepareForExit();
        Shutdown();
    }


    private void RegisterGlobalExceptionHandlers(DiagnosticsLogService diagnostics)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            diagnostics.WriteError("Unhandled dispatcher exception.", args.Exception);
            args.Handled = true;
        };
        // WinForms surfaces unhandled handler exceptions (e.g. tray menu
        // clicks) via this event instead of the default .NET dialog; route
        // them to the diagnostics log.
        System.Windows.Forms.Application.ThreadException += (_, args) =>
        {
            diagnostics.WriteError("Unhandled WinForms thread exception.", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            diagnostics.WriteError("Unhandled app domain exception.", args.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            diagnostics.WriteError("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }
}
