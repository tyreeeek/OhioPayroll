using FluentAssertions;
using OhioPayroll.Engine.Calculators;

namespace OhioPayroll.Engine.Tests;

public class LocalTaxCalculatorTests
{
    [Fact]
    public void StandardRate_CalculatesCorrectly()
    {
        var result = LocalMunicipalityTaxCalculator.Calculate(2_000m, 0.025m);
        result.Should().Be(50.00m);
    }

    [Fact]
    public void ZeroRate_ReturnsZero()
    {
        LocalMunicipalityTaxCalculator.Calculate(2_000m, 0m).Should().Be(0m);
    }

    [Fact]
    public void ZeroGross_ReturnsZero()
    {
        LocalMunicipalityTaxCalculator.Calculate(0m, 0.025m).Should().Be(0m);
    }
}

