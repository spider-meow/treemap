using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Treemap.App;

/// <summary>Colours a usage fraction (0..1): accent normally, amber from 80 %, red from 90 %.</summary>
public sealed class UsageBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var fraction = value is double d ? d : 0;
        var key = fraction >= 0.9 ? "DangerBrush" : fraction >= 0.8 ? "WarnBrush" : "AccentBrush";
        return Application.Current.Resources[key] as Brush ?? Brushes.SteelBlue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
