namespace ItsAlways710.OllamaMonitor.Ollama;

public sealed record OllamaApiCallResult<T>(bool IsSuccess, T? Value = default, string? ErrorMessage = null);
