using System.Globalization;
using System.Text;
using OhioPayroll.App.Documents;

namespace OhioPayroll.App.Services;

/// <summary>
/// Generates SSA EFW2-compliant files for electronic W-2 filing via BSO.
/// Each record is exactly 512 bytes. All alpha fields UPPERCASE, left-justified, space-padded.
/// All money fields right-justified, zero-filled, no decimal point.
/// </summary>
public static class Efw2FileService
{
    private const int RecordLength = 512;
    private const int RwMoneyWidth = 11;
    private const int RtMoneyWidth = 15;
    private const string OhioFips = "39";

    /// <summary>
    /// Generates a complete EFW2 file as a string.
    /// Totals are computed from w2DataList directly — never from external W3Data.
    /// </summary>
    public static string GenerateEfw2(
        string submitterEin,
        string submitterName,
        string submitterAddress,
        string submitterCity,
        string submitterState,
        string submitterZip,
        string contactName,
        string contactPhone,
        string contactEmail,
        List<W2Data> w2DataList,
        int taxYear)
    {
        if (w2DataList == null || w2DataList.Count == 0)
            throw new ArgumentException("At least one W-2 is required.", nameof(w2DataList));

        var lines = new List<string>();

        // RA — Submitter
        lines.Add(BuildRaRecord(
            submitterEin, submitterName, submitterAddress,
            submitterCity, submitterState, submitterZip,
            contactName, contactPhone, contactEmail));

        // RE — Employer (use first W2's employer info)
        var first = w2DataList[0];
        lines.Add(BuildReRecord(
            taxYear, first.EmployerEin, first.EmployerName,
            first.EmployerAddress, string.Empty,
            first.EmployerCity, first.EmployerState, first.EmployerZip));

        // RW + RS per employee (canonical pairing)
        int rwCount = 0;
        int rsCount = 0;
        foreach (var w2 in w2DataList)
        {
            lines.Add(BuildRwRecord(w2));
            rwCount++;

            if (w2.Box16StateWages > 0 || w2.Box17StateTax > 0)
            {
                lines.Add(BuildRsRecord(w2, first.StateWithholdingId));
                rsCount++;
            }
        }

        // RT — Totals (computed from w2DataList)
        lines.Add(BuildRtRecord(w2DataList, rwCount));

        // RU — State totals (only if RS records exist)
        if (rsCount > 0)
        {
            lines.Add(BuildRuRecord(w2DataList, rsCount));
        }

        // RF — Final
        lines.Add(BuildRfRecord(rwCount));

        return string.Join("\r\n", lines) + "\r\n";
    }

    // ── Record Builders ──────────────────────────────────────────────

    private static string BuildRaRecord(
        string ein, string name, string address,
        string city, string state, string zip,
        string contactName, string contactPhone, string contactEmail)
    {
        var sb = new StringBuilder(RecordLength);

        sb.Append("RA");                                    // 1-2
        sb.Append(SanitizeTin(ein));                        // 3-11
        sb.Append(Blank(4));                                // 12-15  User ID
        sb.Append(' ');                                     // 16     Software vendor code
        sb.Append(' ');                                     // 17     Blank
        sb.Append(' ');                                     // 18     Resub indicator
        sb.Append(Blank(6));                                // 19-24  Resub WFID
        sb.Append(' ');                                     // 25     Software code
        sb.Append(Alpha(name, 57));                         // 26-82  Company name
        sb.Append(Alpha(address, 22));                      // 83-104 Location address
        sb.Append(Alpha("", 22));                           // 105-126 Delivery address
        sb.Append(Alpha(city, 22));                         // 127-148 City
        sb.Append(Alpha(state, 2));                         // 149-150 State
        sb.Append(AlphaNum(ZipMain(zip), 5));               // 151-155 ZIP
        sb.Append(AlphaNum(ZipExt(zip), 4));                // 156-159 ZIP ext
        sb.Append(Blank(23));                               // 160-182
        sb.Append(Blank(2));                                // 183-184
        sb.Append(Blank(7));                                // 185-191
        sb.Append(Blank(5));                                // 192-196
        sb.Append(Alpha(contactName, 57));                  // 197-253 Contact name
        sb.Append(AlphaNum(DigitsOnly(contactPhone), 15));  // 254-268 Contact phone
        sb.Append(Blank(5));                                // 269-273 Phone ext
        sb.Append(Blank(3));                                // 274-276
        sb.Append(FormatEmail(contactEmail, 40));           // 277-316 Email (mixed case OK)
        sb.Append(' ');                                     // 317
        sb.Append(Blank(10));                               // 318-327 Fax
        sb.Append(' ');                                     // 328
        sb.Append('L');                                     // 329     Preparer code (self-prepared)
        sb.Append(Blank(183));                              // 330-512

        return ValidateRecord(sb.ToString(), "RA");
    }

