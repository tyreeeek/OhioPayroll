using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.App.Services;
using OhioPayroll.Data;

namespace OhioPayroll.App.ViewModels;

// ── Helper row for Step 2 (Enter Hours) ──────────────────────────────────
public partial class PayrollEntryRow : ObservableObject
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public PayType PayType { get; set; }
    public decimal Rate { get; set; }

    [ObservableProperty]
    private decimal _regularHours;

    [ObservableProperty]
    private decimal _overtimeHours;

    public string DisplayRate => PayType == PayType.Hourly
        ? $"{Rate:C}/hr"
        : $"{Rate:C}/yr (salary)";

    public string PayTypeLabel => PayType == PayType.Hourly ? "Hourly" : "Salary";
}

// ── Helper row for Step 3 (Preview) ──────────────────────────────────────
public class PaycheckPreviewRow
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal RegularPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal GrossPay { get; set; }
    public decimal FederalTax { get; set; }
    public decimal OhioTax { get; set; }
    public decimal SsTax { get; set; }
    public decimal MedTax { get; set; }
    public decimal SchoolDistrictTax { get; set; }
    public decimal LocalTax { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    // Employer-side taxes (for summary)
    public decimal EmployerSs { get; set; }
    public decimal EmployerMed { get; set; }
    public decimal EmployerFuta { get; set; }
    public decimal EmployerSuta { get; set; }

    // Stored for finalization
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }

    // YTD priors (for YTD snapshot calculation at finalize)
    public decimal YtdGrossPrior { get; set; }
    public decimal YtdFederalPrior { get; set; }
    public decimal YtdOhioPrior { get; set; }
    public decimal YtdSchoolDistrictPrior { get; set; }
    public decimal YtdLocalPrior { get; set; }
    public decimal YtdSsPrior { get; set; }
    public decimal YtdMedPrior { get; set; }
    public decimal YtdNetPrior { get; set; }
}

