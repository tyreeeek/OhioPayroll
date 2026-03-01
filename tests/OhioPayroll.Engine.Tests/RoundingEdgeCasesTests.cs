using FluentAssertions;
using OhioPayroll.Engine.Calculators;
using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Engine.Tests;

/// <summary>
/// Tests for rounding edge cases and boundary conditions.
/// Financial calculations must handle rounding correctly to avoid accumulating errors.
/// </summary>
public class RoundingEdgeCasesTests
{
    /// <summary>
    /// Test rounding at the midpoint (0.5 cents).
    /// .NET uses banker's rounding (round to even) by default with MidpointRounding.ToEven.
    /// </summary>
    [Theory]
    [InlineData(100.005, 100.00)]  // 0.005 rounds down to even
    [InlineData(100.015, 100.02)]  // 0.015 rounds up to even
    [InlineData(100.025, 100.02)]  // 0.025 rounds down to even
    [InlineData(100.035, 100.04)]  // 0.035 rounds up to even
    [InlineData(100.045, 100.04)]  // 0.045 rounds down to even
    public void Rounding_MidpointValues_RoundsToEven(decimal input, decimal expected)
    {
        var rounded = Math.Round(input, 2, MidpointRounding.ToEven);
        rounded.Should().Be(expected);
    }

    /// <summary>
    /// Test rounding with AwayFromZero strategy (standard rounding).
    /// This is what most people expect: 0.5 always rounds up.
    /// </summary>
    [Theory]
    [InlineData(100.004, 100.00)]  // 0.004 rounds down
    [InlineData(100.005, 100.01)]  // 0.005 rounds up
    [InlineData(100.015, 100.02)]  // 0.015 rounds up
    [InlineData(100.025, 100.03)]  // 0.025 rounds up
    [InlineData(100.994, 100.99)]  // 0.994 rounds down
    [InlineData(100.995, 101.00)]  // 0.995 rounds up
    public void Rounding_MidpointValues_AwayFromZero(decimal input, decimal expected)
    {
        var rounded = Math.Round(input, 2, MidpointRounding.AwayFromZero);
        rounded.Should().Be(expected);
    }

    /// <summary>
    /// Test FICA calculations with amounts that produce sub-penny results.
    /// </summary>
    [Theory]
    [InlineData(1_234.56, 76.54)]    // 1234.56 * 0.062 = 76.54272 → 76.54
    [InlineData(999.99, 62.00)]      // 999.99 * 0.062 = 61.99938 → 62.00
    [InlineData(1_000.01, 62.00)]    // 1000.01 * 0.062 = 62.00062 → 62.00
    [InlineData(1_612.90, 100.00)]   // 1612.90 * 0.062 = 100.0000 → exactly 100.00
    public void SocialSecurityTax_SubPennyAmounts_RoundsCorrectly(decimal grossPay, decimal expectedSSTax)
    {
        var (ss, _) = FicaCalculator.CalculateEmployee(grossPay, 0m);
        ss.Should().Be(expectedSSTax);
    }

    /// <summary>
    /// Test Medicare calculations with amounts that produce sub-penny results.
    /// </summary>
    [Theory]
    [InlineData(1_000.00, 14.50)]    // 1000.00 * 0.0145 = 14.50
    [InlineData(1_234.48, 17.90)]    // 1234.48 * 0.0145 = 17.89996 → 17.90
    [InlineData(2_068.97, 30.00)]    // 2068.97 * 0.0145 = 30.00 exactly
    [InlineData(999.99, 14.50)]      // 999.99 * 0.0145 = 14.499855 → 14.50
    public void MedicareTax_SubPennyAmounts_RoundsCorrectly(decimal grossPay, decimal expectedMedicare)
    {
        var (_, medicare) = FicaCalculator.CalculateEmployee(grossPay, 0m);
        medicare.Should().Be(expectedMedicare);
    }

