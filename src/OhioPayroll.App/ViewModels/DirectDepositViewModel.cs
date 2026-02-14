using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.ViewModels;

// --- Row ViewModel ---

public class AchPreviewRow
{
    public string EmployeeName { get; set; } = string.Empty;
    public string RoutingLast4 { get; set; } = string.Empty;
    public string AccountLast4 { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// --- Main ViewModel ---

public partial class DirectDepositViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    [ObservableProperty]
    private string _title = "Direct Deposit";

    [ObservableProperty]
    private string _subtitle = "Generate NACHA/ACH files for direct deposit processing.";

    // --- Collections ---

    [ObservableProperty]
    private ObservableCollection<PayrollRunRow> _payrollRunRows = new();

    [ObservableProperty]
    private PayrollRunRow? _selectedRun;

    [ObservableProperty]
    private ObservableCollection<AchPreviewRow> _achPreviewRows = new();

    // --- Summary info ---

    [ObservableProperty]
    private int _ddEmployeeCount;

    [ObservableProperty]
    private decimal _ddTotalAmount;

    [ObservableProperty]
    private int _missingDdCount;

    [ObservableProperty]
    private string? _missingDdWarning;

    // --- Status ---

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isBusy;

    public DirectDepositViewModel(PayrollDbContext db, IEncryptionService encryption, IAuditService audit)
    {
        _db = db;
        _encryption = encryption;
        _audit = audit;

        _ = LoadPayrollRunsAsync();
    }

    // --- Load payroll runs ---

    [RelayCommand]
    private async Task LoadPayrollRunsAsync()
    {
        var runs = await _db.PayrollRuns
            .AsNoTracking()
            .Where(r => r.Status == PayrollRunStatus.Finalized)
            .OrderByDescending(r => r.PayDate)
            .Select(r => new PayrollRunRow
            {
                Id = r.Id,
                PayDate = r.PayDate,
                PeriodStart = r.PeriodStart,
                PeriodEnd = r.PeriodEnd,
                EmployeeCount = r.Paychecks.Count,
                TotalNetPay = r.TotalNetPay
            })
            .ToListAsync();

        PayrollRunRows = new ObservableCollection<PayrollRunRow>(runs);
    }

    // --- When selected run changes, load ACH preview ---

    partial void OnSelectedRunChanged(PayrollRunRow? value)
    {
        if (value is not null)
        {
            _ = LoadAchPreviewAsync(value.Id);
        }
        else
        {
            AchPreviewRows = new ObservableCollection<AchPreviewRow>();
            DdEmployeeCount = 0;
            DdTotalAmount = 0;
            MissingDdCount = 0;
            MissingDdWarning = null;
        }
    }

    private async Task LoadAchPreviewAsync(int payrollRunId)
    {
        // Get all non-void paychecks for this run
        var paychecks = await _db.Paychecks
            .AsNoTracking()
            .Include(p => p.Employee)
            .Where(p => p.PayrollRunId == payrollRunId && !p.IsVoid)
            .OrderBy(p => p.Employee.LastName)
            .ThenBy(p => p.Employee.FirstName)
            .ToListAsync();

        // Get all active employee bank accounts
        var employeeBankAccounts = await _db.EmployeeBankAccounts
            .AsNoTracking()
            .Where(b => b.IsActive)
            .ToListAsync();

        var bankAccountsByEmployee = employeeBankAccounts
            .GroupBy(b => b.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        var previewRows = new ObservableCollection<AchPreviewRow>();
        int withDd = 0;
        int withoutDd = 0;
        decimal totalAmount = 0;

        foreach (var paycheck in paychecks)
        {
            if (bankAccountsByEmployee.TryGetValue(paycheck.EmployeeId, out var bankAccount))
            {
                var routing = SafeDecrypt(bankAccount.EncryptedRoutingNumber);
                var account = SafeDecrypt(bankAccount.EncryptedAccountNumber);

                previewRows.Add(new AchPreviewRow
                {
                    EmployeeName = paycheck.Employee.FullName,
                    RoutingLast4 = MaskLast4(routing),
                    AccountLast4 = MaskLast4(account),
                    AccountType = bankAccount.AccountType.ToString(),
                    Amount = paycheck.NetPay
                });

                withDd++;
                totalAmount += paycheck.NetPay;
            }
            else
            {
                withoutDd++;
            }
        }

        AchPreviewRows = previewRows;
        DdEmployeeCount = withDd;
        DdTotalAmount = totalAmount;
        MissingDdCount = withoutDd;

        if (withoutDd > 0)
        {
            MissingDdWarning = $"{withoutDd} employee(s) do not have bank accounts set up and will not be included in the ACH file.";
        }
        else
        {
            MissingDdWarning = null;
        }
    }

    // --- Generate ACH File ---

    [RelayCommand]
    private async Task GenerateAchFileAsync()
    {
        if (SelectedRun is null)
        {
            ShowError("Please select a payroll run first.");
            return;
        }

        if (DdEmployeeCount == 0)
        {
            ShowError("No employees with direct deposit bank accounts found in this run.");
            return;
        }

        IsBusy = true;
        StatusMessage = null;

        try
        {
            // Load company info
            var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
            if (company is null)
            {
                ShowError("Company info not found. Please configure it in Settings.");
                return;
            }

            // Load company bank account for ACH
            var companyBank = await _db.CompanyBankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.IsDefaultForAch);
            if (companyBank is null)
            {
                ShowError("No default company bank account for ACH. Please configure one in Bank Accounts.");
                return;
            }

            var companyRouting = SafeDecrypt(companyBank.EncryptedRoutingNumber);
            var companyAccount = SafeDecrypt(companyBank.EncryptedAccountNumber);

            // Load paychecks with employee data
            var paychecks = await _db.Paychecks
                .AsNoTracking()
                .Include(p => p.Employee)
                .Where(p => p.PayrollRunId == SelectedRun.Id && !p.IsVoid)
                .OrderBy(p => p.Employee.LastName)
                .ThenBy(p => p.Employee.FirstName)
                .ToListAsync();

            // Load employee bank accounts
            var employeeBankAccounts = await _db.EmployeeBankAccounts
                .AsNoTracking()
                .Where(b => b.IsActive)
                .ToListAsync();

            var bankAccountsByEmployee = employeeBankAccounts
                .GroupBy(b => b.EmployeeId)
                .ToDictionary(g => g.Key, g => g.First());

            // Build ACH entries
            var entries = new System.Collections.Generic.List<AchEntry>();
            foreach (var paycheck in paychecks)
            {
                if (bankAccountsByEmployee.TryGetValue(paycheck.EmployeeId, out var bankAccount))
                {
                    entries.Add(new AchEntry
                    {
                        EmployeeName = paycheck.Employee.FullName,
                        EmployeeId = paycheck.EmployeeId.ToString(),
                        RoutingNumber = SafeDecrypt(bankAccount.EncryptedRoutingNumber),
                        AccountNumber = SafeDecrypt(bankAccount.EncryptedAccountNumber),
                        AccountType = bankAccount.AccountType,
                        Amount = paycheck.NetPay
                    });
                }
            }

            if (entries.Count == 0)
            {
                ShowError("No valid ACH entries to generate.");
                return;
            }

            // Generate ACH file
            var achService = new AchFileService();
            var achContent = achService.GenerateAchFile(
                company.CompanyName,
                company.Ein,
                companyRouting,
                companyAccount,
                companyBank.BankName,
                SelectedRun.PayDate,
                entries);

            // Save to output directory
            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OhioPayroll", "ACH");
            Directory.CreateDirectory(outputDir);

            var fileName = $"ACH_Run{SelectedRun.Id}_{SelectedRun.PayDate:yyyyMMdd}.ach";
            var filePath = Path.Combine(outputDir, fileName);
            await File.WriteAllTextAsync(filePath, achContent);

            await _audit.LogAsync("GeneratedACH", "PayrollRun", SelectedRun.Id,
                newValue: $"{entries.Count} entries, total {entries.Sum(e => e.Amount):C}");

            AppLogger.Information($"Generated ACH file for payroll run #{SelectedRun.Id}: {entries.Count} entries, total {entries.Sum(e => e.Amount):C}");
            ShowSuccess($"ACH file with {entries.Count} entries saved to:\n{filePath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating ACH file: {ex.Message}", ex);
            ShowError($"Error generating ACH file: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Helpers ---

    private string SafeDecrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;
        try { return _encryption.Decrypt(encrypted); }
        catch { return string.Empty; }
    }

    private static string MaskLast4(string plain)
    {
        if (string.IsNullOrEmpty(plain) || plain.Length < 4)
            return "****";
        return "****" + plain[^4..];
    }

    private void ShowSuccess(string message)
    {
        StatusMessage = message;
        IsStatusError = false;
    }

    private void ShowError(string message)
    {
        StatusMessage = message;
        IsStatusError = true;
    }
}

