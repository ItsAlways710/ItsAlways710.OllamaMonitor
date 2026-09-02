using System.Drawing;
using System.Collections;
using System.Resources;
using System.Reflection;
using System.Windows.Forms;
using ItsAlways710.OllamaMonitor.Configuration;
using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Helpers;
using ItsAlways710.OllamaMonitor.Models;
using ItsAlways710.OllamaMonitor.ViewModels;

namespace ItsAlways710.OllamaMonitor.Services;

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
                FileName = "https://github.com/ItsAlways710/ItsAlways710.OllamaMonitor",
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
        var assembly = typeof(TrayIconService).Assembly;
        var iconDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcons");

        return new Dictionary<OllamaMonitorState, Icon>
        {
            [OllamaMonitorState.NotReachable] = LoadTrayIcon(assembly, iconDirectory, "tray-gray.ico", SystemIcons.Error, diagnostics),
            [OllamaMonitorState.Running] = LoadTrayIcon(assembly, iconDirectory, "tray-green.ico", SystemIcons.Information, diagnostics),
            [OllamaMonitorState.ModelLoaded] = LoadTrayIcon(assembly, iconDirectory, "tray-blue.ico", SystemIcons.Shield, diagnostics),
            [OllamaMonitorState.HighUsage] = LoadTrayIcon(assembly, iconDirectory, "tray-orange.ico", SystemIcons.Warning, diagnostics),
            [OllamaMonitorState.Error] = LoadTrayIcon(assembly, iconDirectory, "tray-red.ico", SystemIcons.Error, diagnostics)
        };
    }

    private static Icon LoadTrayIcon(Assembly assembly, string iconDirectory, string fileName, Icon fallback, DiagnosticsLogService diagnostics)
    {
        // Prefer the embedded copy: this is the only copy that survives a
        // single-file publish, where WPF's content-file fallback crashes.
        var embedded = LoadEmbeddedIcon(assembly, fileName);
        if (embedded is not null)
        {
            return embedded;
        }

        // Legacy sidecar file next to the executable.
        var iconPath = Path.Combine(iconDirectory, fileName);
        if (File.Exists(iconPath))
        {
            using var stream = File.OpenRead(iconPath);
            return new Icon(stream);
        }

        diagnostics.WriteInfo($"Tray icon asset not found. Using fallback icon for {fileName}.");
        return (Icon)fallback.Clone();
    }

    private static Icon? LoadEmbeddedIcon(Assembly assembly, string fileName)
    {
        // WPF bakes <Resource> items as named entries inside the assembly's
        // .resources blob (e.g. "assets/trayicons/tray-green.ico"). Locate
        // the entry by key so the lookup does not depend on blob naming.
        try
        {
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                using var blob = assembly.GetManifestResourceStream(resourceName);
                if (blob is null)
                {
                    continue;
                }

                using var reader = new ResourceReader(blob);
                foreach (DictionaryEntry entry in (IEnumerable)reader)
                {
                    var key = entry.Key as string ?? string.Empty;
                    if (!string.Equals(key, "assets/trayicons/" + fileName, StringComparison.OrdinalIgnoreCase)
                        && !key.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (CreateIcon(entry.Value) is { } embedded)
                    {
                        return embedded;
                    }

                    break;
                }
            }
        }
        catch
        {
            // Let the caller fall through to the other sources.
        }

        return null;
    }

    private static Icon? CreateIcon(object? value)
    {
        if (value is Stream data)
        {
            using var buffer = new MemoryStream();
            data.CopyTo(buffer);
            buffer.Position = 0;
            return new Icon(buffer);
        }

        if (value is byte[] bytes)
        {
            return new Icon(new MemoryStream(bytes, writable: false));
        }

        return null;
    }
}