    private static string BuildReRecord(
        int taxYear, string ein, string name,
        string address, string deliveryAddress,
        string city, string state, string zip)
    {
        var sb = new StringBuilder(RecordLength);

        sb.Append("RE");                                    // 1-2
        sb.Append(taxYear.ToString("D4"));                  // 3-6
        sb.Append(' ');                                     // 7      Agent indicator
        sb.Append(SanitizeTin(ein));                        // 8-16
        sb.Append(' ');                                     // 17     Agent for EIN
        sb.Append(' ');                                     // 18     Terminating business
        sb.Append(Blank(4));                                // 19-22  Establishment number
        sb.Append(' ');                                     // 23     Other EIN
        sb.Append(Alpha(name, 57));                         // 24-80
        sb.Append(Alpha(address, 22));                      // 81-102 Location address
        sb.Append(Alpha(deliveryAddress, 22));              // 103-124 Delivery address
        sb.Append(Alpha(city, 22));                         // 125-146
        sb.Append(Alpha(state, 2));                         // 147-148
        sb.Append(AlphaNum(ZipMain(zip), 5));               // 149-153
        sb.Append(AlphaNum(ZipExt(zip), 4));                // 154-157
        sb.Append('N');                                     // 158     Kind of employer
        sb.Append(Blank(9));                                // 159-167
        sb.Append(Blank(8));                                // 168-175
        sb.Append(Blank(2));                                // 176-177
        sb.Append(Blank(335));                              // 178-512

        return ValidateRecord(sb.ToString(), "RE");
    }

    private static string BuildRwRecord(W2Data w2)
    {
        var sb = new StringBuilder(RecordLength);

        sb.Append("RW");                                    // 1-2
        sb.Append(SanitizeTin(w2.EmployeeSsn));            // 3-11
        sb.Append(Alpha(w2.EmployeeFirstName, 15));         // 12-26
        sb.Append(Alpha("", 15));                           // 27-41  Middle name
        sb.Append(Alpha(w2.EmployeeLastName, 20));          // 42-61
        sb.Append(Alpha("", 4));                            // 62-65  Suffix
        sb.Append(Alpha(w2.EmployeeAddress, 22));           // 66-87  Location address
        sb.Append(Alpha("", 22));                           // 88-109 Delivery address
        sb.Append(Alpha(w2.EmployeeCity, 22));              // 110-131
        sb.Append(Alpha(w2.EmployeeState, 2));              // 132-133
        sb.Append(AlphaNum(ZipMain(w2.EmployeeZip), 5));    // 134-138
        sb.Append(AlphaNum(ZipExt(w2.EmployeeZip), 4));     // 139-142
        sb.Append(' ');                                     // 143
        sb.Append(Money11(w2.Box1WagesTips));               // 144-154 Box 1
        sb.Append(Money11(w2.Box2FederalTaxWithheld));      // 155-165 Box 2
        sb.Append(Money11(w2.Box3SocialSecurityWages));     // 166-176 Box 3
        sb.Append(Money11(w2.Box4SocialSecurityTax));       // 177-187 Box 4
        sb.Append(Money11(w2.Box5MedicareWages));           // 188-198 Box 5
        sb.Append(Money11(w2.Box6MedicareTax));             // 199-209 Box 6
        sb.Append(Money11(0m));                             // 210-220 Box 7 SS tips
        sb.Append(Money11(0m));                             // 221-231 Box 8 Allocated tips
        sb.Append(Money11(0m));                             // 232-242 Box 9 (blank/zero)
        sb.Append(Money11(0m));                             // 243-253 Box 10 Dependent care
        sb.Append(Money11(0m));                             // 254-264 Box 11 Deferred comp
        sb.Append(Blank(12));                               // 265-276 Box 12a code(2) + amount(10) — unused
        sb.Append(Blank(11));                               // 277-287 Box 12b
        sb.Append(Blank(11));                               // 288-298 Box 12c
        sb.Append(Blank(11));                               // 299-309 Box 12d
        sb.Append('0');                                     // 310     Statutory employee
        sb.Append('0');                                     // 311     Retirement plan
        sb.Append('0');                                     // 312     Third-party sick pay
        sb.Append(Blank(200));                              // 313-512

        return ValidateRecord(sb.ToString(), "RW");
    }

