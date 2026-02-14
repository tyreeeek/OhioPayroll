namespace OhioPayroll.Engine.Calculators;

public static class EmployerTaxCalculator
{
    public const decimal DefaultFutaRate = 0.006m;
    public const decimal FutaWageCap = 7_000m;
    public const decimal DefaultSutaRate = 0.027m;
    public const decimal OhioSutaWageCap = 9_000m;

    public static (decimal futa, decimal suta) Calculate(
        decimal grossPay,
        decimal ytdGrossPrior,
        decimal? customSutaRate = null)
    {
        decimal sutaRate = customSutaRate ?? DefaultSutaRate;

        decimal futaRemaining = Math.Max(0, FutaWageCap - ytdGrossPrior);
        decimal futaWages = Math.Min(grossPay, futaRemaining);
        decimal futa = Math.Round(futaWages * DefaultFutaRate, 2);

        decimal sutaRemaining = Math.Max(0, OhioSutaWageCap - ytdGrossPrior);
        decimal sutaWages = Math.Min(grossPay, sutaRemaining);
        decimal suta = Math.Round(sutaWages * sutaRate, 2);

        return (futa, suta);
    }
}

