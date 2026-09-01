using ItsAlways710.OllamaMonitor.Ollama;

namespace ItsAlways710.OllamaMonitor.Services;

public interface IOllamaCliService
{
    Task<OllamaApiCallResult<IReadOnlyList<string>>> GetRunningModelsAsync(CancellationToken cancellationToken);
    Task<OllamaApiCallResult<bool>> StopModelAsync(string modelName, CancellationToken cancellationToken);
    Task<OllamaApiCallResult<bool>> PullModelAsync(string modelName, CancellationToken cancellationToken);
    Task<OllamaApiCallResult<bool>> RemoveModelAsync(string modelName, CancellationToken cancellationToken);
    Task<OllamaApiCallResult<bool>> CopyModelAsync(string sourceModelName, string targetModelName, CancellationToken cancellationToken);
    OllamaApiCallResult<bool> StartOllama();
}
