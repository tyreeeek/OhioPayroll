using FluentAssertions;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Data.Tests;

public class AchFileServiceTests
{
    private readonly AchFileService _service = new();

    // Known valid routing numbers (pass NACHA checksum)
    private const string CompanyRouting = "021000021";  // JPMorgan Chase
    private const string EmployeeRouting = "011401533"; // Bank of America
    private const string CompanyEin = "311234567";
    private const string CompanyAccount = "123456789";
    private const string CompanyName = "ACME CORP";
    private const string BankName = "JPMORGAN CHASE";

    private static readonly DateTime PayDate = new(2026, 1, 15);

    private static AchEntry MakeEntry(
        string name = "DOE JOHN",
        string id = "EMP001",
        string routing = EmployeeRouting,
        string account = "9876543210",
        BankAccountType accountType = BankAccountType.Checking,
        decimal amount = 1234.56m)
    {
        return new AchEntry
        {
            EmployeeName = name,
            EmployeeId = id,
            RoutingNumber = routing,
            AccountNumber = account,
            AccountType = accountType,
            Amount = amount
        };
    }

    private string GenerateWithDefaults(List<AchEntry>? entries = null)
    {
        return _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, entries ?? new List<AchEntry> { MakeEntry() });
    }

    // ---------------------------------------------------------------
    // Test 1: Single entry generates a valid file with correct structure
    // ---------------------------------------------------------------
    [Fact]
    public void SingleEntry_GeneratesValidFile()
    {
        var result = GenerateWithDefaults();

        result.Should().NotBeNullOrEmpty();

        var lines = result.TrimEnd('\n').Split('\n');

        // Should have: File Header (1) + Batch Header (1) + Entry (1) + Batch Control (1) + File Control (1) = 5
        // Padded to next multiple of 10 = 10
        lines.Should().HaveCount(10);

        // First record is file header
        lines[0][0].Should().Be('1');
        // Second is batch header
        lines[1][0].Should().Be('5');
        // Third is entry detail
        lines[2][0].Should().Be('6');
        // Fourth is batch control
        lines[3][0].Should().Be('8');
        // Fifth is file control
        lines[4][0].Should().Be('9');
    }

    // ---------------------------------------------------------------
    // Test 2: Every line is exactly 94 characters
    // ---------------------------------------------------------------
    [Fact]
    public void AllLines_AreExactly94Characters()
    {
        var entries = new List<AchEntry>
        {
            MakeEntry(name: "SMITH JANE", id: "EMP001", amount: 500.00m),
            MakeEntry(name: "DOE JOHN", id: "EMP002", amount: 1500.75m),
            MakeEntry(name: "WILLIAMS BOB", id: "EMP003", amount: 2000.00m)
        };

        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, entries);

        var lines = result.TrimEnd('\n').Split('\n');

        foreach (var line in lines)
        {
            line.Should().HaveLength(94, $"line content: '{line}'");
        }
    }

    // ---------------------------------------------------------------
    // Test 3: Total line count is a multiple of 10 (block padding)
    // ---------------------------------------------------------------
    [Fact]
    public void TotalLineCount_IsMultipleOf10()
    {
        // Test with various entry counts to ensure padding works
        for (int count = 1; count <= 12; count++)
        {
            var entries = Enumerable.Range(1, count)
                .Select(i => MakeEntry(id: $"EMP{i:D3}", amount: 100.00m * i))
                .ToList();

            var result = _service.GenerateAchFile(
                CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
                BankName, PayDate, entries);

            var lines = result.TrimEnd('\n').Split('\n');
            (lines.Length % 10).Should().Be(0, $"with {count} entries, got {lines.Length} lines");
        }
    }

    // ---------------------------------------------------------------
    // Test 4: File header has correct record type "1" and format
    // ---------------------------------------------------------------
    [Fact]
    public void FileHeader_HasCorrectRecordTypeAndFormat()
    {
        var result = GenerateWithDefaults();
        var header = result.TrimEnd('\n').Split('\n')[0];

        header[0].Should().Be('1');                               // Record Type
        header.Substring(1, 2).Should().Be("01");                 // Priority Code
        header.Substring(3, 10).Should().StartWith(" ");          // Immediate Destination starts with space
        header.Substring(3, 10).Trim().Should().Be(CompanyRouting);
        header.Substring(34 - 1, 1).Should().Be("A");             // File ID Modifier
        header.Substring(35 - 1, 3).Should().Be("094");           // Record Size
        header.Substring(38 - 1, 2).Should().Be("10");            // Blocking Factor
        header.Substring(40 - 1, 1).Should().Be("1");             // Format Code
        header.Substring(40, 23).TrimEnd().Should().Be(BankName); // Immediate Dest Name
    }

    // ---------------------------------------------------------------
    // Test 5: Batch header has correct service class code "220"
    // ---------------------------------------------------------------
    [Fact]
    public void BatchHeader_HasServiceClassCode220()
    {
        var result = GenerateWithDefaults();
        var batchHeader = result.TrimEnd('\n').Split('\n')[1];

        batchHeader[0].Should().Be('5');                   // Record Type
        batchHeader.Substring(1, 3).Should().Be("220");    // Service Class Code (credits only)
    }

    // ---------------------------------------------------------------
    // Test 6: Entry detail uses "22" for checking, "32" for savings
    // ---------------------------------------------------------------
    [Fact]
    public void EntryDetail_CheckingUses22_SavingsUses32()
    {
        var checkingEntry = MakeEntry(id: "EMP001", accountType: BankAccountType.Checking);
        var savingsEntry = MakeEntry(id: "EMP002", accountType: BankAccountType.Savings);

        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, new List<AchEntry> { checkingEntry, savingsEntry });

        var lines = result.TrimEnd('\n').Split('\n');

        // Entry lines are at index 2 and 3
        lines[2].Substring(1, 2).Should().Be("22"); // Checking credit
        lines[3].Substring(1, 2).Should().Be("32"); // Savings credit
    }

    // ---------------------------------------------------------------
    // Test 7: Amount formatting: $1,234.56 becomes "0000123456"
    // ---------------------------------------------------------------
    [Fact]
    public void AmountFormatting_ConvertsToZeroPaddedCents()
    {
        var entry = MakeEntry(amount: 1234.56m);
        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, new List<AchEntry> { entry });

        var entryLine = result.TrimEnd('\n').Split('\n')[2];
        // Amount is at positions 30-39 (0-indexed: 29..38)
        entryLine.Substring(29, 10).Should().Be("0000123456");
    }

    // ---------------------------------------------------------------
    // Test 8: Entry hash is calculated correctly
    // ---------------------------------------------------------------
    [Fact]
    public void EntryHash_IsCalculatedCorrectly()
    {
        // EmployeeRouting "011401533" -> first 8 digits = 01140153
        // Two entries with same routing:
        // Hash = 01140153 + 01140153 = 02280306
        var entries = new List<AchEntry>
        {
            MakeEntry(id: "EMP001"),
            MakeEntry(id: "EMP002")
        };

        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, entries);

        var lines = result.TrimEnd('\n').Split('\n');

        // Batch control is at index 4 (header + batch header + 2 entries + batch control)
        var batchControl = lines[4];
        batchControl[0].Should().Be('8');

        // Entry hash at positions 11-20 (0-indexed: 10..19)
        long expectedHash = 1140153L + 1140153L; // 2280306
        batchControl.Substring(10, 10).Should().Be(expectedHash.ToString().PadLeft(10, '0'));

        // File control at index 5
        var fileControl = lines[5];
        fileControl[0].Should().Be('9');
        // Entry hash at positions 22-31 (0-indexed: 21..30)
        fileControl.Substring(21, 10).Should().Be(expectedHash.ToString().PadLeft(10, '0'));
    }

    // ---------------------------------------------------------------
    // Test 9: Multiple entries produce correct batch control totals
    // ---------------------------------------------------------------
    [Fact]
    public void MultipleEntries_CorrectBatchControlTotals()
    {
        var entries = new List<AchEntry>
        {
            MakeEntry(id: "EMP001", amount: 1000.00m),
            MakeEntry(id: "EMP002", amount: 2500.50m),
            MakeEntry(id: "EMP003", amount: 750.25m)
        };

        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, entries);

        var lines = result.TrimEnd('\n').Split('\n');
        var batchControl = lines[5]; // header + batch header + 3 entries + batch control

        // Entry/Addenda count at positions 5-10 (0-indexed: 4..9)
        batchControl.Substring(4, 6).Should().Be("000003");

        // Total debit at positions 21-32 (0-indexed: 20..31) should be zero
        batchControl.Substring(20, 12).Should().Be("000000000000");

        // Total credit at positions 33-44 (0-indexed: 32..43)
        // $1000.00 + $2500.50 + $750.25 = $4250.75 = 425075 cents
        batchControl.Substring(32, 12).Should().Be("000000425075");
    }

    // ---------------------------------------------------------------
    // Test 10: Routing number validation - valid passes, invalid fails
    // ---------------------------------------------------------------
    [Fact]
    public void RoutingNumberValidation_ValidPassesInvalidFails()
    {
        // Known valid routing numbers
        AchFileService.ValidateRoutingNumber("021000021").Should().BeTrue();  // JPMorgan Chase
        AchFileService.ValidateRoutingNumber("011401533").Should().BeTrue();  // Bank of America

        // Invalid: wrong checksum
        AchFileService.ValidateRoutingNumber("021000022").Should().BeFalse();
        AchFileService.ValidateRoutingNumber("123456789").Should().BeFalse();

        // Invalid: wrong length
        AchFileService.ValidateRoutingNumber("0210000").Should().BeFalse();
        AchFileService.ValidateRoutingNumber("02100002100").Should().BeFalse();

        // Invalid: non-digits
        AchFileService.ValidateRoutingNumber("02100002A").Should().BeFalse();

        // Invalid: null/empty
        AchFileService.ValidateRoutingNumber("").Should().BeFalse();
        AchFileService.ValidateRoutingNumber(null!).Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Test 11: Zero entries throws ArgumentException
    // ---------------------------------------------------------------
    [Fact]
    public void ZeroEntries_ThrowsArgumentException()
    {
        var act = () => _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, new List<AchEntry>());

        act.Should().Throw<ArgumentException>()
           .WithMessage("*at least one entry*");
    }

    // ---------------------------------------------------------------
    // Test 12: Large amount ($99,999.99) formats correctly
    // ---------------------------------------------------------------
    [Fact]
    public void LargeAmount_FormatsCorrectly()
    {
        var entry = MakeEntry(amount: 99_999.99m);
        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, new List<AchEntry> { entry });

        var entryLine = result.TrimEnd('\n').Split('\n')[2];
        entryLine.Substring(29, 10).Should().Be("0009999999");
    }

    // ---------------------------------------------------------------
    // Test 13: Invalid company routing number throws
    // ---------------------------------------------------------------
    [Fact]
    public void InvalidCompanyRouting_ThrowsArgumentException()
    {
        var act = () => _service.GenerateAchFile(
            CompanyName, CompanyEin, "123456789", CompanyAccount,
            BankName, PayDate, new List<AchEntry> { MakeEntry() });

        act.Should().Throw<ArgumentException>()
           .WithMessage("*routing number*");
    }

    // ---------------------------------------------------------------
    // Test 14: Invalid employee routing number throws
    // ---------------------------------------------------------------
    [Fact]
    public void InvalidEmployeeRouting_ThrowsArgumentException()
    {
        var invalidEntry = MakeEntry(routing: "999999999");

        var act = () => _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, new List<AchEntry> { invalidEntry });

        act.Should().Throw<ArgumentException>()
           .WithMessage("*routing number*");
    }

    // ---------------------------------------------------------------
    // Test 15: Batch header contains PPD and PAYROLL
    // ---------------------------------------------------------------
    [Fact]
    public void BatchHeader_ContainsPPDAndPayroll()
    {
        var result = GenerateWithDefaults();
        var batchHeader = result.TrimEnd('\n').Split('\n')[1];

        // PPD at positions 51-53 (0-indexed: 50..52)
        batchHeader.Substring(50, 3).Should().Be("PPD");

        // PAYROLL at positions 54-63 (0-indexed: 53..62)
        batchHeader.Substring(53, 10).Should().Be("PAYROLL   ");
    }

    // ---------------------------------------------------------------
    // Test 16: File control batch count and block count
    // ---------------------------------------------------------------
    [Fact]
    public void FileControl_HasCorrectBatchAndBlockCount()
    {
        var result = GenerateWithDefaults();
        var lines = result.TrimEnd('\n').Split('\n');

        // Find the file control record (first line starting with '9' that isn't padding)
        var fileControl = lines[4]; // header + batch header + 1 entry + batch control + file control
        fileControl[0].Should().Be('9');

        // Batch count at positions 2-7 (0-indexed: 1..6)
        fileControl.Substring(1, 6).Should().Be("000001");

        // Block count at positions 8-13 (0-indexed: 7..12)
        // 5 real lines + 5 padding = 10 lines = 1 block
        fileControl.Substring(7, 6).Should().Be("000001");
    }

    // ---------------------------------------------------------------
    // Test 17: Entry detail trace number format
    // ---------------------------------------------------------------
    [Fact]
    public void EntryDetail_TraceNumberFormat()
    {
        var result = GenerateWithDefaults();
        var entryLine = result.TrimEnd('\n').Split('\n')[2];

        // Trace number at positions 80-94 (0-indexed: 79..93)
        string traceNumber = entryLine.Substring(79, 15);
        // First 8 digits should be company routing prefix
        traceNumber.Substring(0, 8).Should().Be(CompanyRouting.Substring(0, 8));
        // Last 7 should be sequence number (0000001 for first entry)
        traceNumber.Substring(8, 7).Should().Be("0000001");
    }

    // ---------------------------------------------------------------
    // Test 18: Padding lines are all 9s
    // ---------------------------------------------------------------
    [Fact]
    public void PaddingLines_AreAll9s()
    {
        var result = GenerateWithDefaults();
        var lines = result.TrimEnd('\n').Split('\n');

        // With 1 entry we have 5 real lines + 5 padding lines
        string expectedPadding = new string('9', 94);
        for (int i = 5; i < 10; i++)
        {
            lines[i].Should().Be(expectedPadding, $"padding line at index {i}");
        }
    }

    // ---------------------------------------------------------------
    // Test 19: Exactly 6 entries requires no padding (6+4 = 10 lines)
    // ---------------------------------------------------------------
    [Fact]
    public void SixEntries_ProducesExactly10Lines_NoPaddingNeeded()
    {
        // 1 file header + 1 batch header + 6 entries + 1 batch control + 1 file control = 10
        var entries = Enumerable.Range(1, 6)
            .Select(i => MakeEntry(id: $"EMP{i:D3}", amount: 100.00m * i))
            .ToList();

        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, entries);

        var lines = result.TrimEnd('\n').Split('\n');
        lines.Should().HaveCount(10);

        // Last line should be file control, not padding
        lines[9][0].Should().Be('9');
        // But it should NOT be all 9s — it should be a proper file control record
        lines[9].Substring(1, 6).Should().Be("000001"); // batch count
    }

    // ---------------------------------------------------------------
    // Test 20: Small amount ($0.01) formats correctly
    // ---------------------------------------------------------------
    [Fact]
    public void SmallAmount_OneCent_FormatsCorrectly()
    {
        var entry = MakeEntry(amount: 0.01m);
        var result = _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, new List<AchEntry> { entry });

        var entryLine = result.TrimEnd('\n').Split('\n')[2];
        entryLine.Substring(29, 10).Should().Be("0000000001");
    }

    // ---------------------------------------------------------------
    // Test 21: Null entries list throws ArgumentException
    // ---------------------------------------------------------------
    [Fact]
    public void NullEntries_ThrowsArgumentException()
    {
        var act = () => _service.GenerateAchFile(
            CompanyName, CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, null!);

        act.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // Test 22: Company name truncated to 16 chars in batch header
    // ---------------------------------------------------------------
    [Fact]
    public void LongCompanyName_TruncatedInBatchHeader()
    {
        var entries = new List<AchEntry> { MakeEntry() };
        var result = _service.GenerateAchFile(
            "A VERY LONG COMPANY NAME EXCEEDING LIMIT", CompanyEin, CompanyRouting, CompanyAccount,
            BankName, PayDate, entries);

        var batchHeader = result.TrimEnd('\n').Split('\n')[1];
        // Company name at positions 5-20 (0-indexed: 4..19) = 16 chars
        batchHeader.Substring(4, 16).Should().Be("A VERY LONG COMP");
    }
}
