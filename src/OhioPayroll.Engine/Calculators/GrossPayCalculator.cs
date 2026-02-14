using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Engine.Calculators;

public static class GrossPayCalculator
{
    public static (decimal regularPay, decimal overtimePay, decimal grossPay) Calculate(
        PayType payType,
        decimal hourlyRate,
        decimal annualSalary,
        decimal regularHours,
        decimal overtimeHours,
        PayFrequency frequency)
    {
        if (payType == PayType.Hourly)
        {
            var regularPay = Math.Round(regularHours * hourlyRate, 2);
            var overtimePay = Math.Round(overtimeHours * hourlyRate * 1.5m, 2);
            return (regularPay, overtimePay, regularPay + overtimePay);
        }
        else
        {
            int periods = (int)frequency;
            var regularPay = Math.Round(annualSalary / periods, 2);
            return (regularPay, 0m, regularPay);
        }
    }
}
