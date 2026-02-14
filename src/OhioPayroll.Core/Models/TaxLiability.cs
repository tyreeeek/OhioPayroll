using System.ComponentModel.DataAnnotations;
using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class TaxLiability
{
    public int Id { get; set; }
    public TaxType TaxType { get; set; }
    public int TaxYear { get; set; }

    [Range(1, 4)]
    public int Quarter { get; set; }

    public DateTime PeriodStart { get; set; }
    // PeriodEnd should be >= PeriodStart (enforced via EF configuration check constraint)
    public DateTime PeriodEnd { get; set; }
    public decimal AmountOwed { get; set; }
    public decimal AmountPaid { get; set; }
    public TaxLiabilityStatus Status { get; set; } = TaxLiabilityStatus.Unpaid;
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

