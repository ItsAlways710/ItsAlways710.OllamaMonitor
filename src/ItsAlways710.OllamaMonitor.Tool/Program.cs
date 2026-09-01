using ItsAlways710.OllamaMonitor;
using ItsAlways710.OllamaMonitor.Cli;
using ItsAlways710.OllamaMonitor.Configuration;
using ItsAlways710.OllamaMonitor.Diagnostics;

var diagnostics = new DiagnosticsLogService(AppPaths.LogsDirectoryPath);
var settingsService = new AppSettingsService();
var command = new CliCommandParser().Parse(args);

if (command.ShouldLaunchApp)
{
    return LaunchDesktopApp(diagnostics);
}

return await new CliCommandRunner(settingsService, diagnostics).RunAsync(command, CancellationToken.None);

static int LaunchDesktopApp(DiagnosticsLogService diagnostics)
{
    try
    {
        var desktopDirectory = Path.Combine(AppContext.BaseDirectory, "desktop");
        var desktopExecutablePath = Path.Combine(desktopDirectory, "ItsAlways710.OllamaMonitor.exe");

        if (!File.Exists(desktopExecutablePath))
        {
            Console.Error.WriteLine("Desktop app payload is missing from the tool installation.");
            return 1;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = desktopExecutablePath,
            WorkingDirectory = desktopDirectory,
            UseShellExecute = true
        });

        return process is null ? 1 : 0;
    }
    catch (Exception exception)
    {
        diagnostics.WriteError("Unable to launch the desktop app.", exception);
        Console.Error.WriteLine("Unable to launch the desktop app.");
        return 1;
    }
}
