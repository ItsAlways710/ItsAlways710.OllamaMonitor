using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ElBruno.OllamaMonitor;

public partial class MiniMonitorWindow : Window
{
    private bool _allowClose;

    public MiniMonitorWindow()
    {
        InitializeComponent();
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

    private void OnWindowDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
