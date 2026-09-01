using System.Text.Json.Serialization;

namespace ItsAlways710.OllamaMonitor.Ollama.Contracts;

public sealed record OllamaApiModelDetails(
    [property: JsonPropertyName("format")] string? Format,
    [property: JsonPropertyName("family")] string? Family,
    [property: JsonPropertyName("parameter_size")] string? ParameterSize,
    [property: JsonPropertyName("quantization_level")] string? QuantizationLevel);
