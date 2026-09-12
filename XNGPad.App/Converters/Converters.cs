using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace XNGPad.App.Converters;

public sealed class LevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value as string;
        var color = level switch
        {
            "ok" => Color.FromRgb(0x7B, 0xC4, 0x7F),
            "warn" => Color.FromRgb(0xE0, 0xB4, 0x4A),
            "err" => Color.FromRgb(0xE0, 0x7A, 0x7A),
            "busy" => Color.FromRgb(0x6B, 0x8A, 0xFD),
            _ => Color.FromRgb(0x5A, 0x5C, 0x66)
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BannerToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value as string;
        var color = level switch
        {
            "ok" => Color.FromRgb(0x7B, 0xC4, 0x7F),
            "err" => Color.FromRgb(0xE0, 0x7A, 0x7A),
            "warn" => Color.FromRgb(0xE0, 0xB4, 0x4A),
            _ => Color.FromRgb(0x9A, 0x9A, 0xA3)
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