    private static string BuildRsRecord(W2Data w2, string stateEin)
    {
        var sb = new StringBuilder(RecordLength);

        sb.Append("RS");                                    // 1-2
        sb.Append(OhioFips);                                // 3-4    State FIPS = 39
        sb.Append(Blank(5));                                // 5-9    Taxing entity code
        sb.Append(SanitizeTin(w2.EmployeeSsn));            // 10-18
        sb.Append(Alpha(w2.EmployeeFirstName, 15));         // 19-33
        sb.Append(Alpha("", 15));                           // 34-48  Middle name
        sb.Append(Alpha(w2.EmployeeLastName, 20));          // 49-68
        sb.Append(Alpha("", 4));                            // 69-72  Suffix
        sb.Append(Alpha(w2.EmployeeAddress, 22));           // 73-94
        sb.Append(Alpha("", 22));                           // 95-116 Delivery address
        sb.Append(Alpha(w2.EmployeeCity, 22));              // 117-138
        sb.Append(Alpha(w2.EmployeeState, 2));              // 139-140
        sb.Append(AlphaNum(ZipMain(w2.EmployeeZip), 5));    // 141-145
        sb.Append(AlphaNum(ZipExt(w2.EmployeeZip), 4));     // 146-149
        sb.Append(Money11(w2.Box16StateWages));             // 150-160 State wages
        sb.Append(Money11(w2.Box17StateTax));               // 161-171 State tax
        sb.Append(Blank(12));                               // 172-183
        sb.Append(Alpha(stateEin, 20));                     // 184-203 State employer ID
        sb.Append(Money11(w2.Box18LocalWages));             // 204-214 Local wages
        sb.Append(Money11(w2.Box19LocalTax));               // 215-225 Local tax
        sb.Append(Blank(20));                               // 226-245 State control number
        sb.Append(Blank(267));                              // 246-512

        return ValidateRecord(sb.ToString(), "RS");
    }

    private static string BuildRtRecord(List<W2Data> w2List, int rwCount)
    {
        var sb = new StringBuilder(RecordLength);

        sb.Append("RT");                                                    // 1-2
        sb.Append(rwCount.ToString().PadLeft(7, '0'));                      // 3-9
        sb.Append(Money15(w2List.Sum(w => w.Box1WagesTips)));               // 10-24  Total Box 1
        sb.Append(Money15(w2List.Sum(w => w.Box2FederalTaxWithheld)));      // 25-39  Total Box 2
        sb.Append(Money15(w2List.Sum(w => w.Box3SocialSecurityWages)));     // 40-54  Total Box 3
        sb.Append(Money15(w2List.Sum(w => w.Box4SocialSecurityTax)));       // 55-69  Total Box 4
        sb.Append(Money15(w2List.Sum(w => w.Box5MedicareWages)));           // 70-84  Total Box 5
        sb.Append(Money15(w2List.Sum(w => w.Box6MedicareTax)));             // 85-99  Total Box 6
        sb.Append(Money15(0m));                                             // 100-114 Total SS tips
        sb.Append(Money15(0m));                                             // 115-129
        sb.Append(Money15(0m));                                             // 130-144
        sb.Append(Money15(0m));                                             // 145-159 Dependent care
        sb.Append(Money15(0m));                                             // 160-174 Deferred comp
        sb.Append(Money15(0m));                                             // 175-189
        sb.Append(Blank(323));                                              // 190-512

        return ValidateRecord(sb.ToString(), "RT");
    }

    private static string BuildRuRecord(List<W2Data> w2List, int rsCount)
    {
        // Only sum state data for employees that had RS records
        var stateW2s = w2List.Where(w => w.Box16StateWages > 0 || w.Box17StateTax > 0).ToList();

        var sb = new StringBuilder(RecordLength);

        sb.Append("RU");                                                    // 1-2
        sb.Append(rsCount.ToString().PadLeft(7, '0'));                      // 3-9
        sb.Append(Money15(stateW2s.Sum(w => w.Box16StateWages)));           // 10-24
        sb.Append(Money15(stateW2s.Sum(w => w.Box17StateTax)));             // 25-39
        sb.Append(Money15(0m));                                             // 40-54
        sb.Append(Blank(20));                                               // 55-74
        sb.Append(Money15(stateW2s.Sum(w => w.Box18LocalWages)));           // 75-89
        sb.Append(Money15(stateW2s.Sum(w => w.Box19LocalTax)));             // 90-104
        sb.Append(Blank(20));                                               // 105-124
        sb.Append(AlphaNum(OhioFips, 2));                                   // 125-126 State code
        sb.Append(Blank(8));                                                // 127-134
        sb.Append(Blank(378));                                              // 135-512

        return ValidateRecord(sb.ToString(), "RU");
    }

