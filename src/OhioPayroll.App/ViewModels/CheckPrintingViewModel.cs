using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Documents;
using OhioPayroll.App.Extensions;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;
using QuestPDF.Fluent;

namespace OhioPayroll.App.ViewModels;

// --- Row ViewModels ---

public class PayrollRunRow
{
    public int Id { get; set; }
    public DateTime PayDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalNetPay { get; set; }
    public string Display => $"Run #{Id} \u2014 {PayDate:d} \u2014 {EmployeeCount} employees \u2014 {TotalNetPay:C}";

    public override string ToString() => Display;
}

public class PaycheckRow
{
    public int PaycheckId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int? CheckNumber { get; set; }
    public decimal GrossPay { get; set; }
    public decimal NetPay { get; set; }
    public bool IsVoid { get; set; }
    public string Status => IsVoid ? "VOID" : "Active";
}

// --- Main ViewModel ---

public partial class CheckPrintingViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    [ObservableProperty]
    private string _title = "Check Printing";

    [ObservableProperty]
    private string _subtitle = "Select a finalized payroll run to print checks and paystubs.";

    // --- Collections ---

    [ObservableProperty]
    private ObservableCollection<PayrollRunRow> _payrollRunRows = new();

    [ObservableProperty]
    private PayrollRunRow? _selectedRun;

    [ObservableProperty]
    private ObservableCollection<PaycheckRow> _paycheckRows = new();

    [ObservableProperty]
    private PaycheckRow? _selectedPaycheckRow;

    public bool CanVoidSelectedPaycheck =>
        SelectedPaycheckRow is not null && !SelectedPaycheckRow.IsVoid;

    partial void OnSelectedPaycheckRowChanged(PaycheckRow? value)
    {
        OnPropertyChanged(nameof(CanVoidSelectedPaycheck));
    }

    // --- Status ---

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isBusy;

    public CheckPrintingViewModel(PayrollDbContext db, IEncryptionService encryption, IAuditService audit)
    {
        _db = db;
        _encryption = encryption;
        _audit = audit;

        ExecuteWithLoadingAsync(LoadPayrollRunsAsync, "Loading payroll runs...")
            .FireAndForgetSafeAsync(errorContext: "loading payroll runs");
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

    // --- When selected run changes, load paychecks ---

    partial void OnSelectedRunChanged(PayrollRunRow? value)
    {
        if (value is not null)
        {
            ExecuteWithLoadingAsync(() => LoadPaychecksAsync(value.Id), "Loading paychecks...")
                .FireAndForgetSafeAsync(errorContext: "loading paychecks");
        }
        else
        {
            PaycheckRows = new ObservableCollection<PaycheckRow>();
        }
    }

    private async Task LoadPaychecksAsync(int payrollRunId)
    {
        var paychecks = await _db.Paychecks
            .AsNoTracking()
            .Include(p => p.Employee)
            .Where(p => p.PayrollRunId == payrollRunId)
            .OrderBy(p => p.Employee.LastName)
            .ThenBy(p => p.Employee.FirstName)
            .Select(p => new PaycheckRow
            {
                PaycheckId = p.Id,
                EmployeeName = p.Employee.FirstName + " " + p.Employee.LastName,
                CheckNumber = p.CheckNumber,
                GrossPay = p.GrossPay,
                NetPay = p.NetPay,
                IsVoid = p.IsVoid
            })
            .ToListAsync();

        PaycheckRows = new ObservableCollection<PaycheckRow>(paychecks);
    }

    // --- Print Checks ---

    [RelayCommand]
    private async Task PrintChecksAsync()
    {
        if (SelectedRun is null)
        {
            ShowError("Please select a payroll run first.");
            return;
        }

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
            if (company is null)
            {
                ShowError("Company info not found. Please configure it in Settings.");
                return;
            }

            var bankAccount = await _db.CompanyBankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.IsDefaultForChecks);
            if (bankAccount is null)
            {
                ShowError("No default company bank account for checks. Please configure one in Bank Accounts.");
                return;
            }

            var routingNumber = SafeDecrypt(bankAccount.EncryptedRoutingNumber);
            var accountNumber = SafeDecrypt(bankAccount.EncryptedAccountNumber);

            var settings = await _db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal offsetX = settings?.CheckOffsetX ?? 0;
            decimal offsetY = settings?.CheckOffsetY ?? 0;

            var paychecks = await _db.Paychecks
                .AsNoTracking()
                .Include(p => p.Employee)
                .Include(p => p.PayrollRun)
                .Where(p => p.PayrollRunId == SelectedRun.Id && !p.IsVoid)
                .OrderBy(p => p.Employee.LastName)
                .ThenBy(p => p.Employee.FirstName)
                .ToListAsync();

            if (paychecks.Count == 0)
            {
                ShowError("No active paychecks found in this run.");
                return;
            }

            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OhioPayroll", "Checks");
            Directory.CreateDirectory(outputDir);

            int count = 0;
            foreach (var paycheck in paychecks)
            {
                var doc = new CheckDocument(
                    company, paycheck.Employee, paycheck, paycheck.PayrollRun,
                    routingNumber, accountNumber, offsetX, offsetY);

                var fileName = $"Check_Run{SelectedRun.Id}_{paycheck.Employee.LastName}_{paycheck.Employee.FirstName}_{paycheck.Id}.pdf";
                var filePath = Path.Combine(outputDir, SanitizeFileName(fileName));
                doc.GeneratePdf(filePath);
                count++;
            }

            await _audit.LogAsync("PrintedChecks", "PayrollRun", SelectedRun.Id,
                newValue: $"{count} checks generated");

            AppLogger.Information($"Generated {count} check PDF(s) for payroll run #{SelectedRun.Id}");
            ShowSuccess($"{count} check PDF(s) saved to:\n{outputDir}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating checks: {ex.Message}", ex);
            ShowError($"Error generating checks: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Print Paystubs ---

    [RelayCommand]
    private async Task PrintPaystubsAsync()
    {
        if (SelectedRun is null)
        {
            ShowError("Please select a payroll run first.");
            return;
        }

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
            if (company is null)
            {
                ShowError("Company info not found. Please configure it in Settings.");
                return;
            }

            var paychecks = await _db.Paychecks
                .AsNoTracking()
                .Include(p => p.Employee)
                .Include(p => p.PayrollRun)
                .Where(p => p.PayrollRunId == SelectedRun.Id && !p.IsVoid)
                .OrderBy(p => p.Employee.LastName)
                .ThenBy(p => p.Employee.FirstName)
                .ToListAsync();

            if (paychecks.Count == 0)
            {
                ShowError("No active paychecks found in this run.");
                return;
            }

            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OhioPayroll", "Paystubs");
            Directory.CreateDirectory(outputDir);

            int count = 0;
            foreach (var paycheck in paychecks)
            {
                var doc = new PaystubDocument(company, paycheck.Employee, paycheck, paycheck.PayrollRun);

                var fileName = $"Paystub_Run{SelectedRun.Id}_{paycheck.Employee.LastName}_{paycheck.Employee.FirstName}_{paycheck.Id}.pdf";
                var filePath = Path.Combine(outputDir, SanitizeFileName(fileName));
                doc.GeneratePdf(filePath);
                count++;
            }

            await _audit.LogAsync("PrintedPaystubs", "PayrollRun", SelectedRun.Id,
                newValue: $"{count} paystubs generated");

            AppLogger.Information($"Generated {count} paystub PDF(s) for payroll run #{SelectedRun.Id}");
            ShowSuccess($"{count} paystub PDF(s) saved to:\n{outputDir}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating paystubs: {ex.Message}", ex);
            ShowError($"Error generating paystubs: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Print Calibration Page ---

    [RelayCommand]
    private async Task PrintCalibrationAsync()
    {
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OhioPayroll", "Checks");
            Directory.CreateDirectory(outputDir);

            var doc = new CheckCalibrationDocument();
            var filePath = Path.Combine(outputDir, "CheckCalibration.pdf");
            doc.GeneratePdf(filePath);

            await _audit.LogAsync("PrintedCalibration", "CheckCalibration", 0,
                newValue: "Calibration page generated");

            ShowSuccess($"Calibration page saved to:\n{filePath}");
        }
        catch (Exception ex)
        {
            ShowError($"Error generating calibration page: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Void Paycheck ---

    [RelayCommand]
    private async Task VoidPaycheckAsync()
    {
        if (SelectedPaycheckRow is null || SelectedPaycheckRow.IsVoid)
        {
            ShowError("Please select an active (non-void) paycheck to void.");
            return;
        }

        if (SelectedRun is null)
        {
            ShowError("No payroll run selected.");
            return;
        }

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var paycheckId = SelectedPaycheckRow.PaycheckId;

            // Load the original paycheck with tracking (not AsNoTracking)
            var original = await _db.Paychecks
                .Include(p => p.PayrollRun)
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.Id == paycheckId);

            if (original is null)
            {
                ShowError("Paycheck not found in the database.");
                return;
            }

            if (original.IsVoid)
            {
                ShowError("This paycheck has already been voided.");
                return;
            }

            // Capture old values for audit
            var oldValues = $"Gross={original.GrossPay:C}, Net={original.NetPay:C}, " +
                $"FedTax={original.FederalWithholding:C}, OhioTax={original.OhioStateWithholding:C}, " +
                $"SS={original.SocialSecurityTax:C}, Med={original.MedicareTax:C}, " +
                $"CheckNumber={original.CheckNumber}";

            // 1. Mark original paycheck as voided
            original.IsVoid = true;
            original.VoidDate = DateTime.UtcNow;
            original.VoidReason = "Voided by user";

            // 2. Update CheckRegisterEntry for the original paycheck
            var checkEntry = await _db.CheckRegister
                .FirstOrDefaultAsync(c => c.PaycheckId == original.Id);

            if (checkEntry is not null)
            {
                checkEntry.Status = CheckStatus.Voided;
                checkEntry.VoidDate = DateTime.UtcNow;
                checkEntry.VoidReason = "Voided by user";
            }

            // 3. Create compensating (negated) paycheck
            var compensating = new Paycheck
            {
                PayrollRunId = original.PayrollRunId,
                EmployeeId = original.EmployeeId,
                RegularHours = -original.RegularHours,
                OvertimeHours = -original.OvertimeHours,
                RegularPay = -original.RegularPay,
                OvertimePay = -original.OvertimePay,
                GrossPay = -original.GrossPay,
                FederalWithholding = -original.FederalWithholding,
                OhioStateWithholding = -original.OhioStateWithholding,
                SchoolDistrictTax = -original.SchoolDistrictTax,
                LocalMunicipalityTax = -original.LocalMunicipalityTax,
                SocialSecurityTax = -original.SocialSecurityTax,
                MedicareTax = -original.MedicareTax,
                EmployerSocialSecurity = -original.EmployerSocialSecurity,
                EmployerMedicare = -original.EmployerMedicare,
                EmployerFuta = -original.EmployerFuta,
                EmployerSuta = -original.EmployerSuta,
                TotalDeductions = -original.TotalDeductions,
                NetPay = -original.NetPay,
                // YTD snapshots: set to prior YTD values (before original paycheck)
                YtdGrossPay = original.YtdGrossPay - original.GrossPay,
                YtdFederalWithholding = original.YtdFederalWithholding - original.FederalWithholding,
                YtdOhioStateWithholding = original.YtdOhioStateWithholding - original.OhioStateWithholding,
                YtdSchoolDistrictTax = original.YtdSchoolDistrictTax - original.SchoolDistrictTax,
                YtdLocalTax = original.YtdLocalTax - original.LocalMunicipalityTax,
                YtdSocialSecurity = original.YtdSocialSecurity - original.SocialSecurityTax,
                YtdMedicare = original.YtdMedicare - original.MedicareTax,
                YtdNetPay = original.YtdNetPay - original.NetPay,
                PaymentMethod = PaymentMethod.Manual,
                CheckNumber = null,
                IsVoid = false,
                VoidReason = $"Compensating entry for voided paycheck #{original.Id}",
                OriginalPaycheckId = original.Id,
                CreatedAt = DateTime.UtcNow
            };

            _db.Paychecks.Add(compensating);

            // 4. Update PayrollRun totals (subtract voided amounts)
            var payrollRun = original.PayrollRun;
            payrollRun.TotalGrossPay -= original.GrossPay;
            payrollRun.TotalNetPay -= original.NetPay;
            payrollRun.TotalFederalTax -= original.FederalWithholding;
            payrollRun.TotalStateTax -= original.OhioStateWithholding;
            payrollRun.TotalLocalTax -= original.LocalMunicipalityTax;
            payrollRun.TotalSocialSecurity -= original.SocialSecurityTax;
            payrollRun.TotalMedicare -= original.MedicareTax;
            payrollRun.TotalEmployerSocialSecurity -= original.EmployerSocialSecurity;
            payrollRun.TotalEmployerMedicare -= original.EmployerMedicare;
            payrollRun.TotalEmployerFuta -= original.EmployerFuta;
            payrollRun.TotalEmployerSuta -= original.EmployerSuta;

            // 5. Adjust TaxLiability records for the relevant quarter/year
            int quarter = GetQuarter(payrollRun.PayDate);
            int taxYear = payrollRun.PayDate.Year;

            await AdjustTaxLiabilityAsync(TaxType.Federal, taxYear, quarter, -original.FederalWithholding);
            await AdjustTaxLiabilityAsync(TaxType.Ohio, taxYear, quarter, -original.OhioStateWithholding);
            await AdjustTaxLiabilityAsync(TaxType.Local, taxYear, quarter, -original.LocalMunicipalityTax);
            await AdjustTaxLiabilityAsync(TaxType.SchoolDistrict, taxYear, quarter, -original.SchoolDistrictTax);
            await AdjustTaxLiabilityAsync(TaxType.FICA_SS, taxYear, quarter, -(original.SocialSecurityTax + original.EmployerSocialSecurity));
            await AdjustTaxLiabilityAsync(TaxType.FICA_Med, taxYear, quarter, -(original.MedicareTax + original.EmployerMedicare));
            await AdjustTaxLiabilityAsync(TaxType.FUTA, taxYear, quarter, -original.EmployerFuta);
            await AdjustTaxLiabilityAsync(TaxType.SUTA, taxYear, quarter, -original.EmployerSuta);

            // 6. Single SaveChangesAsync for atomicity
            await _db.SaveChangesAsync();

            // 7. Audit log
            var newValues = $"Voided paycheck #{original.Id}, compensating entry created, " +
                $"Gross={-original.GrossPay:C}, Net={-original.NetPay:C}";

            await _audit.LogAsync("VoidedPaycheck", "Paycheck", original.Id,
                oldValue: oldValues, newValue: newValues);

            // 8. Refresh the paycheck list
            await LoadPaychecksAsync(SelectedRun.Id);

            // Also refresh the payroll run list to reflect updated totals
            await LoadPayrollRunsAsync();

            ShowSuccess($"Paycheck #{original.Id} for {original.Employee.FirstName} {original.Employee.LastName} " +
                $"has been voided. Compensating entry created.");
        }
        catch (Exception ex)
        {
            ShowError($"Error voiding paycheck: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AdjustTaxLiabilityAsync(TaxType taxType, int taxYear, int quarter, decimal adjustment)
    {
        var liability = await _db.TaxLiabilities
            .FirstOrDefaultAsync(t => t.TaxType == taxType
                && t.TaxYear == taxYear
                && t.Quarter == quarter);

        if (liability is not null)
        {
            liability.AmountOwed += adjustment;
            liability.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static int GetQuarter(DateTime date) => (date.Month - 1) / 3 + 1;

    // --- Helpers ---

    private string SafeDecrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;
        try { return _encryption.Decrypt(encrypted); }
        catch { return string.Empty; }
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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}

