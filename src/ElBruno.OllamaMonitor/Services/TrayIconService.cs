using System.Drawing;
using System.Windows.Forms;
using ElBruno.OllamaMonitor.Configuration;
using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Helpers;
using ElBruno.OllamaMonitor.Models;
using ElBruno.OllamaMonitor.ViewModels;

namespace ElBruno.OllamaMonitor.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly MiniMonitorWindow _miniMonitorWindow;
    private readonly MainWindowViewModel _viewModel;
    private readonly AppSettingsService _settingsService;
    private readonly DiagnosticsLogService _diagnostics;
    private readonly Action _exitAction;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleDetailsWindowMenuItem;
    private readonly ToolStripMenuItem _toggleMiniWindowMenuItem;
    private readonly IReadOnlyDictionary<OllamaMonitorState, Icon> _trayIcons;

    public TrayIconService(
        MainWindow mainWindow,
        MiniMonitorWindow miniMonitorWindow,
        MainWindowViewModel viewModel,
        AppSettingsService settingsService,
        DiagnosticsLogService diagnostics,
        Action exitAction)
    {
        _mainWindow = mainWindow;
        _miniMonitorWindow = miniMonitorWindow;
        _viewModel = viewModel;
        _settingsService = settingsService;
        _diagnostics = diagnostics;
        _exitAction = exitAction;
        _trayIcons = LoadTrayIcons(diagnostics);

        _toggleDetailsWindowMenuItem = new ToolStripMenuItem("Show Details", null, (_, _) => ToggleDetailsWindowVisibility());
        _toggleMiniWindowMenuItem = new ToolStripMenuItem("Show Mini Monitor", null, (_, _) => ToggleMiniWindowVisibility());
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Ollama: Starting",
            Icon = _trayIcons[OllamaMonitorState.NotReachable],
            ContextMenuStrip = new ContextMenuStrip()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMiniMonitorWindow();
        _notifyIcon.ContextMenuStrip.Opening += (_, _) => RefreshMenuText();
        _notifyIcon.ContextMenuStrip.Items.AddRange(
        [
            _toggleDetailsWindowMenuItem,
            _toggleMiniWindowMenuItem,
            new ToolStripMenuItem("Settings…", null, (_, _) => _viewModel.OpenSettingsCommand.Execute(null)),
            new ToolStripMenuItem("Refresh", null, async (_, _) => await _viewModel.RefreshAsync(CancellationToken.None)),
            new ToolStripMenuItem("Copy Status", null, (_, _) => _viewModel.CopyStatusCommand.Execute(null)),
            new ToolStripMenuItem("Open Ollama API", null, (_, _) => _viewModel.OpenEndpointCommand.Execute(null)),
            new ToolStripMenuItem("Open Config Folder", null, (_, _) => ProcessLauncher.Open(AppPaths.RootDirectory, _diagnostics)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Visit HomePage", null, (_, _) => OpenGitHubRepository()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => _exitAction())
        ]);

        _viewModel.SnapshotUpdated += (_, snapshot) => ApplySnapshot(snapshot);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        foreach (var icon in _trayIcons.Values)
        {
            icon.Dispose();
        }
    }

    private void ApplySnapshot(OllamaMonitorSnapshot snapshot)
    {
        _notifyIcon.Icon = _trayIcons.TryGetValue(snapshot.State, out var icon)
            ? icon
            : _trayIcons[OllamaMonitorState.NotReachable];
        _notifyIcon.Text = StatusTextHelper.BuildTooltip(snapshot);
    }

    private void ToggleDetailsWindowVisibility()
    {
        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
            return;
        }

        ShowWindow();
    }

    private void ToggleMiniWindowVisibility()
    {
        if (_miniMonitorWindow.IsVisible)
        {
            _miniMonitorWindow.Hide();
            return;
        }

        ShowMiniMonitorWindow();
    }

    private void ShowWindow()
    {
        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
        {
            _mainWindow.WindowState = System.Windows.WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void ShowMiniMonitorWindow()
    {
        if (!_miniMonitorWindow.IsVisible)
        {
            _miniMonitorWindow.Show();
        }

        _miniMonitorWindow.Activate();
    }

    private void RefreshMenuText()
    {
        _toggleDetailsWindowMenuItem.Text = _mainWindow.IsVisible ? "Hide Details" : "Show Details";
        _toggleMiniWindowMenuItem.Text = _miniMonitorWindow.IsVisible ? "Hide Mini Monitor" : "Show Mini Monitor";
    }

    private void OpenGitHubRepository()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/elbruno/ElBruno.OllamaMonitor",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _diagnostics.WriteError("Failed to open GitHub repository.", ex);
        }
    }

    private static IReadOnlyDictionary<OllamaMonitorState, Icon> LoadTrayIcons(DiagnosticsLogService diagnostics)
    {
        var iconDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcons");

        return new Dictionary<OllamaMonitorState, Icon>
        {
            [OllamaMonitorState.NotReachable] = LoadTrayIcon(iconDirectory, "tray-gray.ico", SystemIcons.Error, diagnostics),
            [OllamaMonitorState.Running] = LoadTrayIcon(iconDirectory, "tray-green.ico", SystemIcons.Information, diagnostics),
            [OllamaMonitorState.ModelLoaded] = LoadTrayIcon(iconDirectory, "tray-blue.ico", SystemIcons.Shield, diagnostics),
            [OllamaMonitorState.HighUsage] = LoadTrayIcon(iconDirectory, "tray-orange.ico", SystemIcons.Warning, diagnostics),
            [OllamaMonitorState.Error] = LoadTrayIcon(iconDirectory, "tray-red.ico", SystemIcons.Error, diagnostics)
        };
    }

    private static Icon LoadTrayIcon(string iconDirectory, string fileName, Icon fallback, DiagnosticsLogService diagnostics)
    {
        var iconPath = Path.Combine(iconDirectory, fileName);

        if (!File.Exists(iconPath))
        {
            diagnostics.WriteInfo($"Tray icon asset not found. Using fallback icon for {fileName}.");
            return (Icon)fallback.Clone();
        }

        using var stream = File.OpenRead(iconPath);
        return new Icon(stream);
    }
}
