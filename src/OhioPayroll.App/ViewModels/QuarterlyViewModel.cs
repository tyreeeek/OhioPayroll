using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.ViewModels;

public class QuarterPayrollRunRow
{
    public int Id { get; set; }
    public DateTime PayDate { get; set; }
    public string PeriodDisplay { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal GrossPay { get; set; }
    public decimal NetPay { get; set; }
    public decimal TotalTaxes { get; set; }
    public string GrossPayDisplay => GrossPay.ToString("C");
    public string NetPayDisplay => NetPay.ToString("C");
    public string TotalTaxesDisplay => TotalTaxes.ToString("C");
    public string PayDateDisplay => PayDate.ToString("MM/dd/yyyy");
}

public class QuarterTaxSummaryRow
{
    public TaxType TaxType { get; set; }
    public decimal AmountOwed { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance => AmountOwed - AmountPaid;
    public string Status => Balance <= 0 ? "Paid" : "Unpaid";
    public string TaxTypeDisplay => TaxType.ToString().Replace("_", " ");
    public string AmountOwedDisplay => AmountOwed.ToString("C");
    public string AmountPaidDisplay => AmountPaid.ToString("C");
    public string BalanceDisplay => Balance.ToString("C");
}

public partial class QuarterlyViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;

    [ObservableProperty] private int _selectedYear;
    [ObservableProperty] private int _selectedQuarter = 1;
    [ObservableProperty] private ObservableCollection<int> _yearOptions = new();

    // ── Summary ───────────────────────────────────────────────────────
    [ObservableProperty] private string _quarterTitle = "Q1";
    [ObservableProperty] private string _periodDisplay = "";
    [ObservableProperty] private string _grossPayDisplay = "$0.00";
    [ObservableProperty] private string _netPayDisplay = "$0.00";
    [ObservableProperty] private string _federalTaxDisplay = "$0.00";
    [ObservableProperty] private string _stateTaxDisplay = "$0.00";
    [ObservableProperty] private string _localTaxDisplay = "$0.00";
    [ObservableProperty] private string _ssTaxDisplay = "$0.00";
    [ObservableProperty] private string _medicareTaxDisplay = "$0.00";
    [ObservableProperty] private string _employerTaxDisplay = "$0.00";
    [ObservableProperty] private int _payrollRunCount;
    [ObservableProperty] private int _employeeCount;

    // ── 941 Status ────────────────────────────────────────────────────
    [ObservableProperty] private string _form941DueDateDisplay = "N/A";
    [ObservableProperty] private bool _isOverdue;
    [ObservableProperty] private string _form941StatusDisplay = "Not Filed";

    // ── Tax Liability ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<QuarterTaxSummaryRow> _taxSummary = new();
    [ObservableProperty] private string _totalOwedDisplay = "$0.00";
    [ObservableProperty] private string _totalPaidDisplay = "$0.00";
    [ObservableProperty] private string _balanceDueDisplay = "$0.00";
    [ObservableProperty] private bool _hasBalance;

    // ── Payroll Runs ──────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<QuarterPayrollRunRow> _payrollRuns = new();

    // ── UI State ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Quarter options for ComboBox
    public int[] QuarterOptions { get; } = { 1, 2, 3, 4 };

    public QuarterlyViewModel(PayrollDbContext db)
    {
        _db = db;
        _selectedYear = DateTime.Now.Year;
        _selectedQuarter = GetCurrentQuarter();

        // Populate year options
        YearOptions = new ObservableCollection<int>(
            Enumerable.Range(DateTime.Now.Year - 2, 4));

        _ = LoadDataAsync();
    }

    partial void OnSelectedYearChanged(int value) => _ = LoadDataAsync();
    partial void OnSelectedQuarterChanged(int value) => _ = LoadDataAsync();

    private static int GetCurrentQuarter() => (DateTime.Now.Month - 1) / 3 + 1;

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading quarterly data...";

            var (qStart, qEnd) = GetQuarterDates(SelectedYear, SelectedQuarter);
            QuarterTitle = $"Q{SelectedQuarter} {SelectedYear}";
            PeriodDisplay = $"{qStart:MMMM d} \u2014 {qEnd:MMMM d, yyyy}";

            // Load payroll runs for this quarter
            var runs = await _db.PayrollRuns
                .Where(r => r.Status == PayrollRunStatus.Finalized
                    && r.PayDate >= qStart && r.PayDate <= qEnd)
                .OrderBy(r => r.PayDate)
                .ToListAsync();

