namespace OhioPayroll.Engine.Calculators;

public static class FicaCalculator
{
    public const decimal SocialSecurityRate = 0.062m;
    public const decimal MedicareRate = 0.0145m;
    public const decimal AdditionalMedicareRate = 0.009m;
    public const decimal AdditionalMedicareThreshold = 200_000m;

    /// <summary>
    /// Returns the IRS Social Security wage base for the given tax year.
    /// Official values through 2026; estimates for 2027-2030 based on historical ~3.5% annual increase.
    /// Check IRS announcements each October for next year's official limit.
    /// Source: https://www.ssa.gov/oact/cola/cbb.html
    /// </summary>
    public static decimal GetSocialSecurityWageCap(int year) => year switch
    {
        // Official IRS published values
        2020 => 137_700m,
        2021 => 142_800m,
        2022 => 147_000m,
        2023 => 160_200m,
        2024 => 168_600m,
        2025 => 176_100m,
        2026 => 184_500m,
        // Projected values (update when IRS publishes official figures)
        // Based on ~3.5% annual increase historical trend
        2027 => 191_000m,  // Projected - update when official
        2028 => 197_700m,  // Projected - update when official
        2029 => 204_600m,  // Projected - update when official
        2030 => 211_800m,  // Projected - update when official
        _ when year > 2030 => EstimateFutureWageCap(year),
        _ => throw new ArgumentOutOfRangeException(nameof(year),
            $"Social Security wage cap is not available for year {year}. Supported years: 2020 and later.")
    };

    /// <summary>
    /// Estimates future wage caps for years beyond defined values using 3.5% annual growth.
    /// WARNING: These are estimates only. Update GetSocialSecurityWageCap with official IRS values
    /// as they are published each October.
    /// </summary>
    private static decimal EstimateFutureWageCap(int year)
    {
        const decimal annualGrowthRate = 0.035m;
        const int baseYear = 2030;
        const decimal baseAmount = 211_800m;

        int yearsAhead = year - baseYear;
        decimal estimated = baseAmount * (decimal)Math.Pow((double)(1 + annualGrowthRate), yearsAhead);

        // Round to nearest $100 (IRS typically rounds to nearest $300, but $100 is safer)
        return Math.Round(estimated / 100) * 100;
    }

    /// <summary>
    /// Calculates employee FICA: Social Security (6.2% up to wage cap) +
    /// Medicare (1.45%) + Additional Medicare Tax (0.9% on wages over $200K).
    /// </summary>
    public static (decimal socialSecurity, decimal medicare) CalculateEmployee(
        decimal grossPay,
        decimal ytdSocialSecurityWagesPrior,
        decimal ytdGrossWagesPrior = 0,
        int taxYear = 0)
    {
        if (taxYear == 0) taxYear = DateTime.Now.Year;
        var wageCap = GetSocialSecurityWageCap(taxYear);

        // Social Security (6.2% up to wage cap)
        decimal ss = 0m;
        decimal remainingCap = wageCap - ytdSocialSecurityWagesPrior;
        if (remainingCap > 0)
        {
            decimal ssWages = Math.Min(grossPay, remainingCap);
            ss = Math.Round(ssWages * SocialSecurityRate, 2, MidpointRounding.AwayFromZero);
        }

        // Regular Medicare (1.45%)
        decimal medicare = Math.Round(grossPay * MedicareRate, 2, MidpointRounding.AwayFromZero);

        // Additional Medicare Tax (0.9% on wages over $200K) — employee only
        decimal totalGrossAfterPay = ytdGrossWagesPrior + grossPay;
        if (totalGrossAfterPay > AdditionalMedicareThreshold)
        {
            decimal additionalWages = totalGrossAfterPay
                - Math.Max(ytdGrossWagesPrior, AdditionalMedicareThreshold);
            if (additionalWages > 0)
            {
                medicare += Math.Round(additionalWages * AdditionalMedicareRate, 2, MidpointRounding.AwayFromZero);
            }
        }

        return (ss, medicare);
    }

    /// <summary>
    /// Calculates employer FICA: Social Security (6.2% up to wage cap) +
    /// Medicare (1.45%). Employer does NOT pay Additional Medicare Tax.
    /// </summary>
    public static (decimal socialSecurity, decimal medicare) CalculateEmployer(
        decimal grossPay,
        decimal ytdSocialSecurityWagesPrior,
        int taxYear = 0)
    {
        if (taxYear == 0) taxYear = DateTime.Now.Year;
        var wageCap = GetSocialSecurityWageCap(taxYear);

        decimal ss = 0m;
        decimal remainingCap = wageCap - ytdSocialSecurityWagesPrior;
        if (remainingCap > 0)
        {
            decimal ssWages = Math.Min(grossPay, remainingCap);
            ss = Math.Round(ssWages * SocialSecurityRate, 2, MidpointRounding.AwayFromZero);
        }

        // Regular Medicare only (1.45%) — employer does NOT pay Additional Medicare Tax
        decimal medicare = Math.Round(grossPay * MedicareRate, 2, MidpointRounding.AwayFromZero);

        return (ss, medicare);
    }
}

