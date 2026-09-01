using ItsAlways710.OllamaMonitor.Diagnostics;

namespace ItsAlways710.OllamaMonitor.Services;

/// <summary>
/// Reads/writes/removes the app's "launch at sign-in" registration
/// (the per-user Run key). The registry value is the single source of
/// truth for this setting; settings.json intentionally holds no copy of it.
/// </summary>
public sealed class AutoLaunchService
{
    private readonly DiagnosticsLogService _diagnostics;
    private readonly IStartupRegistryStore _store;

    public AutoLaunchService(DiagnosticsLogService diagnostics)
        : this(diagnostics, new RunKeyRegistryStore())
    {
    }

    public AutoLaunchService(DiagnosticsLogService diagnostics, IStartupRegistryStore store)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// The exact command the app registers under (quoted executable path),
    /// or null when the current process path is unavailable.
    /// </summary>
    public string? ExpectedCommand
    {
        get
        {
            var path = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(path) ? null : $"\"{path}\"";
        }
    }

    /// <summary>
    /// True when the registered command currently matches this app's executable.
    /// A stale entry (e.g. the app was moved) counts as unregistered; the next
    /// <see cref="SetEnabled"/> call rewrites it.
    /// </summary>
    public bool IsEnabled()
    {
        var expected = ExpectedCommand;
        if (expected is null)
        {
            return false;
        }

        try
        {
            return string.Equals(_store.ReadCommand(), expected, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _diagnostics.WriteError("Failed to read startup registration.", ex);
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            Register();
        }
        else
        {
            Unregister();
        }
    }

    private void Register()
    {
        var expected = ExpectedCommand;
        if (expected is null)
        {
            _diagnostics.WriteError("Cannot register at Windows startup: executable path is unavailable.");
            return;
        }

        try
        {
            _store.WriteCommand(expected);
            _diagnostics.WriteInfo($"Registered at Windows startup ({expected}).");
        }
        catch (Exception ex)
        {
            _diagnostics.WriteError("Failed to register at Windows startup.", ex);
        }
    }

    private void Unregister()
    {
        try
        {
            _store.DeleteCommand();
            _diagnostics.WriteInfo("Removed Windows startup registration.");
        }
        catch (Exception ex)
        {
            _diagnostics.WriteError("Failed to remove Windows startup registration.", ex);
        }
    }
}

/// <summary>
/// Minimal abstraction over the Run-key value so the registration logic
/// can be exercised in tests without touching the real registry.
/// </summary>
public interface IStartupRegistryStore
{
    string? ReadCommand();
    void WriteCommand(string command);
    void DeleteCommand();
}

/// <summary>
/// Per-user Run key entry: HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Listed by name in Task Manager's Startup apps and Settings → Startup.
/// </summary>
public sealed class RunKeyRegistryStore : IStartupRegistryStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "OllamaMonitor";

    public string? ReadCommand()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) as string;
    }

    public void WriteCommand(string command)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The Run registry key could not be opened for writing.");
        key.SetValue(RunValueName, command);
    }

    public void DeleteCommand()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The Run registry key could not be opened for writing.");

        // RegistryKey.DeleteValue throws ArgumentException when the value is
        // absent, and the settings save path can call us twice.
        if (key.GetValue(RunValueName) is null)
        {
            return;
        }

        key.DeleteValue(RunValueName);
    }
}
