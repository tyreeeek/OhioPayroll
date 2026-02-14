using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;

    // ── Summary cards ───────────────────────────────────────────────
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private int _activeEmployeeCount;
    [ObservableProperty] private DateTime? _lastPayrollDate;
    [ObservableProperty] private decimal _lastPayrollGross;
    [ObservableProperty] private decimal _ytdGrossPay;
    [ObservableProperty] private decimal _ytdNetPay;
    [ObservableProperty] private decimal _ytdTotalTaxes;
    [ObservableProperty] private int _currentTaxYear;

    // ── Alerts ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _hasTaxTableAlert;
    [ObservableProperty] private string _taxTableAlertMessage = string.Empty;
    [ObservableProperty] private bool _hasTaxLiabilityAlert;
    [ObservableProperty] private string _taxLiabilityAlertMessage = string.Empty;

    // ── UI State ────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Formatted display values ────────────────────────────────────
    public string LastPayrollDateDisplay =>
        LastPayrollDate.HasValue ? LastPayrollDate.Value.ToString("MMM dd, yyyy") : "No payroll runs yet";

    public string LastPayrollGrossDisplay => LastPayrollGross.ToString("$#,##0.00");
    public string YtdGrossPayDisplay => YtdGrossPay.ToString("$#,##0.00");
    public string YtdNetPayDisplay => YtdNetPay.ToString("$#,##0.00");
    public string YtdTotalTaxesDisplay => YtdTotalTaxes.ToString("$#,##0.00");

    public DashboardViewModel(PayrollDbContext db)
    {
        _db = db;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading dashboard...";

            // Company name
            var company = await _db.CompanyInfo.FirstOrDefaultAsync();
            CompanyName = company?.CompanyName ?? "Ohio Payroll";

            // Active employees
            ActiveEmployeeCount = await _db.Employees.CountAsync(e => e.IsActive);

            // Tax year from settings
            var settings = await _db.PayrollSettings.FirstOrDefaultAsync();
            CurrentTaxYear = settings?.CurrentTaxYear ?? DateTime.Now.Year;

            // Last payroll run (finalized)
            var lastRun = await _db.PayrollRuns
                .Where(r => r.Status == PayrollRunStatus.Finalized)
                .OrderByDescending(r => r.PayDate)
                .FirstOrDefaultAsync();

            if (lastRun is not null)
            {
                LastPayrollDate = lastRun.PayDate;
                LastPayrollGross = lastRun.TotalGrossPay;
            }
            else
            {
                LastPayrollDate = null;
                LastPayrollGross = 0m;
            }

            // YTD totals (finalized runs in the current tax year)
            var ytdRuns = await _db.PayrollRuns
                .Where(r => r.Status == PayrollRunStatus.Finalized
                         && r.PayDate.Year == CurrentTaxYear)
                .ToListAsync();

            YtdGrossPay = ytdRuns.Sum(r => r.TotalGrossPay);
            YtdNetPay = ytdRuns.Sum(r => r.TotalNetPay);
            YtdTotalTaxes = ytdRuns.Sum(r =>
                r.TotalFederalTax + r.TotalStateTax + r.TotalLocalTax +
                r.TotalSocialSecurity + r.TotalMedicare);

            // Tax table alert
            var calendarYear = DateTime.Now.Year;
            if (CurrentTaxYear != calendarYear)
            {
                HasTaxTableAlert = true;
                TaxTableAlertMessage =
                    $"Tax tables are set to {CurrentTaxYear} but the current calendar year is {calendarYear}. Please verify your tax year setting.";
            }
            else
            {
                // Also check if we actually have tax tables for this year
                var hasTables = await _db.TaxTables.AnyAsync(t => t.TaxYear == calendarYear);
                if (!hasTables)
                {
                    HasTaxTableAlert = true;
                    TaxTableAlertMessage =
                        $"No tax tables found for {calendarYear}. Tax calculations may be inaccurate.";
                }
                else
                {
                    HasTaxTableAlert = false;
                    TaxTableAlertMessage = string.Empty;
                }
            }

            // Pending tax liabilities alert: unpaid liabilities with PeriodEnd more than 30 days ago
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var overdueLiabilities = await _db.TaxLiabilities
                .Where(t => t.Status == TaxLiabilityStatus.Unpaid
                         && t.PeriodEnd < thirtyDaysAgo)
                .ToListAsync();

            if (overdueLiabilities.Count > 0)
            {
                var totalOverdue = overdueLiabilities.Sum(t => t.AmountOwed - t.AmountPaid);
                HasTaxLiabilityAlert = true;
                TaxLiabilityAlertMessage =
                    $"There are {overdueLiabilities.Count} unpaid tax liabilities totaling {totalOverdue:C} " +
                    $"with period end dates more than 30 days ago. Please review and submit tax payments.";
            }
            else
            {
                HasTaxLiabilityAlert = false;
                TaxLiabilityAlertMessage = string.Empty;
            }

            // Notify formatted display properties
            OnPropertyChanged(nameof(LastPayrollDateDisplay));
            OnPropertyChanged(nameof(LastPayrollGrossDisplay));
            OnPropertyChanged(nameof(YtdGrossPayDisplay));
            OnPropertyChanged(nameof(YtdNetPayDisplay));
            OnPropertyChanged(nameof(YtdTotalTaxesDisplay));

            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

