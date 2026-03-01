using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OhioPayroll.App.Converters;

/// <summary>
/// Converts a boolean to a color brush. When true, returns the TrueColor parameter; when false, returns FalseColor.
/// Parameter format: "TrueColor|FalseColor" (e.g., "#F87171|#4ADE80").
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string colors)
            return Brushes.White;

        var parts = colors.Split('|');
        if (parts.Length != 2)
            return Brushes.White;

        var colorStr = boolValue ? parts[0] : parts[1];
        try
        {
            return SolidColorBrush.Parse(colorStr);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return Brushes.White;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
