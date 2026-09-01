using System.Text.Json.Serialization;

namespace ItsAlways710.OllamaMonitor.Ollama.Contracts;

public sealed record OllamaPsModelResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("size_vram")] long? SizeVram,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("details")] OllamaApiModelDetails? Details,
    [property: JsonPropertyName("context_length")] long? ContextLength);