    private static string BuildRfRecord(int totalRwCount)
    {
        var sb = new StringBuilder(RecordLength);

        sb.Append("RF");                                                    // 1-2
        sb.Append(' ');                                                     // 3
        sb.Append(totalRwCount.ToString().PadLeft(7, '0'));                 // 4-10
        sb.Append(Blank(502));                                              // 11-512

        return ValidateRecord(sb.ToString(), "RF");
    }

    // ── Formatting Helpers ───────────────────────────────────────────

    private static string Money11(decimal value)
    {
        var cents = (long)Math.Round(value * 100, MidpointRounding.AwayFromZero);
        if (cents < 0)
            throw new ArgumentException($"Negative amount not allowed in EFW2: {value}");
        var result = cents.ToString().PadLeft(RwMoneyWidth, '0');
        if (result.Length > RwMoneyWidth)
            throw new InvalidOperationException($"Amount {value} exceeds {RwMoneyWidth}-digit field capacity.");
        return result;
    }

    private static string Money15(decimal value)
    {
        var cents = (long)Math.Round(value * 100, MidpointRounding.AwayFromZero);
        if (cents < 0)
            throw new ArgumentException($"Negative amount not allowed in EFW2: {value}");
        var result = cents.ToString().PadLeft(RtMoneyWidth, '0');
        if (result.Length > RtMoneyWidth)
            throw new InvalidOperationException($"Amount {value} exceeds {RtMoneyWidth}-digit field capacity.");
        return result;
    }

    /// <summary>
    /// Normalize, uppercase, left-justify, space-pad/truncate to exact width.
    /// </summary>
    private static string Alpha(string? value, int width)
    {
        var normalized = NormalizeAscii(value ?? "").ToUpperInvariant();
        return normalized.PadRight(width).Substring(0, width);
    }

    /// <summary>
    /// Left-justify, space-pad/truncate — no uppercasing (for numeric-ish fields like ZIP).
    /// </summary>
    private static string AlphaNum(string? value, int width)
    {
        var normalized = NormalizeAscii(value ?? "");
        return normalized.PadRight(width).Substring(0, width);
    }

    /// <summary>
    /// Email fields preserve case per SSA spec.
    /// </summary>
    private static string FormatEmail(string? value, int width)
    {
        var normalized = NormalizeAscii(value ?? "");
        return normalized.PadRight(width).Substring(0, width);
    }

    private static string Blank(int width) => new string(' ', width);

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
    /// Strip accented characters, smart quotes, and non-printable Unicode.
    /// Replaces with ASCII equivalents where possible.
    /// </summary>
    private static string NormalizeAscii(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Normalize to FormD to decompose accented characters
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            // Skip combining marks (accents)
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            // Replace smart quotes and special punctuation
            switch (c)
            {
                case '\u2018' or '\u2019' or '\u201A' or '\u201B': // smart single quotes
                    sb.Append('\'');
                    break;
                case '\u201C' or '\u201D' or '\u201E' or '\u201F': // smart double quotes
                    sb.Append('"');
                    break;
                case '\u2013' or '\u2014': // en-dash, em-dash
                    sb.Append('-');
                    break;
                case '\u2026': // ellipsis
                    sb.Append("...");
                    break;
                default:
                    // Only keep printable ASCII (32-126)
                    if (c >= 32 && c <= 126)
                        sb.Append(c);
                    else if (c == '\t')
                        sb.Append(' ');
                    // else: drop non-ASCII non-printable
                    break;
            }
        }

        return sb.ToString();
    }

    private static string ZipMain(string? zip)
    {
        var digits = new string((zip ?? "").Where(char.IsDigit).ToArray());
        return digits.Length >= 5 ? digits[..5] : digits;
    }

    private static string ZipExt(string? zip)
    {
        var digits = new string((zip ?? "").Where(char.IsDigit).ToArray());
        return digits.Length > 5 ? digits[5..Math.Min(digits.Length, 9)] : "";
    }

    private static string DigitsOnly(string? value)
    {
        return new string((value ?? "").Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Validates record is exactly 512 characters. Throws on deviation.
    /// </summary>
    private static string ValidateRecord(string record, string recordType)
    {
        if (record.Length != RecordLength)
            throw new InvalidOperationException(
                $"EFW2 {recordType} record is {record.Length} characters, expected {RecordLength}.");
        return record;
    }
}
