using FluentAssertions;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.Calculators;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Tests;

public class OhioStateTaxCalculatorTests
{
    private static OhioStateTaxCalculator CreateCalculator()
    {
        var brackets = new List<TaxBracket>
        {
            new() { BracketStart = 0, BracketEnd = 26_050, Rate = 0.0000m, BaseAmount = 0 },
            new() { BracketStart = 26_050, BracketEnd = 100_000, Rate = 0.0275m, BaseAmount = 0 },
            new() { BracketStart = 100_000, BracketEnd = decimal.MaxValue, Rate = 0.03125m, BaseAmount = 2_033.63m },
        };
        return new OhioStateTaxCalculator(brackets);
    }

    [Fact]
    public void Income_BelowFirstBracket_ZeroTax()
    {
        var calc = CreateCalculator();
        // $25,000 annual, BiWeekly
        var result = calc.Calculate(961.54m, PayFrequency.BiWeekly, 0);
        result.Should().Be(0m);
    }

    [Fact]
    public void Income_InSecondBracket_CorrectRate()
    {
        var calc = CreateCalculator();
        // $60,000 annual. BiWeekly = 60000/26 = 2307.69
        var result = calc.Calculate(2_307.69m, PayFrequency.BiWeekly, 0);
        // Annualized: ~60000
        // Bracket: 26050-100000 at 2.75%
        // Tax = 0 + (60000 - 26050) * 0.0275 = 33950 * 0.0275 = 933.63
        // De-annualized: 933.63 / 26 = 35.91
        result.Should().BeGreaterThan(30m);
        result.Should().BeLessThan(40m);
    }

    [Fact]
    public void WithExemptions_ReducesTaxableWage()
    {
        var calc = CreateCalculator();
        decimal grossPay = 2_307.69m; // ~$60,000 annual

        var noExemptions = calc.Calculate(grossPay, PayFrequency.BiWeekly, 0);
        var withExemptions = calc.Calculate(grossPay, PayFrequency.BiWeekly, 2);

        withExemptions.Should().BeLessThan(noExemptions);
    }

    [Fact]
    public void ZeroGrossPay_ReturnsZero()
    {
        var calc = CreateCalculator();
        calc.Calculate(0m, PayFrequency.BiWeekly, 0).Should().Be(0m);
    }

    [Fact]
    public void HighIncome_TopBracket()
    {
        var calc = CreateCalculator();
        // $150,000 annual. BiWeekly = 150000/26 = 5769.23
        var result = calc.Calculate(5_769.23m, PayFrequency.BiWeekly, 0);
        // Should be higher than second bracket
        result.Should().BeGreaterThan(50m);
    }

    [Fact]
    public void Deterministic_SameInputs_SameOutput()
    {
        var calc = CreateCalculator();
        var r1 = calc.Calculate(3_000m, PayFrequency.BiWeekly, 1);
        var r2 = calc.Calculate(3_000m, PayFrequency.BiWeekly, 1);
        r1.Should().Be(r2);
    }
}
