namespace ItsAlways710.OllamaMonitor;

public static class AppPaths
{
    public static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ItsAlways710",
            "OllamaMonitor");

    public static string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

    public static string LogsDirectoryPath => Path.Combine(RootDirectory, "logs");
}
