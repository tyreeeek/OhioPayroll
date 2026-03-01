using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Extensions;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.ViewModels;

// --- Row ViewModels ---

public partial class CompanyBankAccountRow : ObservableObject
{
    public int Id { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string MaskedRouting { get; set; } = string.Empty;
    public string MaskedAccount { get; set; } = string.Empty;
    public bool IsDefaultForChecks { get; set; }
    public bool IsDefaultForAch { get; set; }
    public string DefaultDisplay => IsDefaultForChecks && IsDefaultForAch ? "Checks + ACH"
        : IsDefaultForChecks ? "Checks"
        : IsDefaultForAch ? "ACH"
        : "";
}

public partial class EmployeeBankAccountRow : ObservableObject
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string MaskedRouting { get; set; } = string.Empty;
    public string MaskedAccount { get; set; } = string.Empty;
    public BankAccountType AccountType { get; set; }
    public bool IsActive { get; set; }
    public string AccountTypeDisplay => AccountType.ToString();
    public string StatusText => IsActive ? "Active" : "Inactive";
}

// --- Main ViewModel ---

public partial class BankAccountsViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    // --- Section titles ---

    [ObservableProperty]
    private string _title = "Bank Accounts";

    [ObservableProperty]
    private string _subtitle = "Manage company and employee bank accounts for check printing and direct deposit.";

    // --- Company bank account collections ---

    [ObservableProperty]
    private ObservableCollection<CompanyBankAccountRow> _companyAccounts = new();

    [ObservableProperty]
    private CompanyBankAccountRow? _selectedCompanyAccount;

    // --- Employee bank account collections ---

    [ObservableProperty]
    private ObservableCollection<EmployeeBankAccountRow> _employeeAccounts = new();

    [ObservableProperty]
    private EmployeeBankAccountRow? _selectedEmployeeAccount;

    // --- Editing state ---

    [ObservableProperty]
    private bool _isEditingCompany;

    [ObservableProperty]
    private bool _isNewCompanyAccount;

    [ObservableProperty]
    private bool _isEditingEmployee;

    [ObservableProperty]
    private bool _isNewEmployeeAccount;

    // --- Company form fields ---

    [ObservableProperty]
    private int _editingCompanyId;

    [ObservableProperty]
    private string _companyBankName = string.Empty;

    [ObservableProperty]
    private string _companyRoutingNumber = string.Empty;

    [ObservableProperty]
    private string _companyAccountNumber = string.Empty;

    [ObservableProperty]
    private bool _companyIsDefaultForChecks;

    [ObservableProperty]
    private bool _companyIsDefaultForAch;

    // --- Employee form fields ---

    [ObservableProperty]
    private int _editingEmployeeAccountId;

    [ObservableProperty]
    private int _selectedEmployeeId;

    [ObservableProperty]
    private string _employeeRoutingNumber = string.Empty;

    [ObservableProperty]
    private string _employeeAccountNumber = string.Empty;

    [ObservableProperty]
    private BankAccountType _employeeAccountType;

    [ObservableProperty]
    private bool _employeeIsActive = true;

    // --- Employees for the dropdown ---

    [ObservableProperty]
    private ObservableCollection<EmployeeOption> _employeeOptions = new();

    // --- Validation ---

    [ObservableProperty]
    private string? _validationError;

    // --- Enum sources ---

    public BankAccountType[] AccountTypeValues { get; } = Enum.GetValues<BankAccountType>();

    // --- Computed ---

    public string CompanyFormTitle => IsNewCompanyAccount ? "Add Company Bank Account" : "Edit Company Bank Account";
    public string EmployeeFormTitle => IsNewEmployeeAccount ? "Add Employee Bank Account" : "Edit Employee Bank Account";

    public BankAccountsViewModel(PayrollDbContext db, IEncryptionService encryption, IAuditService audit)
    {
        _db = db;
        _encryption = encryption;
        _audit = audit;

        ExecuteWithLoadingAsync(LoadAllAsync, "Loading bank accounts...")
            .FireAndForgetSafeAsync(errorContext: "loading bank accounts");
    }

    partial void OnIsNewCompanyAccountChanged(bool value)
    {
        OnPropertyChanged(nameof(CompanyFormTitle));
    }

    partial void OnIsNewEmployeeAccountChanged(bool value)
    {
        OnPropertyChanged(nameof(EmployeeFormTitle));
    }

    // --- Load ---

    [RelayCommand]
    private async Task LoadAllAsync()
    {
        await LoadCompanyAccountsAsync();
        await LoadEmployeeAccountsAsync();
        await LoadEmployeeOptionsAsync();
    }

    private async Task LoadCompanyAccountsAsync()
    {
        var accounts = await _db.CompanyBankAccounts
            .AsNoTracking()
            .OrderBy(a => a.BankName)
            .ToListAsync();

        var rows = accounts.Select(a => new CompanyBankAccountRow
        {
            Id = a.Id,
            BankName = a.BankName,
            MaskedRouting = MaskNumber(SafeDecrypt(a.EncryptedRoutingNumber)),
            MaskedAccount = MaskNumber(SafeDecrypt(a.EncryptedAccountNumber)),
            IsDefaultForChecks = a.IsDefaultForChecks,
            IsDefaultForAch = a.IsDefaultForAch
        }).ToList();

        CompanyAccounts = new ObservableCollection<CompanyBankAccountRow>(rows);
    }

    private async Task LoadEmployeeAccountsAsync()
    {
        var accounts = await _db.EmployeeBankAccounts
            .AsNoTracking()
            .Include(a => a.Employee)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();

        var rows = accounts.Select(a => new EmployeeBankAccountRow
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            EmployeeName = a.Employee.FullName,
            MaskedRouting = MaskNumber(SafeDecrypt(a.EncryptedRoutingNumber)),
            MaskedAccount = MaskNumber(SafeDecrypt(a.EncryptedAccountNumber)),
            AccountType = a.AccountType,
            IsActive = a.IsActive
        }).ToList();

        EmployeeAccounts = new ObservableCollection<EmployeeBankAccountRow>(rows);
    }

    private async Task LoadEmployeeOptionsAsync()
    {
        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new EmployeeOption { Id = e.Id, FullName = e.FullName })
            .ToListAsync();

        EmployeeOptions = new ObservableCollection<EmployeeOption>(employees);
    }

    // --- Company CRUD ---

    [RelayCommand]
    private void AddCompanyAccount()
    {
        IsNewCompanyAccount = true;
        EditingCompanyId = 0;
        ClearCompanyForm();
        IsEditingCompany = true;
    }

    [RelayCommand]
    private async Task EditCompanyAccountAsync()
    {
        if (SelectedCompanyAccount is null) return;

        var account = await _db.CompanyBankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == SelectedCompanyAccount.Id);

        if (account is null) return;

        IsNewCompanyAccount = false;
        EditingCompanyId = account.Id;

        CompanyBankName = account.BankName;
        CompanyRoutingNumber = SafeDecrypt(account.EncryptedRoutingNumber);
        CompanyAccountNumber = SafeDecrypt(account.EncryptedAccountNumber);
        CompanyIsDefaultForChecks = account.IsDefaultForChecks;
        CompanyIsDefaultForAch = account.IsDefaultForAch;
        ValidationError = null;

        IsEditingCompany = true;
    }

    [RelayCommand]
    private async Task SaveCompanyAccountAsync()
    {
        // Validate
        if (string.IsNullOrWhiteSpace(CompanyBankName))
        {
            ValidationError = "Bank name is required.";
            return;
        }

        var cleanRouting = CompanyRoutingNumber.Replace(" ", "").Replace("-", "");
        if (!IsValidRoutingNumber(cleanRouting))
        {
            ValidationError = "Routing number must be 9 digits with a valid ABA checksum.";
            return;
        }

        var cleanAccount = CompanyAccountNumber.Replace(" ", "").Replace("-", "");
        if (string.IsNullOrWhiteSpace(cleanAccount) || cleanAccount.Length < 4 || !cleanAccount.All(char.IsDigit))
        {
            ValidationError = "Account number must be at least 4 digits.";
            return;
        }

        ValidationError = null;

        var encryptedRouting = _encryption.Encrypt(cleanRouting);
        var encryptedAccount = _encryption.Encrypt(cleanAccount);

        // Optimistic concurrency: Retry up to 3 times on conflict
        const int maxRetries = 3;
        int attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    if (IsNewCompanyAccount)
                    {
                        // Clear existing defaults if needed
                        if (CompanyIsDefaultForChecks)
                            await ClearCompanyDefaultForChecksInternalAsync();
                        if (CompanyIsDefaultForAch)
                            await ClearCompanyDefaultForAchInternalAsync();

                        var account = new CompanyBankAccount
                        {
                            BankName = CompanyBankName.Trim(),
                            EncryptedRoutingNumber = encryptedRouting,
                            EncryptedAccountNumber = encryptedAccount,
                            IsDefaultForChecks = CompanyIsDefaultForChecks,
                            IsDefaultForAch = CompanyIsDefaultForAch,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _db.CompanyBankAccounts.Add(account);
                        await _db.SaveChangesAsync();

                        await transaction.CommitAsync();

                        await _audit.LogAsync("Created", "CompanyBankAccount", account.Id,
                            newValue: $"{account.BankName}");
                    }
                    else
                    {
                        var account = await _db.CompanyBankAccounts.FindAsync(EditingCompanyId);
                        if (account is null)
                        {
                            await transaction.RollbackAsync();
                            return;
                        }

                        var oldValue = $"{account.BankName}";

                        // Clear existing defaults if this one is becoming default
                        if (CompanyIsDefaultForChecks && !account.IsDefaultForChecks)
                            await ClearCompanyDefaultForChecksInternalAsync();
                        if (CompanyIsDefaultForAch && !account.IsDefaultForAch)
                            await ClearCompanyDefaultForAchInternalAsync();

                        account.BankName = CompanyBankName.Trim();
                        account.EncryptedRoutingNumber = encryptedRouting;
                        account.EncryptedAccountNumber = encryptedAccount;
                        account.IsDefaultForChecks = CompanyIsDefaultForChecks;
                        account.IsDefaultForAch = CompanyIsDefaultForAch;
                        account.UpdatedAt = DateTime.UtcNow;

                        await _db.SaveChangesAsync();

                        await transaction.CommitAsync();

                        await _audit.LogAsync("Updated", "CompanyBankAccount", account.Id,
                            oldValue: oldValue,
                            newValue: $"{account.BankName}");
                    }

                    IsEditingCompany = false;
                    await LoadCompanyAccountsAsync();
                    break; // Success - exit retry loop
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                attempt++;
                if (attempt >= maxRetries)
                {
                    ValidationError = "This bank account was modified by another user. Please refresh and try again.";
                    AppLogger.Error($"Concurrency conflict after {maxRetries} retries on CompanyBankAccount", ex);
                    return;
                }

                AppLogger.Information($"Concurrency conflict on CompanyBankAccount, retry {attempt}/{maxRetries}");

                // Reload all affected entities
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync();
                }

                // Exponential backoff
                await Task.Delay(100 * attempt);
            }
        }
    }

    [RelayCommand]
    private async Task DeleteCompanyAccountAsync()
    {
        if (SelectedCompanyAccount is null) return;

        var account = await _db.CompanyBankAccounts.FindAsync(SelectedCompanyAccount.Id);
        if (account is null) return;

        var name = account.BankName;

        _db.CompanyBankAccounts.Remove(account);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Deleted", "CompanyBankAccount", SelectedCompanyAccount.Id,
            oldValue: name);

        await LoadCompanyAccountsAsync();
    }

    [RelayCommand]
    private void CancelCompanyEdit()
    {
        IsEditingCompany = false;
        ValidationError = null;
    }

    private async Task ClearCompanyDefaultForChecksAsync()
    {
        await ClearCompanyDefaultForChecksInternalAsync();
        await _db.SaveChangesAsync();
    }

    private async Task ClearCompanyDefaultForChecksInternalAsync()
    {
        var existing = await _db.CompanyBankAccounts
            .Where(a => a.IsDefaultForChecks)
            .ToListAsync();

        foreach (var a in existing)
        {
            a.IsDefaultForChecks = false;
            a.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task ClearCompanyDefaultForAchAsync()
    {
        await ClearCompanyDefaultForAchInternalAsync();
        await _db.SaveChangesAsync();
    }

    private async Task ClearCompanyDefaultForAchInternalAsync()
    {
        var existing = await _db.CompanyBankAccounts
            .Where(a => a.IsDefaultForAch)
            .ToListAsync();

        foreach (var a in existing)
        {
            a.IsDefaultForAch = false;
            a.UpdatedAt = DateTime.UtcNow;
        }
    }

    private void ClearCompanyForm()
    {
        CompanyBankName = string.Empty;
        CompanyRoutingNumber = string.Empty;
        CompanyAccountNumber = string.Empty;
        CompanyIsDefaultForChecks = false;
        CompanyIsDefaultForAch = false;
        ValidationError = null;
    }

    // --- Employee CRUD ---

    [RelayCommand]
    private void AddEmployeeAccount()
    {
        IsNewEmployeeAccount = true;
        EditingEmployeeAccountId = 0;
        ClearEmployeeForm();
        IsEditingEmployee = true;
    }

    [RelayCommand]
    private async Task EditEmployeeAccountAsync()
    {
        if (SelectedEmployeeAccount is null) return;

        var account = await _db.EmployeeBankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == SelectedEmployeeAccount.Id);

        if (account is null) return;

        IsNewEmployeeAccount = false;
        EditingEmployeeAccountId = account.Id;

        SelectedEmployeeId = account.EmployeeId;
        EmployeeRoutingNumber = SafeDecrypt(account.EncryptedRoutingNumber);
        EmployeeAccountNumber = SafeDecrypt(account.EncryptedAccountNumber);
        EmployeeAccountType = account.AccountType;
        EmployeeIsActive = account.IsActive;
        ValidationError = null;

        IsEditingEmployee = true;
    }

    [RelayCommand]
    private async Task SaveEmployeeAccountAsync()
    {
        // Validate
        if (SelectedEmployeeId <= 0)
        {
            ValidationError = "Please select an employee.";
            return;
        }

        var cleanRouting = EmployeeRoutingNumber.Replace(" ", "").Replace("-", "");
        if (!IsValidRoutingNumber(cleanRouting))
        {
            ValidationError = "Routing number must be 9 digits with a valid ABA checksum.";
            return;
        }

        var cleanAccount = EmployeeAccountNumber.Replace(" ", "").Replace("-", "");
        if (string.IsNullOrWhiteSpace(cleanAccount) || cleanAccount.Length < 4 || !cleanAccount.All(char.IsDigit))
        {
            ValidationError = "Account number must be at least 4 digits.";
            return;
        }

        ValidationError = null;

        var encryptedRouting = _encryption.Encrypt(cleanRouting);
        var encryptedAccount = _encryption.Encrypt(cleanAccount);

        if (IsNewEmployeeAccount)
        {
            var account = new EmployeeBankAccount
            {
                EmployeeId = SelectedEmployeeId,
                EncryptedRoutingNumber = encryptedRouting,
                EncryptedAccountNumber = encryptedAccount,
                AccountType = EmployeeAccountType,
                IsActive = EmployeeIsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.EmployeeBankAccounts.Add(account);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "EmployeeBankAccount", account.Id,
                newValue: $"Employee #{account.EmployeeId} {account.AccountType}");
        }
        else
        {
            var account = await _db.EmployeeBankAccounts.FindAsync(EditingEmployeeAccountId);
            if (account is null) return;

            var oldValue = $"Employee #{account.EmployeeId} {account.AccountType}";

            account.EmployeeId = SelectedEmployeeId;
            account.EncryptedRoutingNumber = encryptedRouting;
            account.EncryptedAccountNumber = encryptedAccount;
            account.AccountType = EmployeeAccountType;
            account.IsActive = EmployeeIsActive;
            account.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "EmployeeBankAccount", account.Id,
                oldValue: oldValue,
                newValue: $"Employee #{account.EmployeeId} {account.AccountType}");
        }

        IsEditingEmployee = false;
        await LoadEmployeeAccountsAsync();
    }

    [RelayCommand]
    private async Task DeleteEmployeeAccountAsync()
    {
        if (SelectedEmployeeAccount is null) return;

        var account = await _db.EmployeeBankAccounts.FindAsync(SelectedEmployeeAccount.Id);
        if (account is null) return;

        _db.EmployeeBankAccounts.Remove(account);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Deleted", "EmployeeBankAccount", SelectedEmployeeAccount.Id,
            oldValue: $"Employee #{account.EmployeeId}");

        await LoadEmployeeAccountsAsync();
    }

    [RelayCommand]
    private void CancelEmployeeEdit()
    {
        IsEditingEmployee = false;
        ValidationError = null;
    }

    private void ClearEmployeeForm()
    {
        SelectedEmployeeId = 0;
        EmployeeRoutingNumber = string.Empty;
        EmployeeAccountNumber = string.Empty;
        EmployeeAccountType = BankAccountType.Checking;
        EmployeeIsActive = true;
        ValidationError = null;
    }

    // --- Helpers ---

    private string SafeDecrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;
        try { return _encryption.Decrypt(encrypted); }
        catch { return string.Empty; }
    }

    private static string MaskNumber(string plain)
    {
        if (string.IsNullOrEmpty(plain) || plain.Length < 4)
            return "****";
        return "****" + plain[^4..];
    }

    private static bool IsValidRoutingNumber(string routing)
    {
        if (routing.Length != 9 || !routing.All(char.IsDigit))
            return false;

        // ABA checksum: d1*3 + d2*7 + d3*1 + d4*3 + d5*7 + d6*1 + d7*3 + d8*7 + d9*1
        int[] weights = { 3, 7, 1, 3, 7, 1, 3, 7, 1 };
        int sum = 0;
        for (int i = 0; i < 9; i++)
            sum += (routing[i] - '0') * weights[i];

        return sum % 10 == 0;
    }
}

// --- Helper class for employee dropdown ---

public class EmployeeOption
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public override string ToString() => FullName;
}

