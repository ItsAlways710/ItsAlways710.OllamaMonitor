using System.ComponentModel;
using System.Windows;
using ElBruno.OllamaMonitor.Configuration;

namespace ElBruno.OllamaMonitor.Windows;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsService _settingsService;
    private bool _allowClose;

    public SettingsWindow(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        Loaded += async (_, _) => await LoadSettingsAsync();
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

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync(CancellationToken.None);
            
            EndpointTextBox.Text = settings.Endpoint;
            RefreshIntervalTextBox.Text = settings.RefreshIntervalSeconds.ToString();
            StartMinimizedCheckBox.IsChecked = settings.StartMinimizedToTray;
            ShowFloatingWindowCheckBox.IsChecked = settings.ShowFloatingWindowOnStart;
            EnableGpuMetricsCheckBox.IsChecked = settings.EnableGpuMetrics;
            EnableDiskMetricsCheckBox.IsChecked = settings.EnableDiskMetrics;
            ShowCpuInMiniMonitorCheckBox.IsChecked = settings.ShowCpuInMiniMonitor;
            ShowMemoryInMiniMonitorCheckBox.IsChecked = settings.ShowMemoryInMiniMonitor;
            ShowOllamaLogsInMiniMonitorCheckBox.IsChecked = settings.ShowOllamaLogsInMiniMonitor;
            HighCpuThresholdTextBox.Text = settings.HighCpuThresholdPercent.ToString("F0");
            HighMemoryThresholdTextBox.Text = settings.HighMemoryThresholdGb.ToString("F1");
            HighGpuThresholdTextBox.Text = settings.HighGpuThresholdPercent.ToString("F0");

            ClearErrors();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to load settings: {ex.Message}",
                "Settings Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        ClearErrors();

        var endpoint = EndpointTextBox.Text.Trim();
        var refreshIntervalText = RefreshIntervalTextBox.Text.Trim();

        var endpointValidation = SettingsValidator.ValidateEndpoint(endpoint);
        if (!endpointValidation.Ok)
        {
            ShowError(EndpointErrorText, endpointValidation.Error!);
            return;
        }

        if (!int.TryParse(refreshIntervalText, out var refreshInterval))
        {
            ShowError(RefreshIntervalErrorText, "Refresh interval must be a valid number.");
            return;
        }

        var intervalValidation = SettingsValidator.ValidateRefreshInterval(refreshInterval);
        if (!intervalValidation.Ok)
        {
            ShowError(RefreshIntervalErrorText, intervalValidation.Error!);
            return;
        }

        try
        {
            SaveButton.IsEnabled = false;
            SaveButton.Content = "Saving...";

            // Reload from disk first (last-write-wins per Neo's spec section 5)
            var current = await _settingsService.LoadAsync(CancellationToken.None);
            
            var updated = current with 
            { 
                Endpoint = endpoint,
                RefreshIntervalSeconds = refreshInterval,
                ShowCpuInMiniMonitor = ShowCpuInMiniMonitorCheckBox.IsChecked == true,
                ShowMemoryInMiniMonitor = ShowMemoryInMiniMonitorCheckBox.IsChecked == true,
                ShowOllamaLogsInMiniMonitor = ShowOllamaLogsInMiniMonitorCheckBox.IsChecked == true
            };

            await _settingsService.SaveAsync(updated, CancellationToken.None);

            Hide();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Save Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Content = "Save";
        }
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to reset all settings to defaults?",
            "Reset Settings",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            ResetButton.IsEnabled = false;
            ResetButton.Content = "Resetting...";

            await _settingsService.ResetAsync(CancellationToken.None);
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to reset settings: {ex.Message}",
                "Reset Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            ResetButton.IsEnabled = true;
            ResetButton.Content = "Reset to Defaults";
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ShowError(System.Windows.Controls.TextBlock errorLabel, string message)
    {
        errorLabel.Text = message;
        errorLabel.Visibility = Visibility.Visible;
    }

    private void ClearErrors()
    {
        EndpointErrorText.Visibility = Visibility.Collapsed;
        RefreshIntervalErrorText.Visibility = Visibility.Collapsed;
    }


}
