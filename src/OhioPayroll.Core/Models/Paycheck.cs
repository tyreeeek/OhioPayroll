using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class Paycheck
{
    public int Id { get; set; }
    public int PayrollRunId { get; set; }
    public int EmployeeId { get; set; }

    // Earnings
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal RegularPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal GrossPay { get; set; }

    // Employee Taxes
    public decimal FederalWithholding { get; set; }
    public decimal OhioStateWithholding { get; set; }
    public decimal SchoolDistrictTax { get; set; }
    public decimal LocalMunicipalityTax { get; set; }
    public decimal SocialSecurityTax { get; set; }
    public decimal MedicareTax { get; set; }

    // Employer Taxes
    public decimal EmployerSocialSecurity { get; set; }
    public decimal EmployerMedicare { get; set; }
    public decimal EmployerFuta { get; set; }
    public decimal EmployerSuta { get; set; }

    // Totals
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    // YTD snapshots at time of finalization
    public decimal YtdGrossPay { get; set; }
    public decimal YtdFederalWithholding { get; set; }
    public decimal YtdOhioStateWithholding { get; set; }
    public decimal YtdSchoolDistrictTax { get; set; }
    public decimal YtdLocalTax { get; set; }
    public decimal YtdSocialSecurity { get; set; }
    public decimal YtdMedicare { get; set; }
    public decimal YtdNetPay { get; set; }

    // Payment
    public PaymentMethod PaymentMethod { get; set; }
    public int? CheckNumber { get; set; }
    public string? AchTraceNumber { get; set; }

    // Void tracking
    public bool IsVoid { get; set; }
    public DateTime? VoidDate { get; set; }
    public string? VoidReason { get; set; }
    public int? OriginalPaycheckId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public PayrollRun PayrollRun { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
