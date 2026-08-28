using System.ComponentModel;
using ElBruno.OllamaMonitor.Services;

namespace ElBruno.OllamaMonitor;

public partial class MainWindow : System.Windows.Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += (_, _) =>
        {
            InitializeThemeSelector();
        };
    }

    public void PrepareForExit() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void InitializeThemeSelector()
    {
        var savedTheme = ThemeService.GetSavedThemePreference().ToString();
        foreach (var item in ThemeSelector.Items)
        {
            if (GetThemeText(item) == savedTheme)
            {
                ThemeSelector.SelectedItem = item;
                return;
            }
        }
    }

    private void ThemeSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var text = GetThemeText(ThemeSelector.SelectedItem);
        if (text is not null && System.Enum.TryParse<Services.ThemeMode>(text, out var theme))
        {
            ThemeService.ApplyTheme(theme);
            ThemeService.SaveThemePreference(theme);
        }
    }

    private static string? GetThemeText(object? item) => item switch
    {
        string s => s,
        System.Windows.Controls.ComboBoxItem comboBoxItem => comboBoxItem.Content as string,
        _ => null
    };
}
