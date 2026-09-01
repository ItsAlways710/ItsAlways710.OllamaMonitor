using System.Text.Json.Serialization;

namespace ItsAlways710.OllamaMonitor.Ollama.Contracts;

public sealed record OllamaTagsResponse(
    [property: JsonPropertyName("models")] IReadOnlyList<OllamaTagModelResponse> Models);
