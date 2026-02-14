using FluentAssertions;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.Calculators;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Tests;

public class PayrollCalculationEngineTests
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

        var ohioBrackets = new List<TaxBracket>
        {
            new() { BracketStart = 0, BracketEnd = 26_050, Rate = 0.0000m, BaseAmount = 0 },
            new() { BracketStart = 26_050, BracketEnd = 100_000, Rate = 0.0275m, BaseAmount = 0 },
            new() { BracketStart = 100_000, BracketEnd = decimal.MaxValue, Rate = 0.03125m, BaseAmount = 2_033.63m },
        };

        return new PayrollCalculationEngine(
            new FederalTaxCalculator(singleBrackets, marriedBrackets),
            new OhioStateTaxCalculator(ohioBrackets));
    }

    private static Employee CreateTestEmployee(PayType payType = PayType.Hourly)
    {
        return new Employee
        {
            FirstName = "John",
            LastName = "Doe",
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

    [Fact]
    public void FullPaycheck_HourlyEmployee_AllFieldsPopulated()
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: 40,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            ytdSutaPrior: 0,
            schoolDistrictRate: 0.0175m,
            localTaxRate: 0.025m);

        result.GrossPay.Should().Be(1_000.00m);
        result.RegularPay.Should().Be(1_000.00m);
        result.OvertimePay.Should().Be(0m);

        // All tax fields should be populated
        result.EmployeeTaxes.FederalWithholding.Should().BeGreaterThanOrEqualTo(0m);
        result.EmployeeTaxes.OhioStateWithholding.Should().BeGreaterThanOrEqualTo(0m);
        result.EmployeeTaxes.SchoolDistrictTax.Should().Be(17.50m); // 1000 * 0.0175
        result.EmployeeTaxes.LocalMunicipalityTax.Should().Be(25.00m); // 1000 * 0.025
        result.EmployeeTaxes.SocialSecurityTax.Should().Be(62.00m); // 1000 * 0.062
        result.EmployeeTaxes.MedicareTax.Should().Be(14.50m); // 1000 * 0.0145

        // Employer taxes
        result.EmployerTaxes.SocialSecurity.Should().Be(62.00m);
        result.EmployerTaxes.Medicare.Should().Be(14.50m);
        result.EmployerTaxes.Futa.Should().Be(6.00m); // 1000 * 0.006
        result.EmployerTaxes.Suta.Should().Be(27.00m); // 1000 * 0.027

        // Net pay
        result.TotalDeductions.Should().Be(result.EmployeeTaxes.Total);
        result.NetPay.Should().Be(result.GrossPay - result.TotalDeductions);
        result.NetPay.Should().BeGreaterThan(0m);
        result.NetPay.Should().BeLessThan(result.GrossPay);
    }

    [Fact]
    public void SalariedEmployee_CorrectGrossPay()
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee(PayType.Salary);

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: 0,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 0,
            ytdSocialSecurityPrior: 0,
            ytdFutaPrior: 0,
            ytdSutaPrior: 0,
            schoolDistrictRate: 0,
            localTaxRate: 0);

        result.GrossPay.Should().Be(2_000.00m); // 52000 / 26
    }

    [Fact]
    public void Deterministic_IdenticalInputs_IdenticalOutput()
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        var result1 = engine.CalculatePaycheck(employee, 40, 5, PayFrequency.BiWeekly,
            10_000m, 10_000m, 5_000m, 5_000m, 0.0175m, 0.025m);
        var result2 = engine.CalculatePaycheck(employee, 40, 5, PayFrequency.BiWeekly,
            10_000m, 10_000m, 5_000m, 5_000m, 0.0175m, 0.025m);

        result1.Should().Be(result2);
    }

    [Fact]
    public void SSWageCapBoundary_CorrectPartialSS()
    {
        var engine = CreateEngine();
        var employee = CreateTestEmployee();

        var result = engine.CalculatePaycheck(
            employee,
            regularHours: 40,
            overtimeHours: 0,
            frequency: PayFrequency.BiWeekly,
            ytdGrossPrior: 175_500m,
            ytdSocialSecurityPrior: 175_500m,
            ytdFutaPrior: 10_000m,
            ytdSutaPrior: 10_000m,
            schoolDistrictRate: 0,
            localTaxRate: 0);

        // Gross = 1000. SS cap = 176,100. Prior = 175,500. Taxable = 600
        result.EmployeeTaxes.SocialSecurityTax.Should().Be(37.20m); // 600 * 0.062
        result.EmployeeTaxes.MedicareTax.Should().Be(14.50m); // Always full
    }
}