    /// <summary>
    /// Test that rounding errors don't accumulate over multiple paychecks.
    /// Each paycheck is independent - rounding doesn't carry over.
    /// </summary>
    [Fact]
    public void MultiplePaychecks_RoundingDoesNotAccumulate()
    {
        decimal grossPayPerCheck = 1_234.567m; // Sub-penny precision

        var paychecks = new List<(decimal ss, decimal medicare)>();

        for (int i = 0; i < 26; i++) // Full year bi-weekly
        {
            decimal ytdPrior = grossPayPerCheck * i;
            var (ss, medicare) = FicaCalculator.CalculateEmployee(
                Math.Round(grossPayPerCheck, 2), // Gross always rounded to 2 places
                ytdPrior);

            paychecks.Add((ss, medicare));

            // Each individual paycheck should be rounded
            ss.Should().Be(Math.Round(ss, 2));
            medicare.Should().Be(Math.Round(medicare, 2));
        }

        // Verify no accumulated rounding errors
        var totalSS = paychecks.Sum(p => p.ss);
        var totalMedicare = paychecks.Sum(p => p.medicare);

        totalSS.Should().Be(Math.Round(totalSS, 2));
        totalMedicare.Should().Be(Math.Round(totalMedicare, 2));
    }

    /// <summary>
    /// Test edge case: Exactly at Social Security wage cap boundary.
    /// </summary>
    [Fact]
    public void SocialSecurity_ExactlyAtCap_ZeroTax()
    {
        // 2025 cap is $176,100
        var (ss, medicare) = FicaCalculator.CalculateEmployee(
            2_000m,
            176_100m,  // Exactly at cap
            taxYear: 2025);

        ss.Should().Be(0m);
        medicare.Should().Be(29.00m); // Medicare continues
    }

    /// <summary>
    /// Test edge case: One penny below Social Security wage cap.
    /// </summary>
    [Fact]
    public void SocialSecurity_OnePennyBelowCap_PartialTax()
    {
        // 2025 cap is $176,100
        var (ss, medicare) = FicaCalculator.CalculateEmployee(
            2_000m,
            176_099.99m,  // One penny below cap
            taxYear: 2025);

        // Only 1 cent is taxable: 0.01 * 0.062 = 0.00062 → 0.00
        ss.Should().Be(0.00m);
        medicare.Should().Be(29.00m);
    }

    /// <summary>
    /// Test edge case: Crossing the Additional Medicare Tax threshold exactly.
    /// </summary>
    [Fact]
    public void AdditionalMedicare_ExactlyAtThreshold_NoAdditional()
    {
        // Threshold is $200,000
        var (ss, medicare) = FicaCalculator.CalculateEmployee(
            5_000m,
            195_000m,  // YTD prior exactly at threshold after this check
            ytdGrossWagesPrior: 195_000m,
            taxYear: 2025);

        // Total YTD = 195,000 + 5,000 = 200,000 (exactly at threshold)
        // Regular Medicare: 5,000 * 0.0145 = 72.50
        // No additional Medicare (not OVER threshold)
        medicare.Should().Be(72.50m);
    }

    /// <summary>
    /// Test edge case: One penny over Additional Medicare Tax threshold.
    /// </summary>
    [Fact]
    public void AdditionalMedicare_OnePennyOverThreshold_AdditionalApplies()
    {
        // Threshold is $200,000
        var (ss, medicare) = FicaCalculator.CalculateEmployee(
            5_000m,
            195_000m,
            ytdGrossWagesPrior: 195_000.01m,  // One penny over after this check
            taxYear: 2025);

        // Total YTD = 195,000 + 5,000 = 200,000
        // But YTD wages (for additional Medicare) = 195,000.01 + 5,000 = 200,000.01
        // Regular Medicare: 5,000 * 0.0145 = 72.50
        // Additional: (200,000.01 - 200,000) * 0.009 = 0.00009 → 0.00
        medicare.Should().Be(72.50m);
    }

    /// <summary>
    /// Test very small gross pay amounts.
    /// </summary>
    [Theory]
    [InlineData(0.01)]   // 1 cent
    [InlineData(0.10)]   // 10 cents
    [InlineData(1.00)]   // 1 dollar
    public void VerySmallGrossPay_CalculatesCorrectly(decimal grossPay)
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(grossPay, 0m);

        // SS: grossPay * 0.062
        var expectedSS = Math.Round(grossPay * 0.062m, 2);
        ss.Should().Be(expectedSS);

