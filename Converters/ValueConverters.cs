using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AppOogst.Converters;

public sealed class LogBrushConverter : IValueConverter
{
    private static readonly IBrush Cmd = new SolidColorBrush(Color.Parse("#5b9dff"));
    private static readonly IBrush Err = new SolidColorBrush(Color.Parse("#ff6b6b"));
    private static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#ffb454"));
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#34c77b"));
    private static readonly IBrush Inf = new SolidColorBrush(Color.Parse("#c7ccd8"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            "CMD" => Cmd,
            "ERR" => Err,
            "WRN" => Warn,
            "OK" => Ok,
            _ => Inf,
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolBrushConverter : IValueConverter
{
    private static readonly IBrush True = new SolidColorBrush(Color.Parse("#34c77b"));
    private static readonly IBrush False = new SolidColorBrush(Color.Parse("#98a0b0"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? True : False;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
