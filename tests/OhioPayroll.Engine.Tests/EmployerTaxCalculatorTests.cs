using FluentAssertions;
using OhioPayroll.Engine.Calculators;

namespace OhioPayroll.Engine.Tests;

public class EmployerTaxCalculatorTests
{
    [Fact]
    public void BelowBothCaps_FullTax()
    {
        var (futa, suta) = EmployerTaxCalculator.Calculate(2_000m, 0m);

        futa.Should().Be(12.00m);    // 2000 * 0.006
        suta.Should().Be(54.00m);    // 2000 * 0.027
    }

    [Fact]
    public void CrossingFutaCap_PartialFuta()
    {
        var (futa, suta) = EmployerTaxCalculator.Calculate(2_000m, 6_000m);

        // FUTA cap: 7000. Prior: 6000. Taxable: 1000
        futa.Should().Be(6.00m);     // 1000 * 0.006
        // SUTA cap: 9000. Prior: 6000. Taxable: 2000
        suta.Should().Be(54.00m);    // 2000 * 0.027
    }

    [Fact]
    public void OverFutaCap_ZeroFuta()
    {
        var (futa, suta) = EmployerTaxCalculator.Calculate(2_000m, 7_000m);

        futa.Should().Be(0m);
        // SUTA cap: 9000. Prior: 7000. Taxable: 2000
        suta.Should().Be(54.00m);
    }

    [Fact]
    public void OverBothCaps_ZeroBoth()
    {
        var (futa, suta) = EmployerTaxCalculator.Calculate(2_000m, 10_000m);

        futa.Should().Be(0m);
        suta.Should().Be(0m);
    }

    [Fact]
    public void CrossingSutaCap_PartialSuta()
    {
        var (futa, suta) = EmployerTaxCalculator.Calculate(2_000m, 8_000m);

        futa.Should().Be(0m);        // Over FUTA cap
        // SUTA cap: 9000. Prior: 8000. Taxable: 1000
        suta.Should().Be(27.00m);    // 1000 * 0.027
    }

    [Fact]
    public void CustomSutaRate_Used()
    {
        var (futa, suta) = EmployerTaxCalculator.Calculate(2_000m, 0m, 0.05m);

        futa.Should().Be(12.00m);
        suta.Should().Be(100.00m);   // 2000 * 0.05
    }
}