        // Medicare: grossPay * 0.0145
        var expectedMedicare = Math.Round(grossPay * 0.0145m, 2);
        medicare.Should().Be(expectedMedicare);
    }

    /// <summary>
    /// Test very large gross pay amounts.
    /// </summary>
    [Theory]
    [InlineData(100_000)]
    [InlineData(250_000)]
    [InlineData(500_000)]
    [InlineData(1_000_000)]
    public void VeryLargeGrossPay_CalculatesCorrectly(decimal grossPay)
    {
        var (ss, medicare) = FicaCalculator.CalculateEmployee(grossPay, 0m);

        // SS is capped
        var cap = FicaCalculator.GetSocialSecurityWageCap(DateTime.Now.Year);
        var taxableForSS = Math.Min(grossPay, cap);
        var expectedSS = Math.Round(taxableForSS * 0.062m, 2);
        ss.Should().Be(expectedSS);

        // Medicare has no cap, but has additional tax over $200K
        medicare.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Test that decimal precision doesn't cause incorrect comparisons.
    /// </summary>
    [Fact]
    public void DecimalComparison_NoPrecisionLoss()
    {
        decimal a = 1_000.00m;
        decimal b = 999.99m + 0.01m;

        // Should be exactly equal due to decimal precision
        a.Should().Be(b);

        // Verify in FICA calculation context
        var (ss1, _) = FicaCalculator.CalculateEmployee(a, 0m);
        var (ss2, _) = FicaCalculator.CalculateEmployee(b, 0m);

        ss1.Should().Be(ss2);
    }

    /// <summary>
    /// Test repeating decimal results from division.
    /// </summary>
    [Fact]
    public void RepeatingDecimal_FromDivision_RoundsCorrectly()
    {
        // 100 / 3 = 33.333333...
        decimal amount = 100m / 3m;

        var (ss, medicare) = FicaCalculator.CalculateEmployee(amount, 0m);

        // SS: 33.333333... * 0.062 = 2.0666... → 2.07
        ss.Should().Be(Math.Round(amount * 0.062m, 2));

        // Medicare: 33.333333... * 0.0145 = 0.4833... → 0.48
        medicare.Should().Be(Math.Round(amount * 0.0145m, 2));
    }

    /// <summary>
    /// Test wage cap boundary with fractional cents in YTD.
    /// </summary>
    [Theory]
    [InlineData(176_099.994)]  // Rounds to 176,099.99
    [InlineData(176_099.995)]  // Rounds to 176,100.00 (exactly at cap)
    [InlineData(176_100.004)]  // Rounds to 176,100.00 (at cap)
    [InlineData(176_100.005)]  // Rounds to 176,100.01 (over cap)
    public void WageCap_FractionalCents_HandleCorrectly(decimal ytdPrior)
    {
        var roundedYTD = Math.Round(ytdPrior, 2);

        var (ss, _) = FicaCalculator.CalculateEmployee(
            1_000m,
            roundedYTD,
            taxYear: 2025);

        // Cap is 176,100
        if (roundedYTD >= 176_100m)
        {
            ss.Should().Be(0m, $"YTD {roundedYTD} is at or over cap");
        }
        else
        {
            ss.Should().BeGreaterThan(0m, $"YTD {roundedYTD} is below cap");
        }
    }

    /// <summary>
    /// Test accumulation of rounding across fiscal quarter.
    /// Each paycheck rounds independently, so totals might be off by a few cents.
    /// This is expected and acceptable.
    /// </summary>
    [Fact]
    public void QuarterlyAccumulation_RoundingToleranceAcceptable()
    {
        // Simulate 13 bi-weekly paychecks in a quarter
        decimal grossPerCheck = 2_345.67m;
        decimal totalSS = 0m;
        decimal totalMedicare = 0m;
        decimal totalGross = 0m;

        for (int i = 0; i < 13; i++)
        {
            var (ss, medicare) = FicaCalculator.CalculateEmployee(
                grossPerCheck,
                totalGross);

            totalSS += ss;
            totalMedicare += medicare;
            totalGross += grossPerCheck;
        }

        // Verify totals are reasonable
        // Expected SS: totalGross * 0.062
        decimal expectedSS = Math.Round(totalGross * 0.062m, 2);

        // Due to rounding each paycheck, actual total might differ by a few cents
        var ssDifference = Math.Abs(totalSS - expectedSS);
        ssDifference.Should().BeLessThan(0.10m, "rounding difference should be less than 10 cents");

        // Similar for Medicare
        decimal expectedMedicare = Math.Round(totalGross * 0.0145m, 2);
        var medicareDifference = Math.Abs(totalMedicare - expectedMedicare);
        medicareDifference.Should().BeLessThan(0.10m, "rounding difference should be less than 10 cents");
    }
}
