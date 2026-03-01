using FluentAssertions;
using OhioPayroll.App.Documents;
using OhioPayroll.App.Services;

namespace OhioPayroll.Engine.Tests;

public class IrisCsvServiceTests
{
    [Fact]
    public void GenerateIrisCsv_WithSingleRecord_ReturnsValidCsv()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("ABC Construction", "123456789", true, 15000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
        lines.Should().HaveCountGreaterThanOrEqualTo(2, "should have header + data row");

        var header = lines[0];
        header.Should().StartWith("Payee TIN,Payee TIN Type,");
        header.Should().Contain("Box 1,Box 2,Box 4,");
        header.Should().Contain("Tax Year,");
    }

    [Fact]
    public void GenerateIrisCsv_HeaderExactMatch()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Test Contractor", "111111111", false, 10000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
        var header = lines[0];

        // Assert - exact header from spec
        const string expectedHeader =
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

        header.Should().Be(expectedHeader, "header must match IRIS template byte-for-byte");
    }

    [Fact]
    public void GenerateIrisCsv_ColumnCountMatchesHeader()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Contractor One", "111111111", false, 10000m),
            CreateTest1099NecData("Contractor Two", "222222222", true, 20000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert
        var headerColCount = lines[0].Split(',').Length;
        headerColCount.Should().Be(40, "IRIS CSV should have 40 columns");

        // Each data row should have same column count (accounting for CSV escaping)
        foreach (var dataLine in lines.Skip(1))
        {
            var cols = ParseCsvLine(dataLine);
            cols.Count.Should().Be(headerColCount, $"data row must have {headerColCount} columns");
        }
    }

    [Fact]
    public void GenerateIrisCsv_DecimalFormatUsesInvariantCulture()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Test Contractor", "123456789", false, 1234.56m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
        var dataLine = lines[1];

        // Assert - Box 1 amount should use period (not comma) as decimal separator
        dataLine.Should().Contain("1234.56", "decimal amounts must use period separator (InvariantCulture)");
        dataLine.Should().NotContain("1234,56", "must not use comma as decimal separator");
    }

    [Fact]
    public void GenerateIrisCsv_TinIs9DigitsNoFormatting()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Test Contractor", "123-45-6789", false, 10000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
        var cols = ParseCsvLine(lines[1]);

        // Assert
        var payeeTin = cols[0];
        payeeTin.Should().Be("123456789", "TIN must be exactly 9 digits with no dashes");
        payeeTin.Should().MatchRegex("^[0-9]{9}$", "TIN must be numeric only");
    }

    [Fact]
    public void GenerateIrisCsv_TinTypeCorrect()
    {
        // Arrange
        var formListWithEin = new List<Form1099NecData>
        {
            CreateTest1099NecData("Company LLC", "987654321", isEin: true, 10000m)
        };

        var formListWithSsn = new List<Form1099NecData>
        {
            CreateTest1099NecData("John Doe", "123456789", isEin: false, 10000m)
        };

        // Act
        var resultEin = IrisCsvService.GenerateIrisCsv(formListWithEin, 2025);
        var resultSsn = IrisCsvService.GenerateIrisCsv(formListWithSsn, 2025);

        var colsEin = ParseCsvLine(resultEin.Split('\n')[1]);
        var colsSsn = ParseCsvLine(resultSsn.Split('\n')[1]);

        // Assert
        colsEin[1].Should().Be("1", "EIN should have TIN Type = 1");
        colsSsn[1].Should().Be("2", "SSN should have TIN Type = 2");
    }

    [Fact]
    public void GenerateIrisCsv_BooleanIndicatorsAre0Or1()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Test Contractor", "123456789", false, 10000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
        var cols = ParseCsvLine(lines[1]);

        // Assert
        // Corrected Return Indicator (column 27)
        cols[27].Should().Match(s => s == "0" || s == "1", "Corrected Return Indicator must be 0 or 1");

        // Last Filed Return Indicator (column 28)
        cols[28].Should().Match(s => s == "0" || s == "1", "Last Filed Return Indicator must be 0 or 1");

        // Direct Sales Indicator (column 29)
        cols[29].Should().Match(s => s == "0" || s == "1", "Direct Sales Indicator must be 0 or 1");

        // FATCA Filing Requirement (column 30)
        cols[30].Should().Match(s => s == "0" || s == "1", "FATCA Filing Requirement must be 0 or 1");
    }

    [Fact]
    public void GenerateIrisCsv_EmbeddedLineBreaksQuoted()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Test\nContractor", "123456789", false, 10000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);

        // Assert - field with line break should be quoted per CSV RFC
        result.Should().Contain("\"Test\nContractor\"", "field with embedded newline should be quoted");
    }

    [Fact]
    public void GenerateIrisCsv_MaxRecordsLimit_ThrowsWhenExceeded()
    {
        // Arrange - create 101 records (over the 100 limit)
        var formList = Enumerable.Range(1, 101)
            .Select(i => CreateTest1099NecData($"Contractor {i}", $"{i:D9}", false, 10000m))
            .ToList();

        // Act & Assert
        var act = () => IrisCsvService.GenerateIrisCsv(formList, 2025);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*maximum of 100 records*");
    }

    [Fact]
    public void GenerateIrisCsv_EmptyList_ThrowsException()
    {
        // Arrange
        var emptyList = new List<Form1099NecData>();

        // Act & Assert
        var act = () => IrisCsvService.GenerateIrisCsv(emptyList, 2025);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one 1099-NEC is required*");
    }

    [Fact]
    public void GenerateIrisCsv_CsvEscapingWorks()
    {
        // Arrange - name with comma
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Smith, Inc.", "123456789", true, 10000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
        var dataLine = lines[1];

        // Assert - field with comma should be quoted
        dataLine.Should().Contain("\"Smith, Inc.\"", "field with comma should be quoted");
    }

    [Fact]
    public void GenerateIrisCsv_InvalidTin_ThrowsException()
    {
        // Arrange - TIN with only 8 digits
        var formList = new List<Form1099NecData>
        {
            new Form1099NecData
            {
                PayerName = "Test Company",
                PayerAddress = "123 Main St",
                PayerCity = "Columbus",
                PayerState = "OH",
                PayerZip = "43215",
                PayerTin = "987654321",
                PayerPhone = "6145551234",
                RecipientName = "Test Contractor",
                RecipientAddress = "456 Oak Ave",
                RecipientCity = "Columbus",
                RecipientState = "OH",
                RecipientZip = "43215",
                RecipientTin = "12345678", // Only 8 digits - invalid
                RecipientTinIsEin = false,
                Box1_NonemployeeCompensation = 10000m,
                Box4_FederalTaxWithheld = 0m,
                StateCode = "OH",
                StateName = "Ohio",
                StatePayerNo = "12345678",
                StateIncome = 10000m,
                StateTaxWithheld = 0m,
                TaxYear = 2025,
                AccountNumber = "1"
            }
        };

        // Act & Assert
        var act = () => IrisCsvService.GenerateIrisCsv(formList, 2025);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*9 digits*");
    }

    [Fact]
    public void GenerateIrisCsv_TaxYearFormatted()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Test Contractor", "123456789", false, 10000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
        var cols = ParseCsvLine(lines[1]);

        // Assert
        cols[26].Should().Be("2025", "tax year should be 4-digit year");
    }

    [Fact]
    public void GenerateIrisCsv_MultipleRecords_AllValid()
    {
        // Arrange
        var formList = new List<Form1099NecData>
        {
            CreateTest1099NecData("Contractor One", "111111111", false, 5000m),
            CreateTest1099NecData("Contractor Two", "222222222", true, 15000m),
            CreateTest1099NecData("Contractor Three", "333333333", false, 25000m)
        };

        // Act
        var result = IrisCsvService.GenerateIrisCsv(formList, 2025);
        var lines = result.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Length.Should().Be(4, "should have header + 3 data rows");

        // Verify each data row has correct structure
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            cols.Count.Should().Be(40, $"row {i} should have 40 columns");
        }
    }

    private static Form1099NecData CreateTest1099NecData(
        string recipientName,
        string recipientTin,
        bool isEin,
        decimal compensation)
    {
        return new Form1099NecData
        {
            PayerName = "Test Company Inc",
            PayerAddress = "123 Main Street",
            PayerCity = "Columbus",
            PayerState = "OH",
            PayerZip = "43215",
            PayerTin = "987654321",
            PayerPhone = "6145551234",
            RecipientName = recipientName,
            RecipientAddress = "456 Oak Avenue",
            RecipientCity = "Columbus",
            RecipientState = "OH",
            RecipientZip = "43215",
            RecipientTin = recipientTin,
            RecipientTinIsEin = isEin,
            Box1_NonemployeeCompensation = compensation,
            Box4_FederalTaxWithheld = 0m,
            StateCode = "OH",
            StateName = "Ohio",
            StatePayerNo = "12345678",
            StateIncome = compensation,
            StateTaxWithheld = 0m,
            TaxYear = 2025,
            AccountNumber = "1"
        };
    }

    /// <summary>
    /// Simple CSV parser that handles quoted fields.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var field = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                // Check for escaped quote ("")
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }
}
