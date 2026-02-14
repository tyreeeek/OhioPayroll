using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Interfaces;

public interface IPayrollCalculationEngine
{
    PaycheckCalculationResult CalculatePaycheck(
        Employee employee,
        decimal regularHours,
        decimal overtimeHours,
        PayFrequency frequency,
        decimal ytdGrossPrior,
        decimal ytdSocialSecurityPrior,
        decimal ytdFutaPrior,
        decimal ytdSutaPrior,
        decimal schoolDistrictRate,
        decimal localTaxRate,
        decimal? customSutaRate = null);
}

