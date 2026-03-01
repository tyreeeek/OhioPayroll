using System.Globalization;
using System.Text;
using OhioPayroll.App.Documents;

namespace OhioPayroll.App.Services;

/// <summary>
/// Generates IRS IRIS-compliant CSV files for electronic 1099-NEC filing.
/// Header must match IRIS template byte-for-byte. Max 100 records per file.
/// </summary>
public static class IrisCsvService
{
    public const int MaxRecordsPerFile = 100;

    private const string Header =
        "Payee TIN,Payee TIN Type,Payee Name Line 1,Payee Name Line 2," +
        "Payee Address Line 1,Payee Address Line 2,Payee City,Payee State,Payee ZIP Code," +
        "Payee Foreign Country Indicator,Payee Foreign Province,Payee Foreign Postal Code," +
        "Payee Foreign Country Name,Box 1,Box 2,Box 4," +
        "Payer TIN,Payer TIN Type,Payer Name Line 1,Payer Name Line 2," +
        "Payer Address Line 1,Payer Address Line 2,Payer City,Payer State,Payer ZIP Code," +
        "Payer Phone Number,Tax Year,Corrected Return Indicator,Last Filed Return Indicator," +
        "Direct Sales Indicator,FATCA Filing Requirement Indicator,Special Data Entry," +
        "State Income Tax Withheld 1,State 1,Payer State Number 1,State Income 1," +
        "State Income Tax Withheld 2,State 2,Payer State Number 2,State Income 2";

    private static readonly int HeaderColumnCount = Header.Split(',').Length;

    /// <summary>
    /// Generates IRIS CSV content for 1099-NEC filing.
    /// </summary>
    public static string GenerateIrisCsv(List<Form1099NecData> form1099DataList, int taxYear)
    {
        if (form1099DataList == null || form1099DataList.Count == 0)
            throw new ArgumentException("At least one 1099-NEC is required.", nameof(form1099DataList));

        if (form1099DataList.Count > MaxRecordsPerFile)
            throw new ArgumentException(
                $"IRIS CSV supports a maximum of {MaxRecordsPerFile} records per file. " +
                $"Got {form1099DataList.Count}.", nameof(form1099DataList));

        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var data in form1099DataList)
        {
            var row = BuildDataRow(data, taxYear);
            var columns = row.Split(',').Length;

            // The count check must account for CSV escaping — count unescaped commas
            // Instead, validate by counting fields before joining
            sb.AppendLine(row);
        }

        return sb.ToString();
    }

    private static string BuildDataRow(Form1099NecData data, int taxYear)
    {
        var payeeTin = SanitizeTin(data.RecipientTin);
        var payerTin = SanitizeTin(data.PayerTin);

        var fields = new string[]
        {
            payeeTin,                                               // Payee TIN
            data.RecipientTinIsEin ? "1" : "2",                    // Payee TIN Type (1=EIN, 2=SSN)
            CsvEscape(data.RecipientName),                         // Payee Name Line 1
            "",                                                    // Payee Name Line 2
            CsvEscape(data.RecipientAddress),                      // Payee Address Line 1
            "",                                                    // Payee Address Line 2
            CsvEscape(data.RecipientCity),                         // Payee City
            data.RecipientState,                                   // Payee State
            data.RecipientZip,                                     // Payee ZIP Code
            "0",                                                   // Payee Foreign Country Indicator
            "",                                                    // Payee Foreign Province
            "",                                                    // Payee Foreign Postal Code
            "",                                                    // Payee Foreign Country Name
            FormatAmount(data.Box1_NonemployeeCompensation),       // Box 1
            "",                                                    // Box 2 (unused for NEC)
            FormatAmount(data.Box4_FederalTaxWithheld),            // Box 4
            payerTin,                                              // Payer TIN
            "1",                                                   // Payer TIN Type (always EIN)
            CsvEscape(data.PayerName),                             // Payer Name Line 1
            "",                                                    // Payer Name Line 2
            CsvEscape(data.PayerAddress),                          // Payer Address Line 1
            "",                                                    // Payer Address Line 2
            CsvEscape(data.PayerCity),                             // Payer City
            data.PayerState,                                       // Payer State
            data.PayerZip,                                         // Payer ZIP Code
            DigitsOnly(data.PayerPhone),                           // Payer Phone Number
            taxYear.ToString("D4"),                                // Tax Year
            "0",                                                   // Corrected Return Indicator
            "0",                                                   // Last Filed Return Indicator
            "0",                                                   // Direct Sales Indicator
            "0",                                                   // FATCA Filing Requirement
            "",                                                    // Special Data Entry
            FormatAmount(data.StateTaxWithheld),                   // State Income Tax Withheld 1
            data.StateCode,                                        // State 1
            CsvEscape(data.StatePayerNo),                          // Payer State Number 1
            FormatAmount(data.StateIncome),                        // State Income 1
            "",                                                    // State Income Tax Withheld 2
            "",                                                    // State 2
            "",                                                    // Payer State Number 2
            "",                                                    // State Income 2
        };

        if (fields.Length != HeaderColumnCount)
            throw new InvalidOperationException(
                $"IRIS CSV data row has {fields.Length} columns, expected {HeaderColumnCount}.");

        return string.Join(",", fields);
    }

    // ── Formatting Helpers ───────────────────────────────────────────

    private static string FormatAmount(decimal value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Strip non-digit characters. Throw if result is not exactly 9 digits.
    /// </summary>
    public static string SanitizeTin(string raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 9)
            throw new ArgumentException(
                $"TIN must be exactly 9 digits after stripping non-digits. Got {digits.Length} digits from '{raw}'.");
        return digits;
    }

    /// <summary>
    /// CSV-escape a field: wrap in double quotes only if it contains comma,
    /// double-quote, or line break. Inner double-quotes are doubled.
    /// </summary>
    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        bool needsQuoting = value.Contains(',') || value.Contains('"')
            || value.Contains('\n') || value.Contains('\r');

        if (!needsQuoting)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string DigitsOnly(string? value)
    {
        return new string((value ?? "").Where(char.IsDigit).ToArray());
    }
}
