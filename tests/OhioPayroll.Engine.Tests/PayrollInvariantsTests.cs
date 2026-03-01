using FluentAssertions;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.Calculators;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Tests;

/// <summary>
/// Property-based tests that verify mathematical invariants hold true across all payroll calculations.
/// These tests ensure fundamental correctness regardless of specific input values.
/// </summary>
public class PayrollInvariantsTests
{
    private static PayrollCalculationEngine CreateEngine()
    {
        var singleBrackets = new List<TaxBracket>
        {
            new() { BracketStart = 0, BracketEnd = 6_000, Rate = 0.00m, BaseAmount = 0 },
            new() { BracketStart = 6_000, BracketEnd = 17_600, Rate = 0.10m, BaseAmount = 0 },
            new() { BracketStart = 17_600, BracketEnd = 53_150, Rate = 0.12m, BaseAmount = 1_160 },
            new() { BracketStart = 53_150, BracketEnd = 106_525, Rate = 0.22m, BaseAmount = 5_426 },
            new() { BracketStart = 106_525, BracketEnd = 197_950, Rate = 0.24m, BaseAmount = 17_168.50m },
            new() { BracketStart = 197_950, BracketEnd = 249_725, Rate = 0.32m, BaseAmount = 39_110.50m },
            new() { BracketStart = 249_725, BracketEnd = 615_350, Rate = 0.35m, BaseAmount = 55_678.50m },
            new() { BracketStart = 615_350, BracketEnd = decimal.MaxValue, Rate = 0.37m, BaseAmount = 183_647.25m },
        };
        var marriedBrackets = new List<TaxBracket>
        {
            new() { BracketStart = 0, BracketEnd = 16_300, Rate = 0.00m, BaseAmount = 0 },
            new() { BracketStart = 16_300, BracketEnd = 39_500, Rate = 0.10m, BaseAmount = 0 },
            new() { BracketStart = 39_500, BracketEnd = 110_600, Rate = 0.12m, BaseAmount = 2_320 },
            new() { BracketStart = 110_600, BracketEnd = 217_350, Rate = 0.22m, BaseAmount = 10_852 },
            new() { BracketStart = 217_350, BracketEnd = 400_200, Rate = 0.24m, BaseAmount = 34_337 },
            new() { BracketStart = 400_200, BracketEnd = 503_750, Rate = 0.32m, BaseAmount = 78_221 },
            new() { BracketStart = 503_750, BracketEnd = 747_500, Rate = 0.35m, BaseAmount = 111_357 },
            new() { BracketStart = 747_500, BracketEnd = decimal.MaxValue, Rate = 0.37m, BaseAmount = 196_669.50m },
        };

        var hohBrackets = new List<TaxBracket>
        {
            new() { BracketStart = 0, BracketEnd = 10_800, Rate = 0.00m, BaseAmount = 0 },
            new() { BracketStart = 10_800, BracketEnd = 26_200, Rate = 0.10m, BaseAmount = 0 },
            new() { BracketStart = 26_200, BracketEnd = 66_150, Rate = 0.12m, BaseAmount = 1_540 },
            new() { BracketStart = 66_150, BracketEnd = 106_525, Rate = 0.22m, BaseAmount = 6_334 },
            new() { BracketStart = 106_525, BracketEnd = 197_950, Rate = 0.24m, BaseAmount = 15_216.50m },
            new() { BracketStart = 197_950, BracketEnd = 243_725, Rate = 0.32m, BaseAmount = 37_158.50m },
            new() { BracketStart = 243_725, BracketEnd = 609_350, Rate = 0.35m, BaseAmount = 51_806.50m },
            new() { BracketStart = 609_350, BracketEnd = decimal.MaxValue, Rate = 0.37m, BaseAmount = 179_776.25m },
        };

        var ohioBrackets = new List<TaxBracket>
        {
            new() { BracketStart = 0, BracketEnd = 26_050, Rate = 0.0000m, BaseAmount = 0 },
            new() { BracketStart = 26_050, BracketEnd = 100_000, Rate = 0.0275m, BaseAmount = 0 },
            new() { BracketStart = 100_000, BracketEnd = decimal.MaxValue, Rate = 0.03125m, BaseAmount = 2_033.63m },
        };

        return new PayrollCalculationEngine(
            new FederalTaxCalculator(singleBrackets, marriedBrackets, hohBrackets),
            new OhioStateTaxCalculator(ohioBrackets));
    }

    private static Employee CreateTestEmployee(PayType payType = PayType.Hourly)
    {
        return new Employee
        {
            FirstName = "Test",
            LastName = "Employee",
            PayType = payType,
            HourlyRate = 25.00m,
            AnnualSalary = 52_000m,
            FederalFilingStatus = FilingStatus.Single,
            OhioFilingStatus = FilingStatus.Single,
            FederalAllowances = 0,
            OhioExemptions = 1,
            State = "OH"
        };
    }

