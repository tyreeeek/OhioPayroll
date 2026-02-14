using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class EmployeeBankAccount
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EncryptedRoutingNumber { get; set; } = string.Empty;
    public string EncryptedAccountNumber { get; set; } = string.Empty;
    public BankAccountType AccountType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
}
