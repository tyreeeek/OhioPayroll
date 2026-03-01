using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OhioPayroll.App.Converters;

/// <summary>
/// Converts string to boolean (true if not empty, false if empty/null)
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
