using System.Text.Json.Serialization;

namespace ItsAlways710.OllamaMonitor.Ollama.Contracts;

public sealed record OllamaTagModelResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("modified_at")] DateTimeOffset? ModifiedAt,
    [property: JsonPropertyName("details")] OllamaApiModelDetails? Details);
