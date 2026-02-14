using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.ViewModels;

// --- Row ViewModel ---

public partial class TaxLiabilityRow : ObservableObject
{
    public int Id { get; set; }
    public TaxType TaxType { get; set; }
    public int TaxYear { get; set; }
    public int Quarter { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AmountOwed { get; set; }
    public decimal AmountPaid { get; set; }
    public TaxLiabilityStatus Status { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }

    public string TaxTypeDisplay => TaxType.ToString().Replace("_", " ");
    public string PeriodDisplay => $"{PeriodStart:MM/dd/yyyy} - {PeriodEnd:MM/dd/yyyy}";
    public decimal Balance => AmountOwed - AmountPaid;
    public string StatusDisplay => Status.ToString();
    public string PaymentDateDisplay => PaymentDate?.ToString("MM/dd/yyyy") ?? "";
    public string QuarterDisplay => $"Q{Quarter}";
}

// --- Main ViewModel ---

public partial class TaxLiabilityViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IAuditService _audit;

    // --- Section titles ---

    [ObservableProperty]
    private string _title = "Tax Liability";

    [ObservableProperty]
    private string _subtitle = "Track tax obligations and record payments.";

    // --- Collection ---

    [ObservableProperty]
    private ObservableCollection<TaxLiabilityRow> _liabilities = new();

    [ObservableProperty]
    private TaxLiabilityRow? _selectedLiability;

    // --- Filters ---

    [ObservableProperty]
    private int _filterYear;

    [ObservableProperty]
    private int _filterQuarter; // 0 = All

    [ObservableProperty]
    private string _filterStatus = "All"; // All, Unpaid, Paid

    [ObservableProperty]
    private ObservableCollection<int> _yearOptions = new();

    public int[] QuarterOptions { get; } = { 0, 1, 2, 3, 4 };
    public string[] StatusOptions { get; } = { "All", "Unpaid", "Paid" };

    // --- Summary cards ---

    [ObservableProperty]
    private string _totalOwedDisplay = "$0.00";

    [ObservableProperty]
    private string _totalPaidDisplay = "$0.00";

    [ObservableProperty]
    private string _balanceDueDisplay = "$0.00";

    // --- Payment dialog ---

    [ObservableProperty]
    private bool _isPaymentDialogOpen;

    [ObservableProperty]
    private decimal _paymentAmount;

    [ObservableProperty]
    private string _paymentReference = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string _paymentDialogTitle = "Record Payment";

    [ObservableProperty]
    private bool _isPartialPayment;

    public TaxLiabilityViewModel(PayrollDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;

        FilterYear = DateTime.Now.Year;

        _ = InitializeAsync();
    }

    partial void OnFilterYearChanged(int value) => _ = LoadLiabilitiesAsync();
    partial void OnFilterQuarterChanged(int value) => _ = LoadLiabilitiesAsync();
    partial void OnFilterStatusChanged(string value) => _ = LoadLiabilitiesAsync();

    private async Task InitializeAsync()
    {
        await LoadYearOptionsAsync();
        await LoadLiabilitiesAsync();
    }

    private async Task LoadYearOptionsAsync()
    {
        var years = await _db.TaxLiabilities
            .AsNoTracking()
            .Select(t => t.TaxYear)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        if (!years.Contains(DateTime.Now.Year))
            years.Insert(0, DateTime.Now.Year);

        years.Sort();
        years.Reverse();

        YearOptions = new ObservableCollection<int>(years);
    }

    [RelayCommand]
    private async Task LoadLiabilitiesAsync()
    {
        IQueryable<TaxLiability> query = _db.TaxLiabilities.AsNoTracking();

        if (FilterYear > 0)
            query = query.Where(t => t.TaxYear == FilterYear);

        if (FilterQuarter > 0)
            query = query.Where(t => t.Quarter == FilterQuarter);

        if (FilterStatus == "Unpaid")
            query = query.Where(t => t.Status != TaxLiabilityStatus.Paid);
        else if (FilterStatus == "Paid")
            query = query.Where(t => t.Status == TaxLiabilityStatus.Paid);

        var items = await query
            .OrderBy(t => t.TaxYear)
            .ThenBy(t => t.Quarter)
            .ThenBy(t => t.TaxType)
            .ToListAsync();

        var rows = items.Select(t => new TaxLiabilityRow
        {
            Id = t.Id,
            TaxType = t.TaxType,
            TaxYear = t.TaxYear,
            Quarter = t.Quarter,
            PeriodStart = t.PeriodStart,
            PeriodEnd = t.PeriodEnd,
            AmountOwed = t.AmountOwed,
            AmountPaid = t.AmountPaid,
            Status = t.Status,
            PaymentDate = t.PaymentDate,
            PaymentReference = t.PaymentReference
        }).ToList();

        Liabilities = new ObservableCollection<TaxLiabilityRow>(rows);

        // Update summary
        var totalOwed = rows.Sum(r => r.AmountOwed);
        var totalPaid = rows.Sum(r => r.AmountPaid);
        var balanceDue = totalOwed - totalPaid;

        TotalOwedDisplay = totalOwed.ToString("C");
        TotalPaidDisplay = totalPaid.ToString("C");
        BalanceDueDisplay = balanceDue.ToString("C");
    }

    [RelayCommand]
    private void MarkAsPaid()
    {
        if (SelectedLiability is null) return;
        if (SelectedLiability.Status == TaxLiabilityStatus.Paid) return;

        IsPartialPayment = false;
        PaymentDialogTitle = "Mark as Paid";
        PaymentAmount = SelectedLiability.AmountOwed - SelectedLiability.AmountPaid;
        PaymentReference = string.Empty;
        ValidationError = null;
        IsPaymentDialogOpen = true;
    }

    [RelayCommand]
    private void RecordPartialPayment()
    {
        if (SelectedLiability is null) return;
        if (SelectedLiability.Status == TaxLiabilityStatus.Paid) return;

        IsPartialPayment = true;
        PaymentDialogTitle = "Record Partial Payment";
        PaymentAmount = 0;
        PaymentReference = string.Empty;
        ValidationError = null;
        IsPaymentDialogOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmPaymentAsync()
    {
        if (SelectedLiability is null) return;

        if (PaymentAmount <= 0)
        {
            ValidationError = "Payment amount must be greater than zero.";
            return;
        }

        var remaining = SelectedLiability.AmountOwed - SelectedLiability.AmountPaid;
        if (PaymentAmount > remaining)
        {
            ValidationError = $"Payment amount cannot exceed the remaining balance of {remaining:C}.";
            return;
        }

        ValidationError = null;

        var liability = await _db.TaxLiabilities.FindAsync(SelectedLiability.Id);
        if (liability is null) return;

        var oldPaid = liability.AmountPaid;
        liability.AmountPaid += PaymentAmount;
        liability.PaymentDate = DateTime.UtcNow;
        liability.PaymentReference = string.IsNullOrWhiteSpace(PaymentReference)
            ? null
            : PaymentReference.Trim();
        liability.UpdatedAt = DateTime.UtcNow;

        if (liability.AmountPaid >= liability.AmountOwed)
            liability.Status = TaxLiabilityStatus.Paid;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Payment", "TaxLiability", liability.Id,
            oldValue: $"Paid: {oldPaid:C}",
            newValue: $"Paid: {liability.AmountPaid:C} | Ref: {liability.PaymentReference}");

        IsPaymentDialogOpen = false;
        await LoadLiabilitiesAsync();
    }

    [RelayCommand]
    private void CancelPayment()
    {
        IsPaymentDialogOpen = false;
        ValidationError = null;
    }
}
