namespace ItsAlways710.OllamaMonitor.Configuration;

public static class SettingsValidator
{
    public static (bool Ok, string? Error) ValidateEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return (false, "Endpoint is required.");

        var trimmedEndpoint = endpoint.TrimEnd('/');
        if (!Uri.TryCreate(trimmedEndpoint, UriKind.Absolute, out var uri))
            return (false, "Endpoint must be a valid http(s) URL.");

        if (uri.Scheme != "http" && uri.Scheme != "https")
            return (false, "Endpoint must be a valid http(s) URL.");

        return (true, null);
    }

    public static (bool Ok, string? Error) ValidateRefreshInterval(int seconds)
    {
        if (seconds < 1 || seconds > 60)
            return (false, "Refresh interval must be between 1 and 60 seconds.");

        return (true, null);
    }
}
