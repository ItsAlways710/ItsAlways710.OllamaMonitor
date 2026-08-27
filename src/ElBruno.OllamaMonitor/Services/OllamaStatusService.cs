using ElBruno.OllamaMonitor.Configuration;
using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Helpers;
using ElBruno.OllamaMonitor.Models;
using ElBruno.OllamaMonitor.Ollama;
using ElBruno.OllamaMonitor.Ollama.Contracts;

namespace ElBruno.OllamaMonitor.Services;

public sealed class OllamaStatusService
{
    private readonly OllamaClient _ollamaClient;
    private readonly IOllamaCliService _ollamaCliService;
    private readonly ProcessMetricsService _processMetricsService;
    private readonly NvidiaSmiMetricsService _gpuMetricsService;
    private readonly ContextTrackingService _contextTrackingService;
    private readonly DiagnosticsLogService _diagnostics;

    public OllamaStatusService(
        OllamaClient ollamaClient,
        IOllamaCliService ollamaCliService,
        ProcessMetricsService processMetricsService,
        NvidiaSmiMetricsService gpuMetricsService,
        ContextTrackingService contextTrackingService,
        DiagnosticsLogService diagnostics)
    {
        _ollamaClient = ollamaClient;
        _ollamaCliService = ollamaCliService;
        _processMetricsService = processMetricsService;
        _gpuMetricsService = gpuMetricsService;
        _contextTrackingService = contextTrackingService;
        _diagnostics = diagnostics;
    }

