using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class CheckRegisterEntry
{
    public int Id { get; set; }
    public int CheckNumber { get; set; }
    public int PaycheckId { get; set; }
    public CheckStatus Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public DateTime? VoidDate { get; set; }
    public string? VoidReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public Paycheck Paycheck { get; set; } = null!;
}

