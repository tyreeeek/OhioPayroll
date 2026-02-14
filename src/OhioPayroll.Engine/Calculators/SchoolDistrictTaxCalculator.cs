namespace OhioPayroll.Engine.Calculators;

public static class SchoolDistrictTaxCalculator
{
    public static decimal Calculate(decimal grossPay, decimal rate)
    {
        if (grossPay <= 0 || rate <= 0) return 0m;
        return Math.Round(grossPay * rate, 2);
    }
}
