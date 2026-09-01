using System.Text.Json.Serialization;

namespace ItsAlways710.OllamaMonitor.Ollama.Contracts;

public sealed record OllamaVersionResponse(
    [property: JsonPropertyName("version")] string Version);
