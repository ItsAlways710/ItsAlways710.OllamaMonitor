using System.Text.Json;
using ItsAlways710.OllamaMonitor.Diagnostics;

namespace ItsAlways710.OllamaMonitor.Configuration;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.RootDirectory);

        if (!File.Exists(AppPaths.SettingsFilePath))
        {
            var defaults = new AppSettings();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        await using var stream = File.OpenRead(AppPaths.SettingsFilePath);

        try
        {
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
            if (settings is null)
            {
                return await ResetAsync(cancellationToken);
            }

            return settings;
        }
        catch (JsonException)
        {
            stream.Close();
            var backupPath = $"{AppPaths.SettingsFilePath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json";
            File.Move(AppPaths.SettingsFilePath, backupPath, overwrite: true);
            return await ResetAsync(cancellationToken);
        }
    }

    public async Task<string> LoadAsJsonAsync(CancellationToken cancellationToken)
    {
        var settings = await LoadAsync(cancellationToken);
        return JsonSerializer.Serialize(settings, SerializerOptions);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.RootDirectory);
        await File.WriteAllTextAsync(
            AppPaths.SettingsFilePath,
            JsonSerializer.Serialize(settings, SerializerOptions),
            cancellationToken);
    }

    public async Task<AppSettings> ResetAsync(CancellationToken cancellationToken)
    {
        var defaults = new AppSettings();
        await SaveAsync(defaults, cancellationToken);
        return defaults;
    }

    public async Task UpdateEndpointAsync(string endpoint, CancellationToken cancellationToken)
    {
        var settings = await LoadAsync(cancellationToken);
        await SaveAsync(settings with { Endpoint = endpoint }, cancellationToken);
    }

    public async Task UpdateRefreshIntervalAsync(int refreshIntervalSeconds, CancellationToken cancellationToken)
    {
        var settings = await LoadAsync(cancellationToken);
        await SaveAsync(settings with { RefreshIntervalSeconds = refreshIntervalSeconds }, cancellationToken);
    }

    /// <summary>
    /// Synchronous variant for UI-thread callers at window hide/close, where the process
    /// may also be exiting and an async file round-trip risks lost writes.
    /// </summary>
    public void UpdateMiniMonitorPosition(double left, double top)
    {
        var settings = LoadSync();
        SaveSync(settings with { MiniMonitorLeft = left, MiniMonitorTop = top });
    }

    private static AppSettings LoadSync()
    {
        if (!File.Exists(AppPaths.SettingsFilePath))
        {
            return new AppSettings();
        }

        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFilePath), SerializerOptions)
               ?? new AppSettings();
    }

    private static void SaveSync(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.RootDirectory);
        File.WriteAllText(
            AppPaths.SettingsFilePath,
            JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
