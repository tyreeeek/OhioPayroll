using OhioPayroll.App.Documents;
using OhioPayroll.App.Services;
using Xunit;

namespace OhioPayroll.App.Tests.Services;

public class IrisCsvServiceTests
{
    private const string ExpectedHeader =
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

    [Fact]
    public void GenerateIrisCsv_HeaderMatchesIrsTemplateExactly()
    {
        // Arrange
        var data = new List<Form1099NecData> { CreateSample1099("111223333", "John Doe") };

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        Assert.Equal(ExpectedHeader, lines[0]);
    }

    [Fact]
    public void GenerateIrisCsv_CorrectColumnCount()
    {
        // Arrange
        var data = new List<Form1099NecData> { CreateSample1099("111223333", "John Doe") };

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Use a robust CSV parser that respects quoted fields
        var headerColumns = ParseCsvLine(lines[0]).Count;
        var dataColumns = ParseCsvLine(lines[1]).Count;

        Assert.Equal(40, headerColumns); // IRS IRIS template has 40 columns
        Assert.Equal(40, dataColumns);
    }

    /// <summary>
    /// Simple CSV line parser that handles quoted fields with embedded commas.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        fields.Add(currentField.ToString());
        return fields;
    }

    [Fact]
    public void GenerateIrisCsv_TinFormattedAs9Digits()
    {
        // Arrange
        var data = new List<Form1099NecData>
        {
            CreateSample1099("111-22-3333", "John Doe", payerTin: "98-7654321")
        };

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var dataRow = lines[1];
        var fields = dataRow.Split(',');

        Assert.Equal("111223333", fields[0]); // Payee TIN - 9 digits, no dashes
        Assert.Equal("987654321", fields[16]); // Payer TIN - 9 digits, no dashes
    }

    [Fact]
    public void GenerateIrisCsv_TinTypeCorrect()
    {
        // Arrange
        var ssnData = CreateSample1099("111223333", "John Doe");
        ssnData.RecipientTinIsEin = false;

        var einData = CreateSample1099("987654321", "ABC Corp");
        einData.RecipientTinIsEin = true;

        // Act
        var csv1 = IrisCsvService.GenerateIrisCsv(new List<Form1099NecData> { ssnData }, 2025);
        var csv2 = IrisCsvService.GenerateIrisCsv(new List<Form1099NecData> { einData }, 2025);

        // Assert
        var fields1 = csv1.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(',');
        var fields2 = csv2.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(',');

        Assert.Equal("2", fields1[1]); // SSN = 2
        Assert.Equal("1", fields2[1]); // EIN = 1
    }

    [Fact]
    public void GenerateIrisCsv_AmountsFormattedWithTwoDecimals()
    {
        // Arrange
        var data = new List<Form1099NecData>
        {
            CreateSample1099("111223333", "John Doe", compensation: 15250.50m, fedTax: 1525.05m)
        };

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var fields = lines[1].Split(',');

        Assert.Equal("15250.50", fields[13]); // Box 1 - F2 format
        Assert.Equal("1525.05", fields[15]); // Box 4 - F2 format
    }

    [Fact]
    public void GenerateIrisCsv_BooleanFlagsAs0Or1()
    {
        // Arrange
        var data = new List<Form1099NecData> { CreateSample1099("111223333", "John Doe") };

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var fields = lines[1].Split(',');

        Assert.Equal("0", fields[9]); // Payee Foreign Country Indicator
        Assert.Equal("0", fields[27]); // Corrected Return Indicator
        Assert.Equal("0", fields[28]); // Last Filed Return Indicator
        Assert.Equal("0", fields[29]); // Direct Sales Indicator
        Assert.Equal("0", fields[30]); // FATCA Filing Requirement
    }

    [Fact]
    public void GenerateIrisCsv_CsvQuotingCorrect()
    {
        // Arrange
        var data = new List<Form1099NecData>
        {
            CreateSample1099("111223333", "Doe, John")
        };
        data[0].RecipientAddress = "123 Main St, Apt 4";

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var dataRow = lines[1];

        // Fields with commas should be quoted
        Assert.Contains("\"Doe, John\"", dataRow);
        Assert.Contains("\"123 Main St, Apt 4\"", dataRow);
    }

    [Fact]
    public void GenerateIrisCsv_Max100RecordsEnforced()
    {
        // Arrange
        var data = new List<Form1099NecData>();
        for (int i = 0; i < 101; i++)
        {
            data.Add(CreateSample1099($"{i:D9}", $"Contractor {i}"));
        }

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            IrisCsvService.GenerateIrisCsv(data, 2025));

        Assert.Contains("maximum of 100 records", ex.Message);
    }

    [Fact]
    public void GenerateIrisCsv_ThrowsOnInvalidTinLength()
    {
        // Arrange
        var data = new List<Form1099NecData>
        {
            CreateSample1099("12345", "John Doe") // Only 5 digits
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            IrisCsvService.GenerateIrisCsv(data, 2025));

        Assert.Contains("9 digits", ex.Message);
    }

    [Fact]
    public void GenerateIrisCsv_StateFieldsPopulated()
    {
        // Arrange
        var data = new List<Form1099NecData>
        {
            CreateSample1099("111223333", "John Doe")
        };
        data[0].StateTaxWithheld = 500m;
        data[0].StateCode = "OH";
        data[0].StatePayerNo = "12345678";
        data[0].StateIncome = 15000m;

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var fields = lines[1].Split(',');

        Assert.Equal("500.00", fields[32]); // State Income Tax Withheld 1
        Assert.Equal("OH", fields[33]); // State 1
        Assert.Equal("12345678", fields[34]); // Payer State Number 1
        Assert.Equal("15000.00", fields[35]); // State Income 1
    }

    [Fact]
    public void GenerateIrisCsv_InvariantCultureForAmounts()
    {
        // Arrange - Test that decimal separator is always period, not comma
        var data = new List<Form1099NecData>
        {
            CreateSample1099("111223333", "John Doe", compensation: 1234.56m)
        };

        // Act
        var csv = IrisCsvService.GenerateIrisCsv(data, 2025);

        // Assert
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var fields = lines[1].Split(',');

        Assert.Equal("1234.56", fields[13]); // Must use period, not comma
        Assert.DoesNotContain("1234,56", csv); // Verify no European format
    }

    private static Form1099NecData CreateSample1099(string recipientTin, string recipientName,
        string payerTin = "987654321", decimal compensation = 15000m, decimal fedTax = 0m)
    {
        return new Form1099NecData
        {
            TaxYear = 2025,
            RecipientTin = recipientTin,
            RecipientTinIsEin = false,
            RecipientName = recipientName,
            RecipientAddress = "123 Main St",
            RecipientCity = "Columbus",
            RecipientState = "OH",
            RecipientZip = "43215",
            PayerTin = payerTin,
            PayerName = "Test Company Inc",
            PayerAddress = "456 Oak Ave",
            PayerCity = "Columbus",
            PayerState = "OH",
            PayerZip = "43215",
            PayerPhone = "6145551234",
            Box1_NonemployeeCompensation = compensation,
            Box4_FederalTaxWithheld = fedTax,
            StateCode = "OH",
            StatePayerNo = "87654321",
            StateIncome = compensation,
            StateTaxWithheld = 0m,
            AccountNumber = ""
        };
    }
}
