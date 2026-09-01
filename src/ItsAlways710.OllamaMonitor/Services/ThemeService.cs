using Microsoft.Win32;

namespace ItsAlways710.OllamaMonitor.Services;

public enum ThemeMode
{
    Light,
    Dark,
    System
}

public class ThemeService
{
    private const string ThemePreferenceKey = "ThemePreference";

    public static void ApplyTheme(ThemeMode mode)
    {
        var app = System.Windows.Application.Current;
        string themeName = mode switch
        {
            ThemeMode.Light => "Light",
            ThemeMode.Dark => "Dark",
            ThemeMode.System => IsSystemDarkMode() ? "Dark" : "Light",
            _ => "Light"
        };

        try
        {
            var uri = new Uri($"pack://application:,,,/Resources/Themes{themeName}.xaml");
            if (app.Resources.MergedDictionaries.Count > 0)
            {
                app.Resources.MergedDictionaries[0].Source = uri;
            }
            else
            {
                var dict = new System.Windows.ResourceDictionary { Source = uri };
                app.Resources.MergedDictionaries.Add(dict);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolve a theme preference to the theme that is actually active
    /// (System follows the OS light/dark switch).
    /// </summary>
    public static bool IsResolvedDark(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => false,
        ThemeMode.Dark => true,
        _ => IsSystemDarkMode()
    };

    public static bool IsSystemDarkMode()
    {
        try
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                return value == 0;
        }
        catch
        {
            // Fallback to light theme if registry read fails
        }
        return false;
    }

    public static ThemeMode GetSavedThemePreference()
    {
        var settingsFile = Path.Combine(AppPaths.RootDirectory, "theme.txt");
        if (File.Exists(settingsFile))
        {
            var content = File.ReadAllText(settingsFile).Trim();
            return Enum.TryParse<ThemeMode>(content, out var mode) ? mode : ThemeMode.System;
        }
        return ThemeMode.System;
    }

    public static void SaveThemePreference(ThemeMode mode)
    {
        try
        {
            var settingsFile = Path.Combine(AppPaths.RootDirectory, "theme.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
            File.WriteAllText(settingsFile, mode.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save theme preference: {ex.Message}");
        }
    }
}
