using FluentAssertions;
using OhioPayroll.Engine.Calculators;

namespace OhioPayroll.Engine.Tests;

public class FicaCalculatorTests
{
    [Fact]
    public void Employee_StandardPay_CalculatesBothTaxes()
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 0m);

        ss.Should().Be(124.00m);       // 2000 * 0.062
        medicare.Should().Be(29.00m);   // 2000 * 0.0145
    }

    [Fact]
    public void Employee_AtWageCap_FullSocialSecurity()
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 174_100m);

        ss.Should().Be(124.00m);       // 2000 fully under cap (174100 + 2000 = 176100)
        medicare.Should().Be(29.00m);
    }

    [Fact]
    public void Employee_CrossingWageCap_PartialSocialSecurity()
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 175_000m);

        // Cap is 176,100. Prior is 175,000. Only 1,100 is taxable for SS
        ss.Should().Be(68.20m);        // 1100 * 0.062
        medicare.Should().Be(29.00m);  // Medicare has no cap
    }

    [Fact]
    public void Employee_OverWageCap_ZeroSocialSecurity()
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 176_100m);

        ss.Should().Be(0m);
        medicare.Should().Be(29.00m);  // Medicare always applies
    }

    [Fact]
    public void Employee_WellOverCap_ZeroSocialSecurity()
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(5_000m, 200_000m);

        ss.Should().Be(0m);
        medicare.Should().Be(72.50m);
    }

    [Fact]
    public void Employer_MatchesEmployee()
    {
        var employee = FicaCalculator.CalculateEmployee(3_000m, 0m);
        var employer = FicaCalculator.CalculateEmployer(3_000m, 0m);

        employer.Should().Be(employee);
    }

    [Fact]
    public void ZeroGrossPay_ReturnsZero()
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(0m, 0m);

        ss.Should().Be(0m);
        medicare.Should().Be(0m);
    }
}
