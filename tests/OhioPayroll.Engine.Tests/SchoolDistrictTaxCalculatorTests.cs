using FluentAssertions;
using OhioPayroll.Engine.Calculators;

namespace OhioPayroll.Engine.Tests;

public class SchoolDistrictTaxCalculatorTests
{
    [Fact]
    public void StandardRate_CalculatesCorrectly()
    {
        var result = SchoolDistrictTaxCalculator.Calculate(2_000m, 0.0175m);
        result.Should().Be(35.00m);
    }

    [Fact]
    public void ZeroRate_ReturnsZero()
    {
        SchoolDistrictTaxCalculator.Calculate(2_000m, 0m).Should().Be(0m);
    }

    [Fact]
    public void ZeroGross_ReturnsZero()
    {
        SchoolDistrictTaxCalculator.Calculate(0m, 0.0175m).Should().Be(0m);
    }
}
