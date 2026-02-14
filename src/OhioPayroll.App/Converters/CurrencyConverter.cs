using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OhioPayroll.App.Converters;

/// <summary>
/// Converts a decimal value to a currency-formatted string ($#,##0.00)
/// and parses currency strings back to decimal.
/// </summary>
public class CurrencyConverter : IValueConverter
{
    public static readonly CurrencyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d.ToString("$#,##0.00", CultureInfo.InvariantCulture);

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            // Strip currency symbol, commas, and whitespace before parsing
            var cleaned = s.Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;
        }

        return 0m;
    }
}

