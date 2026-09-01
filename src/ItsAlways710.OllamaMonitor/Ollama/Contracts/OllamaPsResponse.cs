using System.Text.Json.Serialization;

namespace ItsAlways710.OllamaMonitor.Ollama.Contracts;

public sealed record OllamaPsResponse(
    [property: JsonPropertyName("models")] IReadOnlyList<OllamaPsModelResponse> Models);
