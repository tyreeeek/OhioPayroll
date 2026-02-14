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
    public int TaxYear { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Contractor Contractor { get; set; } = null!;
}
