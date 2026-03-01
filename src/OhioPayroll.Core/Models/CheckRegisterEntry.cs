using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class CheckRegisterEntry
{
    public int Id { get; set; }
    public int CheckNumber { get; set; }

    // XOR constraint: Exactly ONE must be non-null (enforced at service layer)
    public int? PaycheckId { get; set; }  // Now nullable
    public int? ContractorPaymentId { get; set; }

    public CheckStatus Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public DateTime? VoidDate { get; set; }
    public string? VoidReason { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Paycheck? Paycheck { get; set; }
    public ContractorPayment? ContractorPayment { get; set; }
}

