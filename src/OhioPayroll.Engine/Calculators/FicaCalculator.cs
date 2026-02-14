namespace OhioPayroll.Engine.Calculators;

public static class FicaCalculator
{
    public const decimal SocialSecurityRate = 0.062m;
    public const decimal MedicareRate = 0.0145m;
    public const decimal SocialSecurityWageCap2025 = 176_100m;

    public static (decimal socialSecurity, decimal medicare) CalculateEmployee(
        decimal grossPay,
        decimal ytdSocialSecurityWagesPrior,
        decimal wageCap = 0)
    {
        if (wageCap == 0) wageCap = SocialSecurityWageCap2025;

        decimal ss = 0m;
        decimal remainingCap = wageCap - ytdSocialSecurityWagesPrior;

        if (remainingCap > 0)
        {
            decimal ssWages = Math.Min(grossPay, remainingCap);
            ss = Math.Round(ssWages * SocialSecurityRate, 2);
        }

        decimal medicare = Math.Round(grossPay * MedicareRate, 2);
        return (ss, medicare);
    }

    public static (decimal socialSecurity, decimal medicare) CalculateEmployer(
        decimal grossPay,
        decimal ytdSocialSecurityWagesPrior,
        decimal wageCap = 0)
    {
        return CalculateEmployee(grossPay, ytdSocialSecurityWagesPrior, wageCap);
    }
}
