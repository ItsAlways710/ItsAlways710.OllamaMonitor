using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ItsAlways710.OllamaMonitor.Helpers;

public sealed class BoolToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(System.Windows.Media.Color.FromRgb(34, 197, 94));
    private static readonly SolidColorBrush InactiveBrush = new(System.Windows.Media.Color.FromRgb(107, 114, 128));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo cultureInfo)
    {
        if (value is bool isActive)
        {
            return isActive ? ActiveBrush : InactiveBrush;
        }

        return InactiveBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo cultureInfo)
    {
        throw new NotImplementedException();
    }
}
