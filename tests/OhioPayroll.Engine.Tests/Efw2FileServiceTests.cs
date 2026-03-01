using FluentAssertions;
using OhioPayroll.App.Documents;
using OhioPayroll.App.Services;

namespace OhioPayroll.Engine.Tests;

public class Efw2FileServiceTests
{
    [Fact]
    public void GenerateEfw2_WithSingleEmployee_ReturnsValidFile()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data(
                empSsn: "123456789",
                empFirst: "John",
                empLast: "Doe",
                box1: 50000m,
                box2: 5000m,
                box3: 50000m,
                box4: 3100m,
                box5: 50000m,
                box6: 725m,
                box16: 50000m,
                box17: 1500m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            submitterEin: "987654321",
            submitterName: "Test Company",
            submitterAddress: "123 Main St",
            submitterCity: "Columbus",
            submitterState: "OH",
            submitterZip: "43215",
            contactName: "Jane Smith",
            contactPhone: "6145551234",
            contactEmail: "contact@test.com",
            w2DataList: w2List,
            taxYear: 2025);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None);

        // Every record must be exactly 512 characters
        foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
        {
            line.Length.Should().Be(512, $"every EFW2 record must be 512 bytes, got line: {line[..Math.Min(50, line.Length)]}...");
        }

        // File must start with RA and end with RF
        lines[0].Should().StartWith("RA");
        lines.Where(l => !string.IsNullOrEmpty(l)).Last().Should().StartWith("RF");
    }

    [Fact]
    public void GenerateEfw2_ValidatesRecordSequence()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("111111111", "Alice", "Anderson", 45000m, 4500m, 45000m, 2790m, 45000m, 652.50m, 45000m, 1350m),
            CreateTestW2Data("222222222", "Bob", "Brown", 55000m, 5500m, 55000m, 3410m, 55000m, 797.50m, 55000m, 1650m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert canonical sequence: RA → RE → (RW₁ → RS₁?) → (RW₂ → RS₂?) → ... → RT → RU? → RF
        lines[0].Should().StartWith("RA", "first record must be Submitter");
        lines[1].Should().StartWith("RE", "second record must be Employer");

        var rwCount = lines.Count(l => l.StartsWith("RW"));
        var rtIndex = lines.FindIndex(l => l.StartsWith("RT"));
        var rfIndex = lines.FindIndex(l => l.StartsWith("RF"));

        rtIndex.Should().BeGreaterThan(1, "RT must appear after RE");
        rfIndex.Should().Be(lines.Count - 1, "RF must be the last record");

        // RW count should match the number of W2s
        rwCount.Should().Be(2, "should have 2 RW records for 2 employees");
    }

    [Fact]
    public void GenerateEfw2_RwCountMatchesRtField()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("111111111", "Alice", "Anderson", 40000m, 4000m, 40000m, 2480m, 40000m, 580m, 40000m, 1200m),
            CreateTestW2Data("222222222", "Bob", "Brown", 50000m, 5000m, 50000m, 3100m, 50000m, 725m, 50000m, 1500m),
            CreateTestW2Data("333333333", "Carol", "Clark", 60000m, 6000m, 60000m, 3720m, 60000m, 870m, 60000m, 1800m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert
        var rtLine = lines.First(l => l.StartsWith("RT"));
        var rwCountField = rtLine.Substring(2, 7); // Positions 3-9: number of RW records
        var actualRwCount = lines.Count(l => l.StartsWith("RW"));

        int.Parse(rwCountField).Should().Be(actualRwCount, "RT record count must match actual RW records");
        int.Parse(rwCountField).Should().Be(3, "should have 3 RW records");
    }

    [Fact]
    public void GenerateEfw2_RfCountMatchesTotalRw()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("111111111", "Test", "User", 40000m, 4000m, 40000m, 2480m, 40000m, 580m, 40000m, 1200m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert
        var rfLine = lines.First(l => l.StartsWith("RF"));
        var rfCountField = rfLine.Substring(3, 7); // Positions 4-10: total RW records
        var actualRwCount = lines.Count(l => l.StartsWith("RW"));

        int.Parse(rfCountField).Should().Be(actualRwCount, "RF record count must match total RW records");
    }

    [Fact]
    public void GenerateEfw2_NumericFieldsContainOnlyDigits()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("123456789", "John", "Doe", 50000m, 5000m, 50000m, 3100m, 50000m, 725m, 50000m, 1500m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert - check RW record money fields (positions 144-154 for Box 1, for example)
        var rwLine = lines.First(l => l.StartsWith("RW"));

        // Box 1 wages: positions 144-154 (11 chars)
        var box1Field = rwLine.Substring(143, 11);
        box1Field.Should().MatchRegex("^[0-9]{11}$", "Box 1 wages must be 11 digits");

        // Box 2 federal tax: positions 155-165 (11 chars)
        var box2Field = rwLine.Substring(154, 11);
        box2Field.Should().MatchRegex("^[0-9]{11}$", "Box 2 federal tax must be 11 digits");

        // SSN: positions 3-11 (9 chars)
        var ssnField = rwLine.Substring(2, 9);
        ssnField.Should().MatchRegex("^[0-9]{9}$", "SSN must be exactly 9 digits");
    }

    [Fact]
    public void GenerateEfw2_AlphaFieldsAreUppercase()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("123456789", "john", "doe", 50000m, 5000m, 50000m, 3100m, 50000m, 725m, 50000m, 1500m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "test company inc", "123 main street", "columbus", "OH", "43215",
            "jane smith", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert
        var rwLine = lines.First(l => l.StartsWith("RW"));

        // Employee first name: positions 12-26 (15 chars)
        var firstNameField = rwLine.Substring(11, 15).Trim();
        firstNameField.Should().Be("JOHN", "first name should be uppercase");

        // Employee last name: positions 42-61 (20 chars)
        var lastNameField = rwLine.Substring(41, 20).Trim();
        lastNameField.Should().Be("DOE", "last name should be uppercase");

        // RA submitter name should be uppercase too
        var raLine = lines.First(l => l.StartsWith("RA"));
        var submitterName = raLine.Substring(24, 57).Trim();
        submitterName.Should().Be("TEST COMPANY INC", "submitter name should be uppercase");
    }

    [Fact]
    public void GenerateEfw2_TinExactly9Digits_ThrowsOnInvalid()
    {
        // Arrange - invalid SSN (8 digits)
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("12345678", "John", "Doe", 50000m, 5000m, 50000m, 3100m, 50000m, 725m, 50000m, 1500m)
        };

        // Act & Assert
        var act = () => Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*9 digits*");
    }

    [Fact]
    public void GenerateEfw2_MoneyFieldsZeroPadded()
    {
        // Arrange - small amounts to test zero-padding
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("123456789", "John", "Doe", 1234.56m, 123.45m, 1234.56m, 76.54m, 1234.56m, 17.90m, 1234.56m, 37.04m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert
        var rwLine = lines.First(l => l.StartsWith("RW"));

        // Box 1: $1,234.56 = 123456 cents → "00000123456" (11 chars)
        var box1Field = rwLine.Substring(143, 11);
        box1Field.Should().Be("00000123456", "Box 1 should be zero-padded to 11 digits");

        // Box 2: $123.45 = 12345 cents → "00000012345" (11 chars)
        var box2Field = rwLine.Substring(154, 11);
        box2Field.Should().Be("00000012345", "Box 2 should be zero-padded to 11 digits");
    }

    [Fact]
    public void GenerateEfw2_EmptyList_ThrowsException()
    {
        // Arrange
        var emptyList = new List<W2Data>();

        // Act & Assert
        var act = () => Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", emptyList, 2025);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one W-2 is required*");
    }

    [Fact]
    public void GenerateEfw2_WithStateWages_IncludesRsRecords()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("123456789", "John", "Doe", 50000m, 5000m, 50000m, 3100m, 50000m, 725m, 50000m, 1500m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert - RS record should be present since state wages > 0
        var rsCount = lines.Count(l => l.StartsWith("RS"));
        rsCount.Should().Be(1, "should have 1 RS record when state wages > 0");

        // RS should immediately follow RW
        var rwIndex = lines.FindIndex(l => l.StartsWith("RW"));
        var rsIndex = lines.FindIndex(l => l.StartsWith("RS"));
        rsIndex.Should().Be(rwIndex + 1, "RS should immediately follow its RW");
    }

    [Fact]
    public void GenerateEfw2_WithoutStateWages_ExcludesRsRecords()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("123456789", "John", "Doe", 50000m, 5000m, 50000m, 3100m, 50000m, 725m, 0m, 0m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert - no RS records when state wages and state tax are both 0
        var rsCount = lines.Count(l => l.StartsWith("RS"));
        rsCount.Should().Be(0, "should have no RS records when state wages = 0 and state tax = 0");
    }

    [Fact]
    public void GenerateEfw2_TotalsMatchSumOfW2Data()
    {
        // Arrange
        var w2List = new List<W2Data>
        {
            CreateTestW2Data("111111111", "Alice", "Anderson", 40000m, 4000m, 40000m, 2480m, 40000m, 580m, 40000m, 1200m),
            CreateTestW2Data("222222222", "Bob", "Brown", 60000m, 6000m, 60000m, 3720m, 60000m, 870m, 60000m, 1800m)
        };

        // Act
        var result = Efw2FileService.GenerateEfw2(
            "987654321", "Test Co", "123 Main", "Columbus", "OH", "43215",
            "Contact", "6145551234", "test@test.com", w2List, 2025);

        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Assert
        var rtLine = lines.First(l => l.StartsWith("RT"));

        // Total wages (Box 1): positions 10-24 (15 chars)
        var totalWagesField = rtLine.Substring(9, 15);
        long totalWagesCents = long.Parse(totalWagesField);
        totalWagesCents.Should().Be(10000000, "total wages should be $100,000 = 10,000,000 cents");

        // Total federal tax (Box 2): positions 25-39 (15 chars)
        var totalFedTaxField = rtLine.Substring(24, 15);
        long totalFedTaxCents = long.Parse(totalFedTaxField);
        totalFedTaxCents.Should().Be(1000000, "total federal tax should be $10,000 = 1,000,000 cents");
    }

    private static W2Data CreateTestW2Data(
        string empSsn,
        string empFirst,
        string empLast,
        decimal box1,
        decimal box2,
        decimal box3,
        decimal box4,
        decimal box5,
        decimal box6,
        decimal box16,
        decimal box17)
    {
        return new W2Data
        {
            EmployerEin = "987654321",
            EmployerName = "Test Company Inc",
            EmployerAddress = "123 Main Street",
            EmployerCity = "Columbus",
            EmployerState = "OH",
            EmployerZip = "43215",
            StateWithholdingId = "12345678",
            EmployeeSsn = empSsn,
            EmployeeFirstName = empFirst,
            EmployeeLastName = empLast,
            EmployeeAddress = "456 Oak Ave",
            EmployeeCity = "Columbus",
            EmployeeState = "OH",
            EmployeeZip = "43215",
            Box1WagesTips = box1,
            Box2FederalTaxWithheld = box2,
            Box3SocialSecurityWages = box3,
            Box4SocialSecurityTax = box4,
            Box5MedicareWages = box5,
            Box6MedicareTax = box6,
            Box16StateWages = box16,
            Box17StateTax = box17,
            Box18LocalWages = 0m,
            Box19LocalTax = 0m,
            Box20LocalityName = "",
            TaxYear = 2025
        };
    }
}
