namespace OhioPayroll.Core.Models;

public record PaycheckCalculationResult
{
    public decimal RegularPay { get; init; }
    public decimal OvertimePay { get; init; }
    public decimal GrossPay { get; init; }
    public TaxBreakdown EmployeeTaxes { get; init; } = new();
    public EmployerTaxBreakdown EmployerTaxes { get; init; } = new();
    public decimal TotalDeductions { get; init; }
    public decimal NetPay { get; init; }
}

public record TaxBreakdown
{
    public decimal FederalWithholding { get; init; }
    public decimal OhioStateWithholding { get; init; }
    public decimal SchoolDistrictTax { get; init; }
    public decimal LocalMunicipalityTax { get; init; }
    public decimal SocialSecurityTax { get; init; }
    public decimal MedicareTax { get; init; }
    public decimal Total => FederalWithholding + OhioStateWithholding +
        SchoolDistrictTax + LocalMunicipalityTax + SocialSecurityTax + MedicareTax;
}

public record EmployerTaxBreakdown
{
    public decimal SocialSecurity { get; init; }
    public decimal Medicare { get; init; }
    public decimal Futa { get; init; }
    public decimal Suta { get; init; }
    public decimal Total => SocialSecurity + Medicare + Futa + Suta;
}

