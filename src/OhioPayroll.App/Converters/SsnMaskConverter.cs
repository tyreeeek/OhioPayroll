using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OhioPayroll.App.Converters;

/// <summary>
/// Converts the last 4 digits of an SSN into a masked display format: ***-**-XXXX.
/// Expects a string of up to 4 characters (the last four digits of the SSN).
/// </summary>
public class SsnMaskConverter : IValueConverter
{
    public static readonly SsnMaskConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string last4 && last4.Length > 0)
        {
            // Pad to 4 digits in case fewer were stored
            var padded = last4.PadLeft(4, '*');
            return $"***-**-{padded}";
        }

        return "***-**-****";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // One-way converter; parsing masked SSN back is not supported
        throw new NotSupportedException("SsnMaskConverter does not support ConvertBack.");
    }
}

