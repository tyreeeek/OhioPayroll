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
        // Use taxYear 2025 where SS wage cap is 176,100
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 174_100m, taxYear: 2025);

        ss.Should().Be(124.00m);       // 2000 fully under cap (174100 + 2000 = 176100)
        medicare.Should().Be(29.00m);
    }

    [Fact]
    public void Employee_CrossingWageCap_PartialSocialSecurity()
    {
        // Use taxYear 2025 where SS wage cap is 176,100
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 175_000m, taxYear: 2025);

        // Cap is 176,100. Prior is 175,000. Only 1,100 is taxable for SS
        ss.Should().Be(68.20m);        // 1100 * 0.062
        medicare.Should().Be(29.00m);  // Medicare has no cap
    }

    [Fact]
    public void Employee_OverWageCap_ZeroSocialSecurity()
    {
        // Use taxYear 2025 where SS wage cap is 176,100
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 176_100m, taxYear: 2025);

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
    public void Employer_MatchesEmployee_WhenBelowAdditionalMedicareThreshold()
    {
        // Below $200K threshold, employee and employer FICA should match
        var employee = FicaCalculator.CalculateEmployee(3_000m, 0m, taxYear: 2025);
        var employer = FicaCalculator.CalculateEmployer(3_000m, 0m, taxYear: 2025);

        employer.Should().Be(employee);
    }

    [Fact]
    public void Employee_AdditionalMedicareTax_AboveThreshold()
    {
        // Employee crossing $200K threshold gets additional 0.9% Medicare
        var (ss, medicare) = FicaCalculator.CalculateEmployee(
            5_000m, 199_000m, ytdGrossWagesPrior: 199_000m, taxYear: 2025);

        // SS: prior 199,000 + 5,000 = 204,000 > cap 176,100, so remaining = 0
        ss.Should().Be(0m);
        // Regular Medicare: 5,000 * 0.0145 = 72.50
        // Additional: (204,000 - 200,000) * 0.009 = 36.00
        medicare.Should().Be(72.50m + 36.00m);
    }

    [Fact]
    public void Employer_NoAdditionalMedicareTax_AboveThreshold()
    {
        // Employer does NOT pay Additional Medicare Tax
        var (ss, medicare) = FicaCalculator.CalculateEmployer(
            5_000m, 199_000m, taxYear: 2025);

        ss.Should().Be(0m);
        // Only regular Medicare: 5,000 * 0.0145 = 72.50 (no additional)
        medicare.Should().Be(72.50m);
    }

    [Fact]
    public void ZeroGrossPay_ReturnsZero()
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(0m, 0m);

        ss.Should().Be(0m);
        medicare.Should().Be(0m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Extended Year Support Tests (2027-2030 and beyond)
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(2020, 137_700)]
    [InlineData(2021, 142_800)]
    [InlineData(2022, 147_000)]
    [InlineData(2023, 160_200)]
    [InlineData(2024, 168_600)]
    [InlineData(2025, 176_100)]
    [InlineData(2026, 184_500)]
    public void GetSocialSecurityWageCap_OfficialYears_ReturnsCorrectCap(int year, decimal expectedCap)
    {
        var cap = FicaCalculator.GetSocialSecurityWageCap(year);
        cap.Should().Be(expectedCap);
    }

    [Theory]
    [InlineData(2027, 191_000)]
    [InlineData(2028, 197_700)]
    [InlineData(2029, 204_600)]
    [InlineData(2030, 211_800)]
    public void GetSocialSecurityWageCap_ProjectedYears_ReturnsProjectedCap(int year, decimal expectedCap)
    {
        var cap = FicaCalculator.GetSocialSecurityWageCap(year);
        cap.Should().Be(expectedCap);
    }

    [Fact]
    public void GetSocialSecurityWageCap_FutureYear2031_ReturnsEstimatedCap()
    {
        // Year 2031 should use EstimateFutureWageCap with ~3.5% annual growth
        var cap = FicaCalculator.GetSocialSecurityWageCap(2031);

        // Base 2030 = 211,800 * 1.035 = 219,213, rounded to nearest 100 = 219,200
        cap.Should().BeGreaterThan(211_800m);
        cap.Should().BeLessThan(230_000m);
        // Should be rounded to nearest $100
        (cap % 100).Should().Be(0);
    }

    [Fact]
    public void GetSocialSecurityWageCap_FutureYear2035_ReturnsReasonableEstimate()
    {
        var cap = FicaCalculator.GetSocialSecurityWageCap(2035);

        // 5 years of ~3.5% growth from 2030 base of 211,800
        // 211,800 * (1.035)^5 ≈ 251,600
        cap.Should().BeGreaterThan(240_000m);
        cap.Should().BeLessThan(270_000m);
    }

    [Fact]
    public void GetSocialSecurityWageCap_Year2019_ThrowsArgumentOutOfRange()
    {
        var act = () => FicaCalculator.GetSocialSecurityWageCap(2019);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Employee_Year2027_UsesProjectedCap()
    {
        // Verify calculations work with projected 2027 cap (191,000)
        var (ss, medicare) = FicaCalculator.CalculateEmployee(2_000m, 190_000m, taxYear: 2027);

        // Only 1,000 is taxable (191,000 - 190,000)
        ss.Should().Be(62.00m);  // 1000 * 0.062
        medicare.Should().Be(29.00m);
    }

    [Fact]
    public void Employee_Year2030_UsesProjectedCap()
    {
        // Verify calculations work with projected 2030 cap (211,800)
        var (ss, medicare) = FicaCalculator.CalculateEmployee(5_000m, 210_000m, taxYear: 2030);

        // Only 1,800 is taxable (211,800 - 210,000)
        ss.Should().Be(111.60m);  // 1800 * 0.062
        medicare.Should().Be(72.50m);
    }

    [Fact]
    public void Employer_Year2031_UsesEstimatedCap()
    {
        // Verify employer calculations work with estimated future cap
        var cap2031 = FicaCalculator.GetSocialSecurityWageCap(2031);
        var (ss, medicare) = FicaCalculator.CalculateEmployer(5_000m, cap2031 - 2_000m, taxYear: 2031);

        // Only 2,000 is taxable
        ss.Should().Be(124.00m);  // 2000 * 0.062
        medicare.Should().Be(72.50m);
    }
}

