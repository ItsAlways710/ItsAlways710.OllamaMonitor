using System.Windows;
using ItsAlways710.OllamaMonitor.ViewModels;

namespace ItsAlways710.OllamaMonitor;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.SaveAsync(CancellationToken.None);
            // This window is only ever shown via ShowDialog() from
            // OpenSettingsAsync, so DialogResult is legal to set.
            DialogResult = true;

            Close();
        }
    }
}
