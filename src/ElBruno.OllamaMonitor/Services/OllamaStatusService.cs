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
    private readonly ProcessMetricsService _processMetricsService;
    private readonly NvidiaSmiMetricsService _gpuMetricsService;
    private readonly DiagnosticsLogService _diagnostics;

    public OllamaStatusService(
        OllamaClient ollamaClient,
        ProcessMetricsService processMetricsService,
        NvidiaSmiMetricsService gpuMetricsService,
        DiagnosticsLogService diagnostics)
    {
        _ollamaClient = ollamaClient;
        _processMetricsService = processMetricsService;
        _gpuMetricsService = gpuMetricsService;
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

        var result = await _ollamaClient.UnloadModelAsync(endpoint, modelName, cancellationToken);
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
                Processor = BuildProcessorLabel(model.Details),
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
