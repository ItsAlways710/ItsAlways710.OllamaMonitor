using System.ComponentModel;
using ElBruno.OllamaMonitor.Models;
using ElBruno.OllamaMonitor.Services;
using ElBruno.OllamaMonitor.ViewModels;

namespace ElBruno.OllamaMonitor;

public partial class MainWindow : System.Windows.Window
{
    private readonly GpuUsageGraph _gpuGraph;
    private bool _allowClose;

    public MainWindow(int refreshIntervalSeconds)
    {
        InitializeComponent();
        _gpuGraph = new GpuUsageGraph(GpuUsagePlot, refreshIntervalSeconds);
        _gpuGraph.ApplyTheme(ThemeService.IsResolvedDark(ThemeService.GetSavedThemePreference()));
        DataContextChanged += MainWindow_DataContextChanged;
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

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SnapshotUpdated -= OnSnapshotUpdated;
        }

        base.OnClosing(e);
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainWindowViewModel viewModel)
        {
            viewModel.SnapshotUpdated += OnSnapshotUpdated;
        }
    }

    private void OnSnapshotUpdated(object? sender, OllamaMonitorSnapshot snapshot)
    {
        var resources = snapshot.Resources;
        if (resources is null)
        {
            return;
        }

        double? vramPercent = resources.VramUsedBytes is { } used
            && resources.VramTotalBytes is { } total
            && total > 0
            ? 100d * used / total
            : null;

        _gpuGraph.AddSample(vramPercent, resources.GpuPercent);
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
            _gpuGraph.ApplyTheme(ThemeService.IsResolvedDark(theme));
        }
    }

    private static string? GetThemeText(object? item) => item switch
    {
        string s => s,
        System.Windows.Controls.ComboBoxItem comboBoxItem => comboBoxItem.Content as string,
        _ => null
    };
}
