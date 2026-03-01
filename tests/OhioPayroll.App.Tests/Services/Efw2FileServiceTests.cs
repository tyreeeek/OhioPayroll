using OhioPayroll.App.Documents;
using OhioPayroll.App.Services;
using Xunit;

namespace OhioPayroll.App.Tests.Services;

public class Efw2FileServiceTests
{
    private const int RecordLength = 512;

    [Fact]
    public void GenerateEfw2_AllRecordsExactly512Characters()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateSampleW2("111223333", "John", "Doe"),
            CreateSampleW2("444556666", "Jane", "Smith")
        };

        // Act
        var efw2 = Efw2FileService.GenerateEfw2(
            submitterEin: "987654321",
            submitterName: "Test Submitter Inc",
            submitterAddress: "123 Main St",
            submitterCity: "Columbus",
            submitterState: "OH",
            submitterZip: "43215",
            contactName: "John Contact",
            contactPhone: "6145551234",
            contactEmail: "contact@test.com",
            w2DataList: w2List,
            taxYear: 2025
        );

        // Assert
        var lines = efw2.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.All(lines, line =>
        {
            Assert.Equal(RecordLength, line.Length);
        });
    }

    [Fact]
    public void GenerateEfw2_CorrectRecordSequence()
    {
        // Arrange
        var w2List = new List<W2Data> { CreateSampleW2("111223333", "John", "Doe") };

        // Act
        var efw2 = Efw2FileService.GenerateEfw2(
            "987654321", "Submitter", "123 St", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = efw2.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        Assert.StartsWith("RA", lines[0]); // Submitter
        Assert.StartsWith("RE", lines[1]); // Employer
        Assert.StartsWith("RW", lines[2]); // Employee wage
        Assert.StartsWith("RS", lines[3]); // State wage
        Assert.StartsWith("RT", lines[4]); // Total
        Assert.StartsWith("RU", lines[5]); // State total
        Assert.StartsWith("RF", lines[6]); // Final
    }

    [Fact]
    public void GenerateEfw2_MoneyFieldsScaledToCents()
    {
        // Arrange
        var w2 = CreateSampleW2("111223333", "John", "Doe");
        w2.Box1WagesTips = 52000.50m; // Should be 5200050 cents

        // Act
        var efw2 = Efw2FileService.GenerateEfw2(
            "987654321", "Submitter", "123 St", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", new List<W2Data> { w2 }, 2025);

        var lines = efw2.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var rwRecord = lines.First(l => l.StartsWith("RW"));

        // Assert - Box 1 is at positions 144-154 (11 chars, zero-filled) per SSA EFW2 spec
        var box1Field = rwRecord.Substring(143, 11);
        Assert.Equal("00005200050", box1Field);
    }

    [Fact]
    public void GenerateEfw2_TinsNormalizedTo9Digits()
    {
        // Arrange
        var w2 = CreateSampleW2("111-22-3333", "John", "Doe");
        w2.EmployerEin = "98-7654321";

        // Act
        var efw2 = Efw2FileService.GenerateEfw2(
            "98-7654321", "Submitter", "123 St", "Columbus", "OH", "43215",
            "Contact", "614-555-1234", "test@test.com", new List<W2Data> { w2 }, 2025);

        var lines = efw2.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var raRecord = lines[0];
        var rwRecord = lines.First(l => l.StartsWith("RW"));

        // Assert - RA EIN at positions 3-11
        Assert.Equal("987654321", raRecord.Substring(2, 9));

        // RW SSN at positions 3-11
        Assert.Equal("111223333", rwRecord.Substring(2, 9));
    }

    [Fact]
    public void GenerateEfw2_TotalsReconcileWithEmployeeRecords()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateSampleW2("111223333", "John", "Doe", wages: 50000m, fedTax: 7500m),
            CreateSampleW2("444556666", "Jane", "Smith", wages: 60000m, fedTax: 9000m)
        };

        // Act
        var efw2 = Efw2FileService.GenerateEfw2(
            "987654321", "Submitter", "123 St", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = efw2.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var rtRecord = lines.First(l => l.StartsWith("RT"));

        // Assert - RT record count at positions 3-9 (7 chars)
        var rwCount = rtRecord.Substring(2, 7);
        Assert.Equal("0000002", rwCount);

        // RT total Box 1 at positions 10-24 (15 chars, cents)
        var totalBox1 = rtRecord.Substring(9, 15);
        Assert.Equal("000000011000000", totalBox1); // 110000.00 = 11000000 cents

        // RT total Box 2 at positions 25-39 (15 chars, cents)
        var totalBox2 = rtRecord.Substring(24, 15);
        Assert.Equal("000000001650000", totalBox2); // 16500.00 = 1650000 cents
    }

    [Fact]
    public void GenerateEfw2_AlphaFieldsUppercaseAndLeftJustified()
    {
        // Arrange
        var w2 = CreateSampleW2("111223333", "john", "doe");
        w2.EmployeeCity = "columbus";

        // Act
        var efw2 = Efw2FileService.GenerateEfw2(
            "987654321", "test submitter", "123 St", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", new List<W2Data> { w2 }, 2025);

        var lines = efw2.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var raRecord = lines[0];
        var rwRecord = lines.First(l => l.StartsWith("RW"));

        // Assert - RA company name at positions 26-82 (57 chars, uppercase)
        var companyName = raRecord.Substring(25, 57).TrimEnd();
        Assert.Equal("TEST SUBMITTER", companyName);

        // RW first name at positions 12-26 (15 chars, uppercase, left-justified)
        var firstName = rwRecord.Substring(11, 15).TrimEnd();
        Assert.Equal("JOHN", firstName);

        // RW last name at positions 42-61 (20 chars, uppercase, left-justified)
        var lastName = rwRecord.Substring(41, 20).TrimEnd();
        Assert.Equal("DOE", lastName);
    }

    [Fact]
    public void GenerateEfw2_ThrowsOnNegativeAmounts()
    {
        // Arrange
        var w2 = CreateSampleW2("111223333", "John", "Doe");
        w2.Box1WagesTips = -1000m;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            Efw2FileService.GenerateEfw2(
                "987654321", "Submitter", "123 St", "Columbus", "OH", "43215",
                "Contact", "6145551234", "test@test.com", new List<W2Data> { w2 }, 2025));

        Assert.Contains("Negative amount not allowed", ex.Message);
    }

    [Fact]
    public void GenerateEfw2_ThrowsOnInvalidTinLength()
    {
        // Arrange
        var w2 = CreateSampleW2("12345", "John", "Doe"); // Only 5 digits

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            Efw2FileService.GenerateEfw2(
                "987654321", "Submitter", "123 St", "Columbus", "OH", "43215",
                "Contact", "6145551234", "test@test.com", new List<W2Data> { w2 }, 2025));

        Assert.Contains("9 digits", ex.Message);
    }

    [Fact]
    public void GenerateEfw2_OhioStateRecordsIncluded()
    {
        // Arrange
        var w2 = CreateSampleW2("111223333", "John", "Doe");
        w2.Box16StateWages = 50000m;
        w2.Box17StateTax = 1500m;
        w2.StateWithholdingId = "12345678";

        // Act
        var efw2 = Efw2FileService.GenerateEfw2(
            "987654321", "Submitter", "123 St", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", new List<W2Data> { w2 }, 2025);

        var lines = efw2.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var rsRecord = lines.First(l => l.StartsWith("RS"));
        var ruRecord = lines.First(l => l.StartsWith("RU"));

        // Assert - RS state code at positions 3-4 (Ohio = 39)
        Assert.Equal("39", rsRecord.Substring(2, 2));

        // RU state code at positions 125-126
        Assert.Equal("39", ruRecord.Substring(124, 2));
    }

    private static W2Data CreateSampleW2(string ssn, string firstName, string lastName,
        decimal wages = 50000m, decimal fedTax = 7500m)
    {
        return new W2Data
        {
            EmployeeSsn = ssn,
            EmployeeFirstName = firstName,
            EmployeeLastName = lastName,
            EmployeeAddress = "456 Oak St",
            EmployeeCity = "Columbus",
            EmployeeState = "OH",
            EmployeeZip = "43215",
            EmployerEin = "987654321",
            EmployerName = "Test Employer Inc",
            EmployerAddress = "789 Elm St",
            EmployerCity = "Columbus",
            EmployerState = "OH",
            EmployerZip = "43215",
            StateWithholdingId = "87654321",
            Box1WagesTips = wages,
            Box2FederalTaxWithheld = fedTax,
            Box3SocialSecurityWages = wages,
            Box4SocialSecurityTax = wages * 0.062m,
            Box5MedicareWages = wages,
            Box6MedicareTax = wages * 0.0145m,
            Box16StateWages = wages,
            Box17StateTax = wages * 0.03m,
            TaxYear = 2025
        };
    }
}