            // Summary calculations
            GrossPayDisplay = runs.Sum(r => r.TotalGrossPay).ToString("C");
            NetPayDisplay = runs.Sum(r => r.TotalNetPay).ToString("C");
            FederalTaxDisplay = runs.Sum(r => r.TotalFederalTax).ToString("C");
            StateTaxDisplay = runs.Sum(r => r.TotalStateTax).ToString("C");
            LocalTaxDisplay = runs.Sum(r => r.TotalLocalTax).ToString("C");
            SsTaxDisplay = runs.Sum(r => r.TotalSocialSecurity).ToString("C");
            MedicareTaxDisplay = runs.Sum(r => r.TotalMedicare).ToString("C");

            var employerTaxes = runs.Sum(r =>
                r.TotalEmployerSocialSecurity + r.TotalEmployerMedicare +
                r.TotalEmployerFuta + r.TotalEmployerSuta);
            EmployerTaxDisplay = employerTaxes.ToString("C");

            PayrollRunCount = runs.Count;

            // Get unique employee count from paychecks in this quarter
            var paycheckEmployeeCount = await _db.Paychecks
                .Where(p => p.PayrollRun.Status == PayrollRunStatus.Finalized
                    && p.PayrollRun.PayDate >= qStart && p.PayrollRun.PayDate <= qEnd)
                .Select(p => p.EmployeeId)
                .Distinct()
                .CountAsync();
            EmployeeCount = paycheckEmployeeCount;

            // Payroll run rows (include paychecks for per-run employee counts)
            var runsWithChecks = await _db.PayrollRuns
                .Include(r => r.Paychecks)
                .Where(r => r.Status == PayrollRunStatus.Finalized
                    && r.PayDate >= qStart && r.PayDate <= qEnd)
                .OrderBy(r => r.PayDate)
                .ToListAsync();

            var runRows = runsWithChecks.Select(r => new QuarterPayrollRunRow
            {
                Id = r.Id,
                PayDate = r.PayDate,
                PeriodDisplay = $"{r.PeriodStart:MM/dd} - {r.PeriodEnd:MM/dd/yyyy}",
                EmployeeCount = r.Paychecks.Count,
                GrossPay = r.TotalGrossPay,
                NetPay = r.TotalNetPay,
                TotalTaxes = r.TotalFederalTax + r.TotalStateTax + r.TotalLocalTax +
                             r.TotalSocialSecurity + r.TotalMedicare
            }).ToList();

            PayrollRuns = new ObservableCollection<QuarterPayrollRunRow>(runRows);

            // Tax liability by type
            var liabilities = await _db.TaxLiabilities
                .Where(t => t.TaxYear == SelectedYear && t.Quarter == SelectedQuarter)
                .ToListAsync();

            var taxRows = liabilities
                .GroupBy(t => t.TaxType)
                .Select(g => new QuarterTaxSummaryRow
                {
                    TaxType = g.Key,
                    AmountOwed = g.Sum(t => t.AmountOwed),
                    AmountPaid = g.Sum(t => t.AmountPaid)
                })
                .OrderBy(r => r.TaxType)
                .ToList();

            TaxSummary = new ObservableCollection<QuarterTaxSummaryRow>(taxRows);

            var totalOwed = liabilities.Sum(t => t.AmountOwed);
            var totalPaid = liabilities.Sum(t => t.AmountPaid);
            TotalOwedDisplay = totalOwed.ToString("C");
            TotalPaidDisplay = totalPaid.ToString("C");
            BalanceDueDisplay = (totalOwed - totalPaid).ToString("C");
            HasBalance = totalOwed - totalPaid > 0;

            // 941 due date and overdue check
            Form941DueDateDisplay = GetForm941DueDate(SelectedYear, SelectedQuarter);
            var dueDate = GetForm941DueDateValue(SelectedYear, SelectedQuarter);
            IsOverdue = HasBalance && DateTime.Now > dueDate;

            // Determine filed status based on whether all liabilities are paid
            if (liabilities.Count == 0)
                Form941StatusDisplay = "No Liabilities";
            else if (liabilities.All(t => t.Status == TaxLiabilityStatus.Paid))
                Form941StatusDisplay = "All Paid";
            else if (liabilities.Any(t => t.AmountPaid > 0))
                Form941StatusDisplay = "Partially Paid";
            else
                Form941StatusDisplay = "Unpaid";

            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading quarterly data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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
