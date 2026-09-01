using ItsAlways710.OllamaMonitor.Configuration;
using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Interop;

namespace ItsAlways710.OllamaMonitor.Cli;

public sealed class CliCommandRunner
{
    private readonly AppSettingsService _settingsService;
    private readonly DiagnosticsLogService _diagnostics;

    public CliCommandRunner(AppSettingsService settingsService, DiagnosticsLogService diagnostics)
    {
        _settingsService = settingsService;
        _diagnostics = diagnostics;
    }

    public async Task<int> RunAsync(CliCommand command, CancellationToken cancellationToken)
    {
        ConsoleManager.EnsureConsole();
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        switch (command.Kind)
        {
            case CliCommandKind.Help:
                Console.WriteLine(BuildHelpText());
                return 0;

            case CliCommandKind.ShowConfig:
                Console.WriteLine(await _settingsService.LoadAsJsonAsync(cancellationToken));
                return 0;

            case CliCommandKind.SetEndpoint:
                var endpointValidation = SettingsValidator.ValidateEndpoint(command.StringValue!);
                if (!endpointValidation.Ok)
                {
                    Console.Error.WriteLine($"Error: {endpointValidation.Error}");
                    return 1;
                }
                await _settingsService.UpdateEndpointAsync(command.StringValue!, cancellationToken);
                Console.WriteLine($"Updated endpoint to {command.StringValue}.");
                return 0;

            case CliCommandKind.SetRefreshInterval:
                var intervalValidation = SettingsValidator.ValidateRefreshInterval(command.IntValue!.Value);
                if (!intervalValidation.Ok)
                {
                    Console.Error.WriteLine($"Error: {intervalValidation.Error}");
                    return 1;
                }
                await _settingsService.UpdateRefreshIntervalAsync(command.IntValue!.Value, cancellationToken);
                Console.WriteLine($"Updated refresh interval to {command.IntValue} second(s).");
                return 0;

            case CliCommandKind.ResetConfig:
                await _settingsService.ResetAsync(cancellationToken);
                Console.WriteLine("Configuration reset to defaults.");
                return 0;

            case CliCommandKind.Invalid:
                _diagnostics.WriteWarning(command.ErrorMessage ?? "Invalid CLI command.");
                Console.Error.WriteLine(command.ErrorMessage ?? "Invalid CLI command.");
                Console.WriteLine();
                Console.WriteLine(BuildHelpText());
                return 1;

            default:
                return 0;
        }
    }

    public static string BuildHelpText() => string.Join(
        Environment.NewLine,
        [
            "ItsAlways710.OllamaMonitor",
            string.Empty,
            "Usage:",
            "  ollamamon",
            "  ollamamon --help",
            "  ollamamon config",
            "  ollamamon config set endpoint <url>",
            "  ollamamon config set refresh-interval <seconds>",
            "  ollamamon config reset"
        ]);
}
