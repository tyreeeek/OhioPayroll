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

    // ── Quarterly Tab ─────────────────────────────────────────────────
    [ObservableProperty] private int _selectedQuarterTab; // 0=YTD, 1=Q1, 2=Q2, 3=Q3, 4=Q4

    [ObservableProperty] private string _quarterLabel = "Year-to-Date";
    [ObservableProperty] private string _quarterGrossPayDisplay = "$0.00";
    [ObservableProperty] private string _quarterNetPayDisplay = "$0.00";
    [ObservableProperty] private string _quarterFederalTaxDisplay = "$0.00";
    [ObservableProperty] private string _quarterStateTaxDisplay = "$0.00";
    [ObservableProperty] private string _quarterSsTaxDisplay = "$0.00";
    [ObservableProperty] private string _quarterMedicareTaxDisplay = "$0.00";
    [ObservableProperty] private int _quarterPayrollRunCount;
    [ObservableProperty] private string _quarterTaxOwedDisplay = "$0.00";
    [ObservableProperty] private string _quarterTaxPaidDisplay = "$0.00";
    [ObservableProperty] private string _quarterTaxBalanceDisplay = "$0.00";
    [ObservableProperty] private bool _quarterHasBalance;
    [ObservableProperty] private string _quarterDueDateDisplay = "N/A";
    [ObservableProperty] private bool _quarterIsOverdue;
    [ObservableProperty] private bool _quarterIsSpecificQuarter; // true when Q1-Q4 selected (not YTD)

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

    [RelayCommand]
    private void SelectQuarter(string quarter)
    {
        SelectedQuarterTab = int.Parse(quarter);
    }

    partial void OnSelectedQuarterTabChanged(int value) => _ = LoadQuarterDataAsync();

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

            // Load quarterly breakdown
            await LoadQuarterDataAsync();

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

    private async Task LoadQuarterDataAsync()
    {
        try
        {
            int year = CurrentTaxYear > 0 ? CurrentTaxYear : DateTime.Now.Year;

            if (SelectedQuarterTab == 0)
            {
                // YTD view — reuse the YTD totals already loaded
                QuarterLabel = "Year-to-Date";
                QuarterIsSpecificQuarter = false;

                var ytdRuns = await _db.PayrollRuns
                    .Where(r => r.Status == PayrollRunStatus.Finalized
                        && r.PayDate.Year == year)
                    .ToListAsync();

                QuarterGrossPayDisplay = ytdRuns.Sum(r => r.TotalGrossPay).ToString("C");
                QuarterNetPayDisplay = ytdRuns.Sum(r => r.TotalNetPay).ToString("C");
                QuarterFederalTaxDisplay = ytdRuns.Sum(r => r.TotalFederalTax).ToString("C");
                QuarterStateTaxDisplay = ytdRuns.Sum(r => r.TotalStateTax).ToString("C");
                QuarterSsTaxDisplay = ytdRuns.Sum(r => r.TotalSocialSecurity).ToString("C");
                QuarterMedicareTaxDisplay = ytdRuns.Sum(r => r.TotalMedicare).ToString("C");
                QuarterPayrollRunCount = ytdRuns.Count;

                QuarterTaxOwedDisplay = "$0.00";
                QuarterTaxPaidDisplay = "$0.00";
                QuarterTaxBalanceDisplay = "$0.00";
                QuarterHasBalance = false;
                QuarterDueDateDisplay = "N/A";
                QuarterIsOverdue = false;
            }
            else
            {
                int quarter = SelectedQuarterTab;
                var (qStart, qEnd) = GetQuarterDates(year, quarter);
                QuarterLabel = $"Q{quarter} ({qStart:MMM} \u2013 {qEnd:MMM yyyy})";
                QuarterIsSpecificQuarter = true;

                var runs = await _db.PayrollRuns
                    .Where(r => r.Status == PayrollRunStatus.Finalized
                        && r.PayDate >= qStart && r.PayDate <= qEnd)
                    .ToListAsync();

                QuarterGrossPayDisplay = runs.Sum(r => r.TotalGrossPay).ToString("C");
                QuarterNetPayDisplay = runs.Sum(r => r.TotalNetPay).ToString("C");
                QuarterFederalTaxDisplay = runs.Sum(r => r.TotalFederalTax).ToString("C");
                QuarterStateTaxDisplay = runs.Sum(r => r.TotalStateTax).ToString("C");
                QuarterSsTaxDisplay = runs.Sum(r => r.TotalSocialSecurity).ToString("C");
                QuarterMedicareTaxDisplay = runs.Sum(r => r.TotalMedicare).ToString("C");
                QuarterPayrollRunCount = runs.Count;

                // Tax liability for this quarter
                var liabilities = await _db.TaxLiabilities
                    .Where(t => t.TaxYear == year && t.Quarter == quarter)
                    .ToListAsync();

                var owed = liabilities.Sum(t => t.AmountOwed);
                var paid = liabilities.Sum(t => t.AmountPaid);
                QuarterTaxOwedDisplay = owed.ToString("C");
                QuarterTaxPaidDisplay = paid.ToString("C");
                QuarterTaxBalanceDisplay = (owed - paid).ToString("C");
                QuarterHasBalance = owed - paid > 0;

                // 941 due dates
                QuarterDueDateDisplay = GetForm941DueDate(year, quarter);
                QuarterIsOverdue = QuarterHasBalance && DateTime.Now > GetForm941DueDateValue(year, quarter);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading quarter data: {ex.Message}";
        }
    }

    private static (DateTime start, DateTime end) GetQuarterDates(int year, int quarter) => quarter switch
    {
        1 => (new DateTime(year, 1, 1), new DateTime(year, 3, 31)),
        2 => (new DateTime(year, 4, 1), new DateTime(year, 6, 30)),
        3 => (new DateTime(year, 7, 1), new DateTime(year, 9, 30)),
        4 => (new DateTime(year, 10, 1), new DateTime(year, 12, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(quarter))
    };

    private static string GetForm941DueDate(int year, int quarter) => quarter switch
    {
        1 => $"April 30, {year}",
        2 => $"July 31, {year}",
        3 => $"October 31, {year}",
        4 => $"January 31, {year + 1}",
        _ => "N/A"
    };

    private static DateTime GetForm941DueDateValue(int year, int quarter) => quarter switch
    {
        1 => new DateTime(year, 4, 30),
        2 => new DateTime(year, 7, 31),
        3 => new DateTime(year, 10, 31),
        4 => new DateTime(year + 1, 1, 31),
        _ => DateTime.MaxValue
    };
}

