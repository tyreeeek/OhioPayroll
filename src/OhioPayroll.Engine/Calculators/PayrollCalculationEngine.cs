using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Engine.TaxTables;

namespace OhioPayroll.Engine.Calculators;

public class PayrollCalculationEngine : IPayrollCalculationEngine
{
    private readonly FederalTaxCalculator _federalCalc;
    private readonly OhioStateTaxCalculator _ohioCalc;

    public PayrollCalculationEngine(
        FederalTaxCalculator federalCalc,
        OhioStateTaxCalculator ohioCalc)
    {
        _federalCalc = federalCalc;
        _ohioCalc = ohioCalc;
    }

    public PaycheckCalculationResult CalculatePaycheck(
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
        decimal? customSutaRate = null)
    {
        // Step 1: Gross Pay
        var (regularPay, overtimePay, grossPay) = GrossPayCalculator.Calculate(
            employee.PayType, employee.HourlyRate, employee.AnnualSalary,
            regularHours, overtimeHours, frequency);

        // Step 2: Federal Tax
        decimal federal = _federalCalc.Calculate(
            grossPay, employee.FederalFilingStatus, frequency,
            employee.FederalAllowances);

        // Step 3: Ohio State Tax
        decimal ohio = _ohioCalc.Calculate(
            grossPay, frequency, employee.OhioExemptions);

        // Step 4: School District Tax
        decimal schoolDistrict = SchoolDistrictTaxCalculator.Calculate(
            grossPay, schoolDistrictRate);

        // Step 5: Local Municipality Tax
        decimal local = LocalMunicipalityTaxCalculator.Calculate(
            grossPay, localTaxRate);

        // Step 6: Employee FICA
        var (empSs, empMed) = FicaCalculator.CalculateEmployee(
            grossPay, ytdSocialSecurityPrior);

        // Step 7: Employer FICA
        var (emplrSs, emplrMed) = FicaCalculator.CalculateEmployer(
            grossPay, ytdSocialSecurityPrior);

        // Step 8: FUTA + SUTA (uses separate YTD tracking for wage caps)
        var (futa, suta) = EmployerTaxCalculator.Calculate(
            grossPay, ytdFutaPrior, customSutaRate);

        var employeeTaxes = new TaxBreakdown
        {
            FederalWithholding = federal,
            OhioStateWithholding = ohio,
            SchoolDistrictTax = schoolDistrict,
            LocalMunicipalityTax = local,
            SocialSecurityTax = empSs,
            MedicareTax = empMed
        };

        return new PaycheckCalculationResult
        {
            RegularPay = regularPay,
            OvertimePay = overtimePay,
            GrossPay = grossPay,
            EmployeeTaxes = employeeTaxes,
            EmployerTaxes = new EmployerTaxBreakdown
            {
                SocialSecurity = emplrSs,
                Medicare = emplrMed,
                Futa = futa,
                Suta = suta
            },
            TotalDeductions = employeeTaxes.Total,
            NetPay = grossPay - employeeTaxes.Total
        };
    }
}
