namespace ItsAlways710.OllamaMonitor.Cli;

public sealed class CliCommandParser
{
    public CliCommand Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new CliCommand(CliCommandKind.LaunchApp, ShouldLaunchApp: true);
        }

        if (args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)))
        {
            return new CliCommand(CliCommandKind.Help);
        }

        if (!string.Equals(args[0], "config", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid($"Unknown command '{string.Join(' ', args)}'.");
        }

        if (args.Count == 1)
        {
            return new CliCommand(CliCommandKind.ShowConfig);
        }

        if (args.Count == 2 && string.Equals(args[1], "reset", StringComparison.OrdinalIgnoreCase))
        {
            return new CliCommand(CliCommandKind.ResetConfig);
        }

        if (args.Count == 4 && string.Equals(args[1], "set", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(args[2], "endpoint", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.TryCreate(args[3], UriKind.Absolute, out _)
                    ? new CliCommand(CliCommandKind.SetEndpoint, StringValue: args[3])
                    : Invalid("The endpoint value must be a valid absolute URL.");
            }

            if (string.Equals(args[2], "refresh-interval", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(args[3], out var refreshInterval) && refreshInterval > 0
                    ? new CliCommand(CliCommandKind.SetRefreshInterval, IntValue: refreshInterval)
                    : Invalid("Refresh interval must be a positive integer in seconds.");
            }

            return Invalid($"Unknown config key '{args[2]}'.");
        }

        return Invalid("Unsupported config command.");
    }

    private static CliCommand Invalid(string errorMessage) => new(CliCommandKind.Invalid, ErrorMessage: errorMessage);
}