    public async Task<OllamaMonitorSnapshot> GetSnapshotAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.Now;

        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new OllamaMonitorSnapshot
            {
                State = OllamaMonitorState.Error,
                Endpoint = settings.Endpoint,
                LastChecked = checkedAt,
                ErrorMessage = "Configured endpoint is not a valid absolute URL."
            };
        }

        var versionTask = _ollamaClient.GetVersionAsync(endpoint, cancellationToken);
        var runningModelsTask = _ollamaClient.GetRunningModelsAsync(endpoint, cancellationToken);
        var processMetricsTask = _processMetricsService.GetMetricsAsync(settings.EnableDiskMetrics, cancellationToken);
        var gpuMetricsTask = _gpuMetricsService.GetMetricsAsync(settings.EnableGpuMetrics, cancellationToken);

        await Task.WhenAll(versionTask, runningModelsTask, processMetricsTask, gpuMetricsTask);

        var versionResult = await versionTask;
        var runningModelsResult = await runningModelsTask;
        var processMetrics = await processMetricsTask;
        var gpuMetrics = await gpuMetricsTask;

        var errors = new List<string>();
        if (!versionResult.IsSuccess && !string.IsNullOrWhiteSpace(versionResult.ErrorMessage))
        {
            errors.Add(versionResult.ErrorMessage);
        }

        if (versionResult.IsSuccess && !runningModelsResult.IsSuccess && !string.IsNullOrWhiteSpace(runningModelsResult.ErrorMessage))
        {
            errors.Add(runningModelsResult.ErrorMessage);
        }

        if (!processMetrics.IsProcessFound && !string.IsNullOrWhiteSpace(processMetrics.ErrorMessage))
        {
            errors.Add(processMetrics.ErrorMessage);
        }

        var resourceSnapshot = BuildResourceSnapshot(processMetrics, gpuMetrics);
        var models = runningModelsResult.IsSuccess
            ? BuildModelSnapshots(runningModelsResult.Value?.Models)
            : Array.Empty<OllamaModelSnapshot>();

        var state = DetermineState(versionResult, runningModelsResult, resourceSnapshot, models, settings);
        var errorMessage = errors.Count == 0 ? null : string.Join(" | ", errors.Distinct());

        if (state is OllamaMonitorState.Error && errorMessage is null)
        {
            errorMessage = "One or more Ollama status checks failed.";
        }

        return new OllamaMonitorSnapshot
        {
            State = state,
            Endpoint = settings.Endpoint,
            Version = versionResult.Value?.Version,
            IsApiReachable = versionResult.IsSuccess,
            Models = models,
            ContextWindows = _contextTrackingService.GetSnapshot(),
            Resources = resourceSnapshot,
            LastChecked = checkedAt,
            ErrorMessage = errorMessage
        };
    }

    public async Task<OllamaApiCallResult<bool>> UnloadModelAsync(AppSettings settings, string modelName, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Configured endpoint is not a valid absolute URL.");
        }

        var result = await StopModelAsync(settings, endpoint, modelName, cancellationToken);
        if (result.IsSuccess)
        {
            _diagnostics.WriteInfo($"Requested unload for model '{modelName}'.");
        }
        else
        {
            _diagnostics.WriteWarning($"Unload request for model '{modelName}' failed: {result.ErrorMessage}");
        }

        return result;
    }

    public async Task<OllamaApiCallResult<IReadOnlyList<string>>> GetRunningModelNamesAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new OllamaApiCallResult<IReadOnlyList<string>>(false, ErrorMessage: "Configured endpoint is not a valid absolute URL.");
        }

        if (ShouldUseCli(settings.UnloadStrategy, endpoint))
        {
            var cliResult = await _ollamaCliService.GetRunningModelsAsync(cancellationToken);
            if (cliResult.IsSuccess)
            {
                return cliResult;
            }
        }

        var runningModelsResult = await _ollamaClient.GetRunningModelsAsync(endpoint, cancellationToken);
        if (!runningModelsResult.IsSuccess || runningModelsResult.Value is null)
        {
            return new OllamaApiCallResult<IReadOnlyList<string>>(
                false,
                ErrorMessage: runningModelsResult.ErrorMessage ?? "Unable to read running models.");
        }

        var modelNames = runningModelsResult.Value.Models.Select(model => model.Name).ToArray();
        return new OllamaApiCallResult<IReadOnlyList<string>>(true, modelNames);
    }

    public async Task<OllamaApiCallResult<bool>> PullModelAsync(AppSettings settings, string modelName, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Configured endpoint is not a valid absolute URL.");
        }

        if (!IsLocalEndpoint(endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Model pull is available only for local Ollama endpoints.");
        }

        return await _ollamaCliService.PullModelAsync(modelName, cancellationToken);
    }

    public async Task<OllamaApiCallResult<bool>> RemoveModelAsync(AppSettings settings, string modelName, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Configured endpoint is not a valid absolute URL.");
        }

        if (!IsLocalEndpoint(endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Model removal is available only for local Ollama endpoints.");
        }

        return await _ollamaCliService.RemoveModelAsync(modelName, cancellationToken);
    }

    public async Task<OllamaApiCallResult<bool>> CopyModelAsync(
        AppSettings settings,
        string sourceModelName,
        string targetModelName,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Configured endpoint is not a valid absolute URL.");
        }

        if (!IsLocalEndpoint(endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Model copy is available only for local Ollama endpoints.");
        }

        return await _ollamaCliService.CopyModelAsync(sourceModelName, targetModelName, cancellationToken);
    }

    public OllamaApiCallResult<bool> StartOllama(AppSettings settings)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Configured endpoint is not a valid absolute URL.");
        }

        if (!IsLocalEndpoint(endpoint))
        {
            return new OllamaApiCallResult<bool>(false, ErrorMessage: "Starting Ollama is available only for local endpoints.");
        }

        return _ollamaCliService.StartOllama();
    }

    private async Task<OllamaApiCallResult<bool>> StopModelAsync(
        AppSettings settings,
        Uri endpoint,
        string modelName,
        CancellationToken cancellationToken)
    {
        if (settings.UnloadStrategy is ModelUnloadStrategy.Cli && !IsLocalEndpoint(endpoint))
        {
            return new OllamaApiCallResult<bool>(
                false,
                ErrorMessage: "CLI unload strategy requires a local Ollama endpoint.");
        }

        if (ShouldUseCli(settings.UnloadStrategy, endpoint))
        {
            var stopResult = await _ollamaCliService.StopModelAsync(modelName, cancellationToken);
            if (stopResult.IsSuccess || settings.UnloadStrategy is ModelUnloadStrategy.Cli)
            {
                return stopResult;
            }
        }

        var apiResult = await _ollamaClient.UnloadModelAsync(endpoint, modelName, cancellationToken);
        if (apiResult.IsSuccess || settings.UnloadStrategy is not ModelUnloadStrategy.Auto)
        {
            return apiResult;
        }

        return new OllamaApiCallResult<bool>(
            false,
            ErrorMessage: "Both CLI stop and API unload requests failed.");
    }

    private static bool ShouldUseCli(ModelUnloadStrategy strategy, Uri endpoint)
    {
        return strategy switch
        {
            ModelUnloadStrategy.Cli => true,
            ModelUnloadStrategy.Api => false,
            _ => IsLocalEndpoint(endpoint)
        };
    }

    private static bool IsLocalEndpoint(Uri endpoint)
    {
        if (endpoint.IsLoopback)
        {
            return true;
        }

        var host = endpoint.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<OllamaModelSnapshot> BuildModelSnapshots(IReadOnlyList<OllamaPsModelResponse>? models)
    {
        if (models is null || models.Count == 0)
        {
            return Array.Empty<OllamaModelSnapshot>();
        }

        return models
            .Select(model => new OllamaModelSnapshot
            {
                Name = model.Name,
                Size = StatusTextHelper.FormatBytes(model.Size),
                SizeVram = model.SizeVram,
                Processor = BuildProcessorLabel(model.Details),
                ProcessorUsage = StatusTextHelper.BuildProcessorDisplay(model.Size, model.SizeVram),
                ExpiresAt = model.ExpiresAt
            })
            .ToArray();
    }

    private static string? BuildProcessorLabel(OllamaApiModelDetails? details)
    {
        var values = new[]
        {
            details?.Family,
            details?.ParameterSize,
            details?.QuantizationLevel
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        return values.Any() ? string.Join(" · ", values) : details?.Format;
    }

    private static ResourceSnapshot BuildResourceSnapshot(ProcessMetricsResult processMetrics, GpuMetricsResult gpuMetrics) =>
        new()
        {
            CpuPercent = processMetrics.CpuPercent,
            MemoryBytes = processMetrics.WorkingSetBytes,
            MemoryGb = processMetrics.WorkingSetBytes is null ? null : processMetrics.WorkingSetBytes.Value / 1024d / 1024d / 1024d,
            PrivateMemoryBytes = processMetrics.PrivateMemoryBytes,
            DiskReadBytesPerSecond = processMetrics.DiskReadBytesPerSecond,
            DiskWriteBytesPerSecond = processMetrics.DiskWriteBytesPerSecond,
            GpuPercent = gpuMetrics.GpuPercent,
            VramUsedBytes = gpuMetrics.VramUsedBytes,
            VramTotalBytes = gpuMetrics.VramTotalBytes,
            GpuName = gpuMetrics.GpuName,
            GpuStatus = gpuMetrics.StatusMessage,
            ProcessStartedAt = processMetrics.StartedAt,
            ProcessName = processMetrics.ProcessName
        };

    private OllamaMonitorState DetermineState(
        OllamaApiCallResult<OllamaVersionResponse> versionResult,
        OllamaApiCallResult<OllamaPsResponse> runningModelsResult,
        ResourceSnapshot resourceSnapshot,
        IReadOnlyList<OllamaModelSnapshot> models,
        AppSettings settings)
    {
        if (!versionResult.IsSuccess)
        {
            return OllamaMonitorState.NotReachable;
        }

        if (!runningModelsResult.IsSuccess)
        {
            _diagnostics.WriteWarning("Ollama /api/ps call failed after API reachability succeeded.");
            return OllamaMonitorState.Error;
        }

        if (IsHighUsage(resourceSnapshot, settings))
        {
            return OllamaMonitorState.HighUsage;
        }

        return models.Count > 0 ? OllamaMonitorState.ModelLoaded : OllamaMonitorState.Running;
    }

    private static bool IsHighUsage(ResourceSnapshot resources, AppSettings settings)
    {
        var isCpuHigh = resources.CpuPercent >= settings.HighCpuThresholdPercent;
        var isMemoryHigh = resources.MemoryGb >= settings.HighMemoryThresholdGb;
        var isGpuHigh = resources.GpuPercent >= settings.HighGpuThresholdPercent;
        return isCpuHigh || isMemoryHigh || isGpuHigh;
    }
}
