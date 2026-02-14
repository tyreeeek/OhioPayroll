using System.Text;
using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.App.Services;

public class AchEntry
{
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string RoutingNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public BankAccountType AccountType { get; set; }
    public decimal Amount { get; set; }
}

public class AchFileService
{
    private const int RecordLength = 94;
    private const int BlockingFactor = 10;

    /// <summary>
    /// Generates a NACHA-compliant ACH file as a string.
    /// </summary>
    public string GenerateAchFile(
        string companyName,
        string companyEin,
        string companyRoutingNumber,
        string companyAccountNumber,
        string bankName,
        DateTime payDate,
        List<AchEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            throw new ArgumentException("At least one entry is required to generate an ACH file.", nameof(entries));

        if (!ValidateRoutingNumber(companyRoutingNumber))
            throw new ArgumentException($"Invalid company routing number: {companyRoutingNumber}", nameof(companyRoutingNumber));

        foreach (var entry in entries)
        {
            if (!ValidateRoutingNumber(entry.RoutingNumber))
                throw new ArgumentException($"Invalid routing number for employee {entry.EmployeeId}: {entry.RoutingNumber}");
        }

        var lines = new List<string>();
        var now = DateTime.Now;

        // File Header Record (Type 1)
        lines.Add(BuildFileHeader(companyEin, companyRoutingNumber, bankName, companyName, now));

        // Batch Header Record (Type 5)
        int batchNumber = 1;
        lines.Add(BuildBatchHeader(companyName, companyEin, companyRoutingNumber, payDate, batchNumber));

        // Entry Detail Records (Type 6)
        long entryHash = 0;
        long totalCreditAmount = 0;
        int sequenceNumber = 0;

        foreach (var entry in entries)
        {
            sequenceNumber++;
            string receivingDfi = entry.RoutingNumber.Substring(0, 8);
            entryHash += long.Parse(receivingDfi);

            long amountInCents = (long)Math.Round(entry.Amount * 100, MidpointRounding.AwayFromZero);
            totalCreditAmount += amountInCents;

            lines.Add(BuildEntryDetail(entry, companyRoutingNumber, sequenceNumber));
        }

        // Entry hash is mod 10^10
        long entryHashMod = entryHash % 10_000_000_000;

        // Batch Control Record (Type 8)
        lines.Add(BuildBatchControl(entries.Count, entryHashMod, totalCreditAmount, companyEin, companyRoutingNumber, batchNumber));

        // File Control Record (Type 9)
        int entryAddendaCount = entries.Count;
        lines.Add(BuildFileControl(batchNumber, lines.Count + 1, entryAddendaCount, entryHashMod, totalCreditAmount));

        // Pad with 9999... lines until total line count is a multiple of 10
        int totalLines = lines.Count;
        int remainder = totalLines % BlockingFactor;
        if (remainder != 0)
        {
            int paddingNeeded = BlockingFactor - remainder;
            string paddingLine = new string('9', RecordLength);
            for (int i = 0; i < paddingNeeded; i++)
            {
                lines.Add(paddingLine);
            }
        }

        // Validate every line is exactly 94 characters
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length != RecordLength)
                throw new InvalidOperationException(
                    $"Line {i + 1} is {lines[i].Length} characters, expected {RecordLength}. Content: '{lines[i]}'");
        }

        return string.Join("\r\n", lines) + "\r\n";
    }

    /// <summary>
    /// Validates an ABA routing number using the NACHA checksum algorithm.
    /// </summary>
    public static bool ValidateRoutingNumber(string routing)
    {
        if (string.IsNullOrEmpty(routing) || routing.Length != 9 || !routing.All(char.IsDigit))
            return false;

        var digits = routing.Select(c => c - '0').ToArray();
        int checksum = 3 * (digits[0] + digits[3] + digits[6])
                     + 7 * (digits[1] + digits[4] + digits[7])
                     + 1 * (digits[2] + digits[5] + digits[8]);
        return checksum % 10 == 0;
    }

    private static string BuildFileHeader(
        string companyEin, string companyRoutingNumber, string bankName, string companyName, DateTime now)
    {
        var sb = new StringBuilder(RecordLength);

        sb.Append('1');                                                          // Pos 1: Record Type
        sb.Append("01");                                                         // Pos 2-3: Priority Code
        sb.Append((' ' + companyRoutingNumber).PadRight(10).Substring(0, 10));   // Pos 4-13: Immediate Destination
        sb.Append((' ' + companyEin).PadRight(10).Substring(0, 10));             // Pos 14-23: Immediate Origin
        sb.Append(now.ToString("yyMMdd"));                                       // Pos 24-29: File Creation Date
        sb.Append(now.ToString("HHmm"));                                         // Pos 30-33: File Creation Time
        sb.Append('A');                                                          // Pos 34: File ID Modifier
        sb.Append("094");                                                        // Pos 35-37: Record Size
        sb.Append("10");                                                         // Pos 38-39: Blocking Factor
        sb.Append('1');                                                          // Pos 40: Format Code
        sb.Append(bankName.PadRight(23).Substring(0, 23));                       // Pos 41-63: Immediate Dest Name
        sb.Append(companyName.PadRight(23).Substring(0, 23));                    // Pos 64-86: Immediate Origin Name
        sb.Append(new string(' ', 8));                                           // Pos 87-94: Reference Code

        return sb.ToString();
    }

    private static string BuildBatchHeader(
        string companyName, string companyEin, string companyRoutingNumber, DateTime payDate, int batchNumber)
    {
        var sb = new StringBuilder(RecordLength);

        // Build company identification: "1" + 9-digit EIN
        string companyId = ("1" + companyEin).PadRight(10).Substring(0, 10);

        sb.Append('5');                                                          // Pos 1: Record Type
        sb.Append("220");                                                        // Pos 2-4: Service Class Code (credits only)
        sb.Append(companyName.PadRight(16).Substring(0, 16));                    // Pos 5-20: Company Name
        sb.Append(new string(' ', 20));                                          // Pos 21-40: Discretionary Data
        sb.Append(companyId);                                                    // Pos 41-50: Company Identification
        sb.Append("PPD");                                                        // Pos 51-53: Standard Entry Class
        sb.Append("PAYROLL   ");                                                 // Pos 54-63: Company Entry Description
        sb.Append(payDate.ToString("yyMMdd"));                                   // Pos 64-69: Descriptive Date
        sb.Append(payDate.ToString("yyMMdd"));                                   // Pos 70-75: Effective Entry Date
        sb.Append("   ");                                                        // Pos 76-78: Settlement Date
        sb.Append('1');                                                          // Pos 79: Originator Status Code
        sb.Append(companyRoutingNumber.Substring(0, 8));                         // Pos 80-87: Originating DFI ID
        sb.Append(batchNumber.ToString().PadLeft(7, '0'));                        // Pos 88-94: Batch Number

        return sb.ToString();
    }

    private static string BuildEntryDetail(AchEntry entry, string companyRoutingNumber, int sequenceNumber)
    {
        var sb = new StringBuilder(RecordLength);

        string transactionCode = entry.AccountType == BankAccountType.Checking ? "22" : "32";
        long amountInCents = (long)Math.Round(entry.Amount * 100, MidpointRounding.AwayFromZero);

        sb.Append('6');                                                          // Pos 1: Record Type
        sb.Append(transactionCode);                                              // Pos 2-3: Transaction Code
        sb.Append(entry.RoutingNumber.Substring(0, 8));                          // Pos 4-11: Receiving DFI ID
        sb.Append(entry.RoutingNumber[8]);                                       // Pos 12: Check Digit
        sb.Append(entry.AccountNumber.PadRight(17).Substring(0, 17));            // Pos 13-29: DFI Account Number
        sb.Append(amountInCents.ToString().PadLeft(10, '0'));                     // Pos 30-39: Amount
        sb.Append(entry.EmployeeId.PadRight(15).Substring(0, 15));               // Pos 40-54: Individual ID
        sb.Append(entry.EmployeeName.PadRight(22).Substring(0, 22));             // Pos 55-76: Individual Name
        sb.Append("  ");                                                         // Pos 77-78: Discretionary Data
        sb.Append('0');                                                          // Pos 79: Addenda Record Indicator
        sb.Append(companyRoutingNumber.Substring(0, 8));                         // Pos 80-87: Trace (routing part)
        sb.Append(sequenceNumber.ToString().PadLeft(7, '0'));                     // Pos 88-94: Trace (sequence part)

        return sb.ToString();
    }

    private static string BuildBatchControl(
        int entryCount, long entryHash, long totalCreditAmount, string companyEin,
        string companyRoutingNumber, int batchNumber)
    {
        var sb = new StringBuilder(RecordLength);

        string companyId = ("1" + companyEin).PadRight(10).Substring(0, 10);

        sb.Append('8');                                                          // Pos 1: Record Type
        sb.Append("220");                                                        // Pos 2-4: Service Class Code
        sb.Append(entryCount.ToString().PadLeft(6, '0'));                         // Pos 5-10: Entry/Addenda Count
        sb.Append(entryHash.ToString().PadLeft(10, '0'));                         // Pos 11-20: Entry Hash
        sb.Append("000000000000");                                               // Pos 21-32: Total Debit Amount
        sb.Append(totalCreditAmount.ToString().PadLeft(12, '0'));                 // Pos 33-44: Total Credit Amount
        sb.Append(companyId);                                                    // Pos 45-54: Company Identification
        sb.Append(new string(' ', 19));                                          // Pos 55-73: Message Auth Code
        sb.Append(new string(' ', 6));                                           // Pos 74-79: Reserved
        sb.Append(companyRoutingNumber.Substring(0, 8));                         // Pos 80-87: Originating DFI ID
        sb.Append(batchNumber.ToString().PadLeft(7, '0'));                        // Pos 88-94: Batch Number

        return sb.ToString();
    }

    private static string BuildFileControl(
        int batchCount, int totalLineCount, int entryAddendaCount, long entryHash, long totalCreditAmount)
    {
        // Block count: total lines (including this record) / 10, rounded up
        // totalLineCount already includes this file control record
        int blockCount = (int)Math.Ceiling(totalLineCount / (double)BlockingFactor);

        var sb = new StringBuilder(RecordLength);

        sb.Append('9');                                                          // Pos 1: Record Type
        sb.Append(batchCount.ToString().PadLeft(6, '0'));                         // Pos 2-7: Batch Count
        sb.Append(blockCount.ToString().PadLeft(6, '0'));                         // Pos 8-13: Block Count
        sb.Append(entryAddendaCount.ToString().PadLeft(8, '0'));                  // Pos 14-21: Entry/Addenda Count
        sb.Append(entryHash.ToString().PadLeft(10, '0'));                         // Pos 22-31: Entry Hash
        sb.Append("000000000000");                                               // Pos 32-43: Total Debit Amount
        sb.Append(totalCreditAmount.ToString().PadLeft(12, '0'));                 // Pos 44-55: Total Credit Amount
        sb.Append(new string(' ', 39));                                          // Pos 56-94: Reserved

        return sb.ToString();
    }
}

