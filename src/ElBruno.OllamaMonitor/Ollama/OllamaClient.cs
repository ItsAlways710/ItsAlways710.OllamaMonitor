using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Ollama.Contracts;

namespace ElBruno.OllamaMonitor.Ollama;

public sealed class OllamaClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly DiagnosticsLogService _diagnostics;

    public OllamaClient(HttpClient httpClient, DiagnosticsLogService diagnostics)
    {
        _httpClient = httpClient;
        _diagnostics = diagnostics;
    }

    public Task<OllamaApiCallResult<OllamaVersionResponse>> GetVersionAsync(Uri endpoint, CancellationToken cancellationToken) =>
        GetAsync<OllamaVersionResponse>(endpoint, "/api/version", cancellationToken);

    public Task<OllamaApiCallResult<OllamaTagsResponse>> GetTagsAsync(Uri endpoint, CancellationToken cancellationToken) =>
        GetAsync<OllamaTagsResponse>(endpoint, "/api/tags", cancellationToken);

    public Task<OllamaApiCallResult<OllamaPsResponse>> GetRunningModelsAsync(Uri endpoint, CancellationToken cancellationToken) =>
        GetAsync<OllamaPsResponse>(endpoint, "/api/ps", cancellationToken);

    public async Task<OllamaApiCallResult<bool>> UnloadModelAsync(Uri endpoint, string modelName, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new { model = modelName, keep_alive = 0 };
            using var response = await _httpClient.PostAsJsonAsync(BuildUri(endpoint, "/api/generate"), payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new OllamaApiCallResult<bool>(false, ErrorMessage: $"Ollama API returned {(int)response.StatusCode} when unloading '{modelName}'.");
            }

            return new OllamaApiCallResult<bool>(true, true);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException or JsonException)
        {
            _diagnostics.WriteWarning($"Ollama unload call failed for model '{modelName}': {exception.Message}");
            return new OllamaApiCallResult<bool>(false, ErrorMessage: exception.Message);
        }
    }

    private async Task<OllamaApiCallResult<T>> GetAsync<T>(Uri endpoint, string relativePath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(BuildUri(endpoint, relativePath), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new OllamaApiCallResult<T>(false, ErrorMessage: $"Ollama API returned {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
            return payload is null
                ? new OllamaApiCallResult<T>(false, ErrorMessage: "Ollama API returned an empty response.")
                : new OllamaApiCallResult<T>(true, payload);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException or JsonException)
        {
            _diagnostics.WriteWarning($"Ollama API call failed for {relativePath}: {exception.Message}");
            return new OllamaApiCallResult<T>(false, ErrorMessage: exception.Message);
        }
    }

    private static Uri BuildUri(Uri endpoint, string relativePath)
    {
        var builder = new UriBuilder(endpoint)
        {
            Path = relativePath.TrimStart('/')
        };

        return builder.Uri;
    }
}
