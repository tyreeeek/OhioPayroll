using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class ContractorPayment
{
    public int Id { get; set; }
    public int ContractorId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public ContractorPaymentMethod PaymentMethod { get; set; }
    public string? CheckNumber { get; set; }
    public string? Reference { get; set; }
    public int TaxYear { get; set; } = DateTime.UtcNow.Year;
    public bool IsDeleted { get; set; }

    // Payroll run fields
    public int? ContractorPayrollRunId { get; set; }  // Nullable for backward compatibility
    public decimal? HoursWorked { get; set; }
    public decimal? DaysWorked { get; set; }

    // CRITICAL: Historical snapshots (immutable after finalization)
    public decimal? RateAtPayment { get; set; }
    public ContractorRateType? RateTypeAtPayment { get; set; }  // Snapshot rate type
    public string? ContractorNameSnapshot { get; set; }  // Snapshot name

    public ContractorPaymentType PaymentType { get; set; } = ContractorPaymentType.AdHoc;
    public bool HasPaystub { get; set; }
    public bool HasCheck { get; set; }
    public bool IsLocked { get; set; }  // Prevents editing after finalization

    // Audit trail
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = Environment.UserName;
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public Contractor Contractor { get; set; } = null!;
    public ContractorPayrollRun? ContractorPayrollRun { get; set; }
}