    /// <summary>
    /// INVARIANT: Net Pay = Gross Pay - Total Deductions
    /// This must ALWAYS be true, regardless of any inputs or calculation complexity.
    /// </summary>
    [Theory]
    [InlineData(500)]
    [InlineData(1_000)]
    [InlineData(2_500)]
    [InlineData(5_000)]
    [InlineData(10_000)]
    public void NetPay_AlwaysEqualsGrossMinusDeductions(decimal grossPay)
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        // Calculate paycheck with varying gross amounts
        decimal regularHours = grossPay / employee.HourlyRate;

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: regularHours,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            schoolDistrictRate: 0.0175m,
            localTaxRate: 0.025m);

        // Verify fundamental equation
        var calculatedNet = result.GrossPay - result.TotalDeductions;
        result.NetPay.Should().Be(calculatedNet);
    }

    /// <summary>
    /// INVARIANT: Total Deductions = Sum of All Individual Tax Components
    /// Every cent deducted must be accounted for.
    /// </summary>
    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 5)]
    [InlineData(80, 10)]
    public void TotalDeductions_AlwaysEqualsSumOfComponents(decimal regularHours, decimal overtimeHours)
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: regularHours,
            overtimeHours: overtimeHours,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            schoolDistrictRate: 0.0175m,
            localTaxRate: 0.025m);

        var sumOfComponents =
            result.EmployeeTaxes.FederalWithholding +
            result.EmployeeTaxes.OhioStateWithholding +
            result.EmployeeTaxes.SchoolDistrictTax +
            result.EmployeeTaxes.LocalMunicipalityTax +
            result.EmployeeTaxes.SocialSecurityTax +
            result.EmployeeTaxes.MedicareTax;

        result.TotalDeductions.Should().Be(sumOfComponents);
        result.EmployeeTaxes.Total.Should().Be(sumOfComponents);
    }

    /// <summary>
    /// INVARIANT: Net Pay must NEVER be negative
    /// Even with maximum deductions, net pay cannot go below zero.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void NetPay_NeverNegative(decimal grossPay)
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();
        decimal regularHours = grossPay / employee.HourlyRate;

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: regularHours,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            schoolDistrictRate: 0.0175m,
            localTaxRate: 0.025m);

        result.NetPay.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// INVARIANT: Total Deductions must NEVER exceed Gross Pay
    /// You cannot deduct more than the employee earned.
    /// </summary>
    [Theory]
    [InlineData(500)]
    [InlineData(1_000)]
    [InlineData(5_000)]
    public void TotalDeductions_NeverExceedGrossPay(decimal grossPay)
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();
        decimal regularHours = grossPay / employee.HourlyRate;

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: regularHours,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            schoolDistrictRate: 0.0175m,
            localTaxRate: 0.025m);

        result.TotalDeductions.Should().BeLessThanOrEqualTo(result.GrossPay);
    }

    /// <summary>
    /// INVARIANT: Calculations must be DETERMINISTIC
    /// Same inputs ALWAYS produce same outputs, no matter how many times calculated.
    /// </summary>
    [Fact]
    public void Calculations_AreDeterministic()
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        var results = new List<PaycheckCalculationResult>();

        // Run same calculation 10 times
        for (int i = 0; i < 10; i++)
        {
            var result = engine.CalculatePaycheck(
                employee,
                regularHours: 40,
                overtimeHours: 5,
                frequency: PayFrequency.BiWeekly,
                ytdGrossPrior: 10_000m,
                ytdSocialSecurityPrior: 10_000m,
                ytdFutaPrior: 5_000m,
                schoolDistrictRate: 0.0175m,
                localTaxRate: 0.025m);

            results.Add(result);
        }

        // All results must be IDENTICAL
        for (int i = 1; i < results.Count; i++)
        {
            results[i].Should().Be(results[0], $"iteration {i} should match iteration 0");
        }
    }

    /// <summary>
    /// INVARIANT: All tax amounts must round to exactly 2 decimal places
    /// Financial calculations must not have sub-penny precision.
    /// </summary>
    [Theory]
    [InlineData(1_234.56)]
    [InlineData(999.99)]
    [InlineData(2_500.01)]
    public void AllTaxAmounts_RoundedToTwoDecimals(decimal grossPay)
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();
        decimal regularHours = grossPay / employee.HourlyRate;

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: regularHours,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            schoolDistrictRate: 0.0175m,
            localTaxRate: 0.025m);

        // Verify all amounts have exactly 2 decimal places
        AssertTwoDecimalPlaces(result.GrossPay);
        AssertTwoDecimalPlaces(result.NetPay);
        AssertTwoDecimalPlaces(result.TotalDeductions);
        AssertTwoDecimalPlaces(result.EmployeeTaxes.FederalWithholding);
        AssertTwoDecimalPlaces(result.EmployeeTaxes.OhioStateWithholding);
        AssertTwoDecimalPlaces(result.EmployeeTaxes.SchoolDistrictTax);
        AssertTwoDecimalPlaces(result.EmployeeTaxes.LocalMunicipalityTax);
        AssertTwoDecimalPlaces(result.EmployeeTaxes.SocialSecurityTax);
        AssertTwoDecimalPlaces(result.EmployeeTaxes.MedicareTax);
    }

    /// <summary>
    /// INVARIANT: Overtime pay rate = Regular rate * 1.5
    /// Federal law requires time-and-a-half for overtime hours.
    /// </summary>
    [Theory]
    [InlineData(20.00)]
    [InlineData(25.00)]
    [InlineData(30.50)]
    public void OvertimePay_AlwaysTimesOnePointFive(decimal hourlyRate)
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();
        employee.HourlyRate = hourlyRate;

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: 40,
            overtimeHours: 10,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            schoolDistrictRate: 0,
            localTaxRate: 0);

        var expectedOvertimePay = 10 * hourlyRate * 1.5m;
        result.OvertimePay.Should().Be(expectedOvertimePay);
    }

    /// <summary>
    /// INVARIANT: Gross Pay = Regular Pay + Overtime Pay
    /// Total gross must be sum of all pay components.
    /// </summary>
    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 5)]
    [InlineData(35, 10)]
    public void GrossPay_AlwaysEqualsRegularPlusOvertime(decimal regularHours, decimal overtimeHours)
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: regularHours,
            overtimeHours: overtimeHours,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            schoolDistrictRate: 0,
            localTaxRate: 0);

        result.GrossPay.Should().Be(result.RegularPay + result.OvertimePay);
    }

    /// <summary>
    /// INVARIANT: Social Security tax stops at wage cap
    /// Once YTD earnings exceed SS wage cap, SS tax must be zero.
    /// </summary>
    [Fact]
    public void SocialSecurity_StopsAtWageCap()
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        // 2025 SS cap is $176,100
        var result = engine.CalculatePaycheck(
            employee,
            regularHours: 40,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 180_000m,  // Well over cap
            ytdSocialSecurityPrior: 180_000m,
            ytdFutaPrior: 10_000m,
            schoolDistrictRate: 0,
            localTaxRate: 0,
            taxYear: 2025);

        result.EmployeeTaxes.SocialSecurityTax.Should().Be(0);
        result.EmployerTaxes.SocialSecurity.Should().Be(0);
    }

    /// <summary>
    /// INVARIANT: Medicare tax NEVER stops
    /// Unlike SS, Medicare applies to all wages regardless of YTD.
    /// </summary>
    [Fact]
    public void Medicare_NeverStops()
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: 40,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 500_000m,  // Very high YTD
            ytdSocialSecurityPrior: 500_000m,
            ytdFutaPrior: 10_000m,
            schoolDistrictRate: 0,
            localTaxRate: 0);

        result.EmployeeTaxes.MedicareTax.Should().BeGreaterThan(0);
        result.EmployerTaxes.Medicare.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// INVARIANT: Higher filing status = Lower tax (for same income)
    /// Married filing should always result in lower federal tax than Single.
    /// </summary>
    [Theory]
    [InlineData(1_000)]
    [InlineData(2_000)]
    [InlineData(3_000)]
    public void MarriedFiling_LowerTax_ThanSingle(decimal grossPay)
    {
        var engine = CreateEngine();
        var employeeSingle = CreateTestEmployee();
        var employeeMarried = CreateTestEmployee();
        employeeMarried.FederalFilingStatus = FilingStatus.Married;

        decimal regularHours = grossPay / employeeSingle.HourlyRate;

        var resultSingle = engine.CalculatePaycheck(
            employeeSingle, regularHours, 0, PayFrequency.BiWeekly, 0, 0, 0, 0, 0);

        var resultMarried = engine.CalculatePaycheck(
            employeeMarried, regularHours, 0, PayFrequency.BiWeekly, 0, 0, 0, 0, 0);

        resultMarried.EmployeeTaxes.FederalWithholding
            .Should().BeLessThanOrEqualTo(resultSingle.EmployeeTaxes.FederalWithholding);
    }

    private static void AssertTwoDecimalPlaces(decimal amount)
    {
        var rounded = Math.Round(amount, 2);
        amount.Should().Be(rounded, $"{amount} should be rounded to 2 decimal places");
    }
}
