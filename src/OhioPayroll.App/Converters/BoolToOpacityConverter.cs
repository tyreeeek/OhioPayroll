using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OhioPayroll.App.Converters;

/// <summary>
/// Converts boolean to opacity (1.0 for true, 0.5 for false)
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? 1.0 : 0.5;

        return 0.5;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
