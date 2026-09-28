using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ServiceDesk.Desktop.Controls;

// "#2196F3" → SolidColorBrush (цвет статуса из справочника)
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value as string ?? "#7A8794"));
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) ^ (parameter as string == "invert") ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public static class Fmt
{
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    public static string Money(decimal v) => v.ToString("#,0.##", Ru) + " ₽";
    public static string Date(DateTime? d) => d?.ToString("dd.MM.yyyy") ?? "—";
    public static string DateTime(DateTime? d) => d?.ToString("dd.MM.yyyy HH:mm") ?? "—";
}