// ── Main PayrollRun ViewModel (4-step wizard) ────────────────────────────
public partial class PayrollRunViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IPayrollCalculationEngine _engine;
    private readonly IAuditService _audit;

    // ── Step tracking ────────────────────────────────────────────────────
    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private string _stepTitle = "Step 1: Select Pay Period";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // ── Step 1: Select Period ────────────────────────────────────────────
    [ObservableProperty]
    private DateTimeOffset _periodStart = DateTimeOffset.Now.AddDays(-13);

    [ObservableProperty]
    private DateTimeOffset _periodEnd = DateTimeOffset.Now;

    [ObservableProperty]
    private DateTimeOffset _payDate = DateTimeOffset.Now.AddDays(3);

    [ObservableProperty]
    private PayFrequency _selectedFrequency = PayFrequency.BiWeekly;

    public List<PayFrequency> AvailableFrequencies { get; } =
        Enum.GetValues<PayFrequency>().ToList();

    // ── Step 2: Enter Hours ──────────────────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<PayrollEntryRow> _entryRows = new();

    // ── Step 3: Preview ──────────────────────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<PaycheckPreviewRow> _previewRows = new();

    [ObservableProperty]
    private decimal _totalGrossPay;

    [ObservableProperty]
    private decimal _totalNetPay;

    [ObservableProperty]
    private decimal _totalFederalTax;

    [ObservableProperty]
    private decimal _totalStateTax;

    [ObservableProperty]
    private decimal _totalLocalTax;

    [ObservableProperty]
    private decimal _totalSsTax;

    [ObservableProperty]
    private decimal _totalMedTax;

    [ObservableProperty]
    private decimal _totalSchoolDistrictTax;

    [ObservableProperty]
    private decimal _totalDeductions;

    [ObservableProperty]
    private decimal _totalEmployerSs;

    [ObservableProperty]
    private decimal _totalEmployerMed;

    [ObservableProperty]
    private decimal _totalEmployerFuta;

    [ObservableProperty]
    private decimal _totalEmployerSuta;

    [ObservableProperty]
    private int _employeeCount;

    // ── Step 4: Finalize ─────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isFinalized;

    [ObservableProperty]
    private string _finalizedMessage = string.Empty;

    [ObservableProperty]
    private int _finalizedRunId;

    // ── Computed visibility helpers ──────────────────────────────────────
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool CanGoBack => CurrentStep > 1 && CurrentStep < 4 && !IsFinalized;

    public PayrollRunViewModel(
        PayrollDbContext db,
        IPayrollCalculationEngine engine,
        IAuditService audit)
    {
        _db = db;
        _engine = engine;
        _audit = audit;

        _ = LoadSettingsDefaultsAsync();
    }

    private async Task LoadSettingsDefaultsAsync()
    {
        try
        {
            var settings = await _db.PayrollSettings.FirstOrDefaultAsync();
            if (settings is not null)
            {
                SelectedFrequency = settings.PayFrequency;
            }
        }
        catch
        {
            // Settings may not exist yet; defaults are fine
        }
    }

    partial void OnCurrentStepChanged(int value)
    {
        StepTitle = value switch
        {
            1 => "Step 1: Select Pay Period",
            2 => "Step 2: Enter Hours",
            3 => "Step 3: Preview Calculations",
            4 => "Step 4: Finalized",
            _ => StepTitle
        };

        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(CanGoBack));
    }

    // ── Next Step Command ────────────────────────────────────────────────
    [RelayCommand]
    private async Task NextStepAsync()
    {
        ErrorMessage = null;

        try
        {
            IsLoading = true;

            switch (CurrentStep)
            {
                case 1:
                    // Validate dates
                    if (PeriodStart >= PeriodEnd)
                    {
                        ErrorMessage = "Period Start must be before Period End.";
                        return;
                    }
                    if (PayDate < PeriodEnd)
                    {
                        ErrorMessage = "Pay Date should be on or after Period End.";
                        return;
                    }

                    // Validate pay period is not longer than 35 days
                    var periodDays = (PeriodEnd.DateTime - PeriodStart.DateTime).TotalDays;
                    if (periodDays > 35)
                    {
                        ErrorMessage = "Pay period cannot be longer than 35 days.";
                        return;
                    }

                    // Validate pay date is not more than 10 days after period end
                    var daysAfterEnd = (PayDate.DateTime - PeriodEnd.DateTime).TotalDays;
                    if (daysAfterEnd > 10)
                    {
                        ErrorMessage = "Pay date cannot be more than 10 days after the period end date.";
                        return;
                    }

                    // Check for duplicate finalized payroll run with exact same period dates
                    var duplicateExists = await _db.PayrollRuns
                        .AnyAsync(r => r.Status == PayrollRunStatus.Finalized
                            && r.PeriodStart.Date == PeriodStart.DateTime.Date
                            && r.PeriodEnd.Date == PeriodEnd.DateTime.Date);
                    if (duplicateExists)
                    {
                        ErrorMessage = "A finalized payroll run already exists for this exact pay period. Please adjust the dates.";
                        return;
                    }

                    // Check for overlapping finalized payroll runs
                    var newStart = PeriodStart.DateTime;
                    var newEnd = PeriodEnd.DateTime;
                    var overlappingRun = await _db.PayrollRuns
                        .AsNoTracking()
                        .Where(r => r.Status == PayrollRunStatus.Finalized)
                        .Where(r => r.PeriodStart < newEnd && r.PeriodEnd > newStart)
                        .Select(r => new { r.Id, r.PeriodStart, r.PeriodEnd })
                        .FirstOrDefaultAsync();

                    if (overlappingRun is not null)
                    {
                        ErrorMessage = $"A finalized payroll run (#{overlappingRun.Id}) already exists " +
                            $"with an overlapping pay period ({overlappingRun.PeriodStart:d} - {overlappingRun.PeriodEnd:d}). " +
                            $"Please adjust your period dates to avoid overlap.";
                        return;
                    }

                    // Load active employees for hour entry
                    await LoadEmployeesForEntryAsync();

                    if (EntryRows.Count == 0)
                    {
                        ErrorMessage = "No active employees found. Add employees before running payroll.";
                        return;
                    }

                    CurrentStep = 2;
                    break;

                case 2:
                    // Validate hours
                    foreach (var row in EntryRows)
                    {
                        // Overtime hours should never be negative
                        if (row.OvertimeHours < 0)
                        {
                            ErrorMessage = $"{row.EmployeeName}: Overtime hours cannot be negative.";
                            return;
                        }

                        if (row.PayType == PayType.Hourly)
                        {
                            // Hourly employees: negative hours are blocked
                            if (row.RegularHours < 0)
                            {
                                ErrorMessage = $"{row.EmployeeName}: Regular hours cannot be negative for hourly employees.";
                                return;
                            }
                            if (row.RegularHours == 0)
                            {
                                ErrorMessage = $"{row.EmployeeName}: Regular hours must be greater than zero for hourly employees.";
                                return;
                            }
                        }
                        // Salary employees: zero or negative RegularHours is warned but not blocked
                        // (salary employees may have zero-hour entries for partial periods)
                    }

                    // Run calculations
                    await CalculatePreviewAsync();
                    CurrentStep = 3;
                    break;

                case 3:
                    // Step 3 → finalize is handled by FinalizeCommand
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Previous Step Command ────────────────────────────────────────────
    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1 && !IsFinalized)
        {
            ErrorMessage = null;
            CurrentStep--;
        }
    }

    // ── Load Employees for Entry (Step 1 → 2) ───────────────────────────
    private async Task LoadEmployeesForEntryAsync()
    {
        var periodStartDate = PeriodStart.DateTime;
        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            // Exclude employees terminated before the pay period started
            // (employees terminated during the period still need their final paycheck)
            .Where(e => e.TerminationDate == null || e.TerminationDate >= periodStartDate)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();

        var rows = new ObservableCollection<PayrollEntryRow>();

        foreach (var emp in employees)
        {
            var row = new PayrollEntryRow
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.FullName,
                PayType = emp.PayType,
                Rate = emp.PayType == PayType.Hourly ? emp.HourlyRate : emp.AnnualSalary,
                OvertimeHours = 0m
            };

            // Pre-fill hours based on pay type and frequency
            if (emp.PayType == PayType.Salary)
            {
                // Salary employees: calculate standard hours for the pay period
                row.RegularHours = SelectedFrequency switch
                {
                    PayFrequency.Weekly => 40m,
                    PayFrequency.BiWeekly => 80m,
                    PayFrequency.SemiMonthly => 86.67m,
                    PayFrequency.Monthly => 173.33m,
                    _ => 80m
                };
            }
            else
            {
                row.RegularHours = 0m;
            }

            rows.Add(row);
        }

        EntryRows = rows;
    }

    // ── Calculate Preview (Step 2 → 3) ───────────────────────────────────
    private async Task CalculatePreviewAsync()
    {
        var settings = await _db.PayrollSettings.FirstOrDefaultAsync();
        var schoolDistrictRate = settings?.SchoolDistrictRate ?? 0m;
        var localTaxRate = settings?.LocalTaxRate ?? 0m;
        var sutaRate = settings?.SutaRate ?? 0.027m;

        int payDateYear = PayDate.Year;

        var previews = new ObservableCollection<PaycheckPreviewRow>();

        foreach (var entry in EntryRows)
        {
            // Load the full employee record
            var emp = await _db.Employees
                .AsNoTracking()
                .FirstAsync(e => e.Id == entry.EmployeeId);

            // Load YTD priors from finalized paychecks this year
            var ytdPaychecks = await _db.Paychecks
                .AsNoTracking()
                .Where(p => p.EmployeeId == emp.Id
                    && p.PayrollRun.PayDate.Year == payDateYear
                    && p.PayrollRun.Status == PayrollRunStatus.Finalized)
                .ToListAsync();

            decimal ytdGross = ytdPaychecks.Sum(p => p.GrossPay);
            decimal ytdSs = ytdPaychecks.Sum(p => p.SocialSecurityTax);
            decimal ytdFuta = ytdPaychecks.Sum(p => p.EmployerFuta);
            decimal ytdSuta = ytdPaychecks.Sum(p => p.EmployerSuta);
            decimal ytdFederal = ytdPaychecks.Sum(p => p.FederalWithholding);
            decimal ytdOhio = ytdPaychecks.Sum(p => p.OhioStateWithholding);
            decimal ytdSchool = ytdPaychecks.Sum(p => p.SchoolDistrictTax);
            decimal ytdLocal = ytdPaychecks.Sum(p => p.LocalMunicipalityTax);
            decimal ytdMed = ytdPaychecks.Sum(p => p.MedicareTax);
            decimal ytdNet = ytdPaychecks.Sum(p => p.NetPay);

            // Run the calculation engine
            var result = _engine.CalculatePaycheck(
                emp,
                entry.RegularHours,
                entry.OvertimeHours,
                SelectedFrequency,
                ytdGross,
                ytdSs,
                ytdFuta,
                ytdSuta,
                schoolDistrictRate,
                localTaxRate,
                sutaRate);

            previews.Add(new PaycheckPreviewRow
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.FullName,
                RegularPay = result.RegularPay,
                OvertimePay = result.OvertimePay,
                GrossPay = result.GrossPay,
                FederalTax = result.EmployeeTaxes.FederalWithholding,
                OhioTax = result.EmployeeTaxes.OhioStateWithholding,
                SsTax = result.EmployeeTaxes.SocialSecurityTax,
                MedTax = result.EmployeeTaxes.MedicareTax,
                SchoolDistrictTax = result.EmployeeTaxes.SchoolDistrictTax,
                LocalTax = result.EmployeeTaxes.LocalMunicipalityTax,
                TotalDeductions = result.TotalDeductions,
                NetPay = result.NetPay,
                EmployerSs = result.EmployerTaxes.SocialSecurity,
                EmployerMed = result.EmployerTaxes.Medicare,
                EmployerFuta = result.EmployerTaxes.Futa,
                EmployerSuta = result.EmployerTaxes.Suta,
                RegularHours = entry.RegularHours,
                OvertimeHours = entry.OvertimeHours,
                YtdGrossPrior = ytdGross,
                YtdFederalPrior = ytdFederal,
                YtdOhioPrior = ytdOhio,
                YtdSchoolDistrictPrior = ytdSchool,
                YtdLocalPrior = ytdLocal,
                YtdSsPrior = ytdSs,
                YtdMedPrior = ytdMed,
                YtdNetPrior = ytdNet
            });
        }

        PreviewRows = previews;

        // Compute run totals
        TotalGrossPay = previews.Sum(p => p.GrossPay);
        TotalNetPay = previews.Sum(p => p.NetPay);
        TotalFederalTax = previews.Sum(p => p.FederalTax);
        TotalStateTax = previews.Sum(p => p.OhioTax);
        TotalLocalTax = previews.Sum(p => p.LocalTax);
        TotalSsTax = previews.Sum(p => p.SsTax);
        TotalMedTax = previews.Sum(p => p.MedTax);
        TotalSchoolDistrictTax = previews.Sum(p => p.SchoolDistrictTax);
        TotalDeductions = previews.Sum(p => p.TotalDeductions);
        TotalEmployerSs = previews.Sum(p => p.EmployerSs);
        TotalEmployerMed = previews.Sum(p => p.EmployerMed);
        TotalEmployerFuta = previews.Sum(p => p.EmployerFuta);
        TotalEmployerSuta = previews.Sum(p => p.EmployerSuta);
        EmployeeCount = previews.Count;
    }

    // ── Finalize Command (Step 3 → 4) ───────────────────────────────────
    [RelayCommand]
    private async Task FinalizeAsync()
    {
        if (IsFinalized) return;

        ErrorMessage = null;

        try
        {
            IsLoading = true;

            var settings = await _db.PayrollSettings.FirstOrDefaultAsync();
            int nextCheckNumber = settings?.NextCheckNumber ?? 1001;

            // Create PayrollRun
            var run = new PayrollRun
            {
                PeriodStart = PeriodStart.DateTime,
                PeriodEnd = PeriodEnd.DateTime,
                PayDate = PayDate.DateTime,
                PayFrequency = SelectedFrequency,
                Status = PayrollRunStatus.Finalized,
                TotalGrossPay = TotalGrossPay,
                TotalNetPay = TotalNetPay,
                TotalFederalTax = TotalFederalTax,
                TotalStateTax = TotalStateTax,
                TotalLocalTax = TotalLocalTax,
                TotalSocialSecurity = TotalSsTax,
                TotalMedicare = TotalMedTax,
                TotalEmployerSocialSecurity = TotalEmployerSs,
                TotalEmployerMedicare = TotalEmployerMed,
                TotalEmployerFuta = TotalEmployerFuta,
                TotalEmployerSuta = TotalEmployerSuta,
                CreatedAt = DateTime.UtcNow,
                FinalizedAt = DateTime.UtcNow
            };

            _db.PayrollRuns.Add(run);
            await _db.SaveChangesAsync(); // generates run.Id

            // Create Paychecks and CheckRegisterEntries
            foreach (var preview in PreviewRows)
            {
                var paycheck = new Paycheck
                {
                    PayrollRunId = run.Id,
                    EmployeeId = preview.EmployeeId,
                    RegularHours = preview.RegularHours,
                    OvertimeHours = preview.OvertimeHours,
                    RegularPay = preview.RegularPay,
                    OvertimePay = preview.OvertimePay,
                    GrossPay = preview.GrossPay,
                    FederalWithholding = preview.FederalTax,
                    OhioStateWithholding = preview.OhioTax,
                    SchoolDistrictTax = preview.SchoolDistrictTax,
                    LocalMunicipalityTax = preview.LocalTax,
                    SocialSecurityTax = preview.SsTax,
                    MedicareTax = preview.MedTax,
                    EmployerSocialSecurity = preview.EmployerSs,
                    EmployerMedicare = preview.EmployerMed,
                    EmployerFuta = preview.EmployerFuta,
                    EmployerSuta = preview.EmployerSuta,
                    TotalDeductions = preview.TotalDeductions,
                    NetPay = preview.NetPay,
                    // YTD snapshots: prior + this paycheck
                    YtdGrossPay = preview.YtdGrossPrior + preview.GrossPay,
                    YtdFederalWithholding = preview.YtdFederalPrior + preview.FederalTax,
                    YtdOhioStateWithholding = preview.YtdOhioPrior + preview.OhioTax,
                    YtdSchoolDistrictTax = preview.YtdSchoolDistrictPrior + preview.SchoolDistrictTax,
                    YtdLocalTax = preview.YtdLocalPrior + preview.LocalTax,
                    YtdSocialSecurity = preview.YtdSsPrior + preview.SsTax,
                    YtdMedicare = preview.YtdMedPrior + preview.MedTax,
                    YtdNetPay = preview.YtdNetPrior + preview.NetPay,
                    PaymentMethod = PaymentMethod.Check,
                    CheckNumber = nextCheckNumber,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Paychecks.Add(paycheck);
                await _db.SaveChangesAsync(); // generates paycheck.Id

                // Create check register entry
                var checkEntry = new CheckRegisterEntry
                {
                    CheckNumber = nextCheckNumber,
                    PaycheckId = paycheck.Id,
                    Status = CheckStatus.Issued,
                    Amount = paycheck.NetPay,
                    IssuedDate = PayDate.DateTime,
                    CreatedAt = DateTime.UtcNow
                };

                _db.CheckRegister.Add(checkEntry);
                nextCheckNumber++;
            }

            // Create tax liabilities for this payroll run
            int quarter = GetQuarter(PayDate.DateTime);
            int taxYear = PayDate.Year;

            CreateTaxLiability(TaxType.Federal, taxYear, quarter, TotalFederalTax);
            CreateTaxLiability(TaxType.Ohio, taxYear, quarter, TotalStateTax);
            CreateTaxLiability(TaxType.Local, taxYear, quarter, TotalLocalTax);
            CreateTaxLiability(TaxType.SchoolDistrict, taxYear, quarter, TotalSchoolDistrictTax);
            CreateTaxLiability(TaxType.FICA_SS, taxYear, quarter, TotalSsTax + TotalEmployerSs);
            CreateTaxLiability(TaxType.FICA_Med, taxYear, quarter, TotalMedTax + TotalEmployerMed);
            CreateTaxLiability(TaxType.FUTA, taxYear, quarter, TotalEmployerFuta);
            CreateTaxLiability(TaxType.SUTA, taxYear, quarter, TotalEmployerSuta);

            // Update next check number in settings
            if (settings is not null)
            {
                settings.NextCheckNumber = nextCheckNumber;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // Audit log
            await _audit.LogAsync(
                "Finalized",
                "PayrollRun",
                run.Id,
                newValue: $"Employees: {EmployeeCount}, Gross: {TotalGrossPay:C}, Net: {TotalNetPay:C}");

            AppLogger.Information($"Payroll run #{run.Id} finalized: {EmployeeCount} employees, Gross: {TotalGrossPay:C}, Net: {TotalNetPay:C}");

            FinalizedRunId = run.Id;
            IsFinalized = true;
            FinalizedMessage = $"Payroll run #{run.Id} has been finalized successfully.\n\n" +
                $"Employees paid: {EmployeeCount}\n" +
                $"Total gross pay: {TotalGrossPay:C}\n" +
                $"Total net pay: {TotalNetPay:C}\n" +
                $"Check numbers: {nextCheckNumber - EmployeeCount} - {nextCheckNumber - 1}";

            CurrentStep = 4;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error finalizing payroll: {ex.Message}", ex);
            ErrorMessage = $"Error finalizing payroll: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CreateTaxLiability(TaxType taxType, int year, int quarter, decimal amount)
    {
        if (amount <= 0) return;

        _db.TaxLiabilities.Add(new TaxLiability
        {
            TaxType = taxType,
            TaxYear = year,
            Quarter = quarter,
            PeriodStart = PeriodStart.DateTime,
            PeriodEnd = PeriodEnd.DateTime,
            AmountOwed = amount,
            AmountPaid = 0m,
            Status = TaxLiabilityStatus.Unpaid,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static int GetQuarter(DateTime date) => (date.Month - 1) / 3 + 1;

    // ── Start New Run Command ────────────────────────────────────────────
    [RelayCommand]
    private void StartNewRun()
    {
        CurrentStep = 1;
        IsFinalized = false;
        ErrorMessage = null;
        FinalizedMessage = string.Empty;
        FinalizedRunId = 0;

        PeriodStart = DateTimeOffset.Now.AddDays(-13);
        PeriodEnd = DateTimeOffset.Now;
        PayDate = DateTimeOffset.Now.AddDays(3);

        EntryRows = new ObservableCollection<PayrollEntryRow>();
        PreviewRows = new ObservableCollection<PaycheckPreviewRow>();

        TotalGrossPay = 0;
        TotalNetPay = 0;
        TotalFederalTax = 0;
        TotalStateTax = 0;
        TotalLocalTax = 0;
        TotalSsTax = 0;
        TotalMedTax = 0;
        TotalSchoolDistrictTax = 0;
        TotalDeductions = 0;
        TotalEmployerSs = 0;
        TotalEmployerMed = 0;
        TotalEmployerFuta = 0;
        TotalEmployerSuta = 0;
        EmployeeCount = 0;
    }
}
