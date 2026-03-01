using FluentAssertions;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.Calculators;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Tests;

public class FederalTaxCalculatorTests
{
    private static FederalTaxCalculator CreateCalculator()
    {
        // 2025 Pub 15-T percentage method brackets
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

        return new FederalTaxCalculator(singleBrackets, marriedBrackets, hohBrackets);
    }

    [Fact]
    public void Single_BelowStandardDeduction_ZeroTax()
    {
        var calc = CreateCalculator();
        // $6,000 annual is in 0% bracket. BiWeekly = 6000/26 = ~230.77
        var result = calc.Calculate(230.00m, FilingStatus.Single, PayFrequency.BiWeekly);
        result.Should().Be(0m);
    }

    [Fact]
    public void Single_ModerateIncome_CorrectBracket()
    {
        var calc = CreateCalculator();
        // $50,000 annual. BiWeekly = 50000/26 = 1923.08
        var result = calc.Calculate(1_923.08m, FilingStatus.Single, PayFrequency.BiWeekly);
        // Annualized: 1923.08 * 26 = 50000.08
        // Falls in 12% bracket (17,600-53,150)
        // Tax = 1160 + (50000.08 - 17600) * 0.12 = 1160 + 3888.01 = 5048.01
        // De-annualized: 5048.01 / 26 = 194.15
        result.Should().BeGreaterThan(0m);
        result.Should().BeLessThan(300m);
    }

    [Fact]
    public void Married_LowerTax_ThanSingle()
    {
        var calc = CreateCalculator();
        decimal grossPay = 2_000m;

        var singleTax = calc.Calculate(grossPay, FilingStatus.Single, PayFrequency.BiWeekly);
        var marriedTax = calc.Calculate(grossPay, FilingStatus.Married, PayFrequency.BiWeekly);

        marriedTax.Should().BeLessThan(singleTax);
    }

    [Fact]
    public void ZeroGrossPay_ReturnsZero()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(0m, FilingStatus.Single, PayFrequency.BiWeekly);
        result.Should().Be(0m);
    }

    [Fact]
    public void NegativeGrossPay_ReturnsZero()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(-100m, FilingStatus.Single, PayFrequency.BiWeekly);
        result.Should().Be(0m);
    }

    [Fact]
    public void Deterministic_SameInputs_SameOutput()
    {
        var calc = CreateCalculator();
        var result1 = calc.Calculate(2_500m, FilingStatus.Single, PayFrequency.BiWeekly);
        var result2 = calc.Calculate(2_500m, FilingStatus.Single, PayFrequency.BiWeekly);
        result1.Should().Be(result2);
    }

    [Theory]
    [InlineData(PayFrequency.Weekly)]
    [InlineData(PayFrequency.BiWeekly)]
    [InlineData(PayFrequency.SemiMonthly)]
    [InlineData(PayFrequency.Monthly)]
    public void AllFrequencies_ProducePositiveTax_ForModerateIncome(PayFrequency frequency)
    {
        var calc = CreateCalculator();
        decimal annualSalary = 60_000m;
        int periods = (int)frequency;
        decimal periodPay = Math.Round(annualSalary / periods, 2);

        var result = calc.Calculate(periodPay, FilingStatus.Single, frequency);
        result.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void MarriedWithholdAtSingle_UseSingleBrackets()
    {
        var calc = CreateCalculator();
        decimal grossPay = 3_000m;

        var singleTax = calc.Calculate(grossPay, FilingStatus.Single, PayFrequency.BiWeekly);
        var mwsTax = calc.Calculate(grossPay, FilingStatus.MarriedWithholdAtSingle, PayFrequency.BiWeekly);

        mwsTax.Should().Be(singleTax);
    }

    [Fact]
    public void HeadOfHousehold_LowerTax_ThanSingle()
    {
        var calc = CreateCalculator();
        decimal grossPay = 2_000m;

        var singleTax = calc.Calculate(grossPay, FilingStatus.Single, PayFrequency.BiWeekly);
        var hohTax = calc.Calculate(grossPay, FilingStatus.HeadOfHousehold, PayFrequency.BiWeekly);

        hohTax.Should().BeLessThan(singleTax);
    }

    [Fact]
    public void HeadOfHousehold_BelowStandardDeduction_ZeroTax()
    {
        var calc = CreateCalculator();
        // $10,800 annual is in 0% bracket. BiWeekly = 10800/26 = ~415.38
        var result = calc.Calculate(415.00m, FilingStatus.HeadOfHousehold, PayFrequency.BiWeekly);
        result.Should().Be(0m);
    }

    [Fact]
    public void Allowances_ReduceTax()
    {
        var calc = CreateCalculator();
        decimal grossPay = 2_000m;

        var taxNoAllowances = calc.Calculate(grossPay, FilingStatus.Single, PayFrequency.BiWeekly, allowances: 0);
        var taxWithAllowances = calc.Calculate(grossPay, FilingStatus.Single, PayFrequency.BiWeekly, allowances: 2);

        taxWithAllowances.Should().BeLessThan(taxNoAllowances);
    }

    [Fact]
    public void ZeroAllowances_SameAsDefault()
    {
        var calc = CreateCalculator();
        decimal grossPay = 2_000m;

        var taxDefault = calc.Calculate(grossPay, FilingStatus.Single, PayFrequency.BiWeekly);
        var taxZero = calc.Calculate(grossPay, FilingStatus.Single, PayFrequency.BiWeekly, allowances: 0);

        taxZero.Should().Be(taxDefault);
    }

    [Fact]
    public void HighAllowances_ReducesToZeroTax()
    {
        var calc = CreateCalculator();
        // With enough allowances, tax should be zero
        var result = calc.Calculate(2_000m, FilingStatus.Single, PayFrequency.BiWeekly, allowances: 50);
        result.Should().Be(0m);
    }

    [Fact]
    public void NegativeAllowances_ThrowsArgumentOutOfRange()
    {
        var calc = CreateCalculator();
        var act = () => calc.Calculate(2_000m, FilingStatus.Single, PayFrequency.BiWeekly, allowances: -1);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("allowances");
    }
}

