using System.Diagnostics;
using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Ollama;

namespace ElBruno.OllamaMonitor.Services;

public sealed class OllamaCliService : IOllamaCliService
{
    private readonly DiagnosticsLogService _diagnostics;

    public OllamaCliService(DiagnosticsLogService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public async Task<OllamaApiCallResult<IReadOnlyList<string>>> GetRunningModelsAsync(CancellationToken cancellationToken)
    {
        var commandResult = await RunOllamaCommandAsync(["ps"], cancellationToken);
        if (!commandResult.IsSuccess || string.IsNullOrWhiteSpace(commandResult.Value?.StdOut))
        {
            return new OllamaApiCallResult<IReadOnlyList<string>>(
                false,
                ErrorMessage: commandResult.ErrorMessage ?? "Unable to read running models from `ollama ps`.");
        }

        var modelNames = commandResult.Value.StdOut
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .Select(ParseModelNameFromPsLine)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new OllamaApiCallResult<IReadOnlyList<string>>(true, modelNames);
    }

    public async Task<OllamaApiCallResult<bool>> StopModelAsync(string modelName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Model name cannot be empty.");
        }

        var commandResult = await RunOllamaCommandAsync(["stop", modelName], cancellationToken);
        return commandResult.IsSuccess
            ? new OllamaApiCallResult<bool>(true, true)
            : new OllamaApiCallResult<bool>(false, ErrorMessage: commandResult.ErrorMessage);
    }

    public async Task<OllamaApiCallResult<bool>> PullModelAsync(string modelName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Model name cannot be empty.");
        }

        var commandResult = await RunOllamaCommandAsync(["pull", modelName], cancellationToken);
        return commandResult.IsSuccess
            ? new OllamaApiCallResult<bool>(true, true)
            : new OllamaApiCallResult<bool>(false, ErrorMessage: commandResult.ErrorMessage);
    }

    public async Task<OllamaApiCallResult<bool>> RemoveModelAsync(string modelName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Model name cannot be empty.");
        }

        var commandResult = await RunOllamaCommandAsync(["rm", modelName], cancellationToken);
        return commandResult.IsSuccess
            ? new OllamaApiCallResult<bool>(true, true)
            : new OllamaApiCallResult<bool>(false, ErrorMessage: commandResult.ErrorMessage);
    }

    public async Task<OllamaApiCallResult<bool>> CopyModelAsync(string sourceModelName, string targetModelName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceModelName) || string.IsNullOrWhiteSpace(targetModelName))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Source and target model names are required.");
        }

        var commandResult = await RunOllamaCommandAsync(["cp", sourceModelName, targetModelName], cancellationToken);
        return commandResult.IsSuccess
            ? new OllamaApiCallResult<bool>(true, true)
            : new OllamaApiCallResult<bool>(false, ErrorMessage: commandResult.ErrorMessage);
    }

    public OllamaApiCallResult<bool> StartOllama()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("serve");

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return new OllamaApiCallResult<bool>(false, ErrorMessage: "Failed to start `ollama serve`.");
            }

            _diagnostics.WriteInfo("Started `ollama serve`.");
            return new OllamaApiCallResult<bool>(true, true);
        }
        catch (Exception exception)
        {
            _diagnostics.WriteWarning($"Unable to start `ollama serve`: {exception.Message}");
            return new OllamaApiCallResult<bool>(false, ErrorMessage: exception.Message);
        }
    }

    private async Task<OllamaApiCallResult<ProcessExecutionResult>> RunOllamaCommandAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var displayArgs = string.Join(" ", arguments);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new OllamaApiCallResult<ProcessExecutionResult>(false, ErrorMessage: $"Unable to execute `ollama {displayArgs}`.");
            }

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
                _diagnostics.WriteWarning($"`ollama {displayArgs}` failed with exit code {process.ExitCode}: {error}");
                return new OllamaApiCallResult<ProcessExecutionResult>(false, ErrorMessage: error.Trim());
            }

            return new OllamaApiCallResult<ProcessExecutionResult>(
                true,
                new ProcessExecutionResult(process.ExitCode, stdOut, stdErr));
        }
        catch (Exception exception)
        {
            _diagnostics.WriteWarning($"`ollama {displayArgs}` execution failed: {exception.Message}");
            return new OllamaApiCallResult<ProcessExecutionResult>(false, ErrorMessage: exception.Message);
        }
    }

    private static string ParseModelNameFromPsLine(string line)
    {
        var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return columns.Length == 0 ? string.Empty : columns[0];
    }

    private sealed record ProcessExecutionResult(int ExitCode, string StdOut, string StdErr);
}
