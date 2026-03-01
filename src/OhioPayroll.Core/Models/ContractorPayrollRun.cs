using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class ContractorPayrollRun
{
    public int Id { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime PayDate { get; set; }
    public PayFrequency PayFrequency { get; set; }
    public ContractorPayrollRunStatus Status { get; set; } = ContractorPayrollRunStatus.Draft;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = Environment.UserName;  // Audit trail
    public DateTime? FinalizedAt { get; set; }
    public string? FinalizedBy { get; set; }  // Audit trail

    // Optimistic concurrency token
    public byte[]? RowVersion { get; set; }

    public ICollection<ContractorPayment> Payments { get; set; } = new List<ContractorPayment>();
}
