namespace OhioPayroll.Core.Models;

public class CompanyBankAccount
{
    public int Id { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string EncryptedRoutingNumber { get; set; } = string.Empty;
    public string EncryptedAccountNumber { get; set; } = string.Empty;
    public bool IsDefaultForChecks { get; set; }
    public bool IsDefaultForAch { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

