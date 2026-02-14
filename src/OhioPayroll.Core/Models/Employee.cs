using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string EncryptedSsn { get; set; } = string.Empty;
    public string SsnLast4 { get; set; } = string.Empty;
    public PayType PayType { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal AnnualSalary { get; set; }
    public FilingStatus FederalFilingStatus { get; set; }
    public FilingStatus OhioFilingStatus { get; set; }
    public int FederalAllowances { get; set; }
    public int OhioExemptions { get; set; }
    public string? SchoolDistrictCode { get; set; }
    public string? MunicipalityCode { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = "OH";
    public string ZipCode { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Paycheck> Paychecks { get; set; } = new List<Paycheck>();
    public ICollection<EmployeeBankAccount> BankAccounts { get; set; } = new List<EmployeeBankAccount>();

    public string FullName => $"{FirstName} {LastName}";
}
