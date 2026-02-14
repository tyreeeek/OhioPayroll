using System;
using System.Collections.ObjectModel;
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

public partial class ContractorRowViewModel : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string TinLast4 { get; set; } = string.Empty;
    public ContractorBusinessType BusinessType { get; set; }
    public bool IsEin { get; set; }
    public bool IsActive { get; set; }
    public decimal YtdPayments { get; set; }

    public string MaskedTin => string.IsNullOrEmpty(TinLast4)
        ? "---"
        : IsEin
            ? $"**-***{TinLast4}"
            : $"***-**-{TinLast4}";

    public string StatusText => IsActive ? "Active" : "Inactive";

    public string YtdPaymentsDisplay => YtdPayments.ToString("C");

    public string BusinessTypeDisplay => BusinessType switch
    {
        ContractorBusinessType.Individual => "Individual",
        ContractorBusinessType.SoleProprietor => "Sole Proprietor",
        ContractorBusinessType.SingleMemberLlc => "Single-Member LLC",
        ContractorBusinessType.CCorporation => "C Corporation",
        ContractorBusinessType.SCorporation => "S Corporation",
        ContractorBusinessType.Partnership => "Partnership",
        ContractorBusinessType.TrustEstate => "Trust/Estate",
        ContractorBusinessType.LlcC => "LLC (C)",
        ContractorBusinessType.LlcS => "LLC (S)",
        ContractorBusinessType.LlcP => "LLC (P)",
        ContractorBusinessType.Other => "Other",
        _ => BusinessType.ToString()
    };
}

public class ContractorPaymentRow
{
    public int Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public ContractorPaymentMethod PaymentMethod { get; set; }
    public string? CheckNumber { get; set; }
    public string? Reference { get; set; }

    public string AmountDisplay => Amount.ToString("C");
    public string DateDisplay => PaymentDate.ToString("MM/dd/yyyy");
}

public partial class ContractorListViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    [ObservableProperty]
    private ObservableCollection<ContractorRowViewModel> _contractors = new();

    [ObservableProperty]
    private ContractorRowViewModel? _selectedContractor;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isNewContractor;

    [ObservableProperty]
    private int _editingContractorId;

    [ObservableProperty]
    private bool _isViewingPayments;

    // --- Form fields ---

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _businessName = string.Empty;

    [ObservableProperty]
    private string _tin = string.Empty;

    [ObservableProperty]
    private bool _isEin;

    [ObservableProperty]
    private ContractorBusinessType _businessType;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _city = string.Empty;

    [ObservableProperty]
    private string _state = "OH";

    [ObservableProperty]
    private string _zipCode = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private bool _is1099Exempt;

    [ObservableProperty]
    private string? _validationError;

    // --- Payment view properties ---

    [ObservableProperty]
    private ObservableCollection<ContractorPaymentRow> _payments = new();

    [ObservableProperty]
    private string _viewingContractorName = string.Empty;

    [ObservableProperty]
    private bool _isAddingPayment;

    [ObservableProperty]
    private DateTimeOffset _paymentDate = DateTimeOffset.Now;

    [ObservableProperty]
    private decimal _paymentAmount;

    [ObservableProperty]
    private string _paymentDescription = string.Empty;

    [ObservableProperty]
    private ContractorPaymentMethod _paymentMethod;

    [ObservableProperty]
    private string _paymentCheckNumber = string.Empty;

    [ObservableProperty]
    private string _paymentReference = string.Empty;

    [ObservableProperty]
    private string? _paymentValidationError;

    [ObservableProperty]
    private string _paymentsTotalDisplay = 0m.ToString("C");

    private int _viewingContractorId;

    // --- Enum sources for ComboBoxes ---

    public ContractorBusinessType[] BusinessTypeValues { get; } = Enum.GetValues<ContractorBusinessType>();
    public ContractorPaymentMethod[] PaymentMethodValues { get; } = Enum.GetValues<ContractorPaymentMethod>();

    // --- Computed properties ---

    public string FormTitle => IsNewContractor ? "Add Contractor" : "Edit Contractor";

    public ContractorListViewModel(
        PayrollDbContext db,
        IEncryptionService encryption,
        IAuditService audit)
    {
        _db = db;
        _encryption = encryption;
        _audit = audit;

        _ = LoadContractorsAsync();
    }

    partial void OnIsNewContractorChanged(bool value)
    {
        OnPropertyChanged(nameof(FormTitle));
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = LoadContractorsAsync();
    }

    partial void OnShowInactiveChanged(bool value)
    {
        _ = LoadContractorsAsync();
    }

    [RelayCommand]
    private async Task LoadContractorsAsync()
    {
        IQueryable<Contractor> query = _db.Contractors.AsNoTracking();

        if (!ShowInactive)
            query = query.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (c.BusinessName != null && c.BusinessName.ToLower().Contains(search)) ||
                c.TinLast4.Contains(search));
        }

        var currentYear = DateTime.Now.Year;

        var contractors = await query
            .OrderBy(c => c.Name)
            .Select(c => new ContractorRowViewModel
            {
                Id = c.Id,
                Name = c.Name,
                BusinessName = c.BusinessName,
                TinLast4 = c.TinLast4,
                BusinessType = c.BusinessType,
                IsEin = c.IsEin,
                IsActive = c.IsActive,
                YtdPayments = c.Payments
                    .Where(p => p.TaxYear == currentYear)
                    .Sum(p => p.Amount)
            })
            .ToListAsync();

        Contractors = new ObservableCollection<ContractorRowViewModel>(contractors);
    }

    [RelayCommand]
    private void AddContractor()
    {
        IsNewContractor = true;
        EditingContractorId = 0;
        ClearForm();
        IsEditing = true;
    }

    [RelayCommand]
    private async Task EditContractorAsync(ContractorRowViewModel? row)
    {
        var target = row ?? SelectedContractor;
        if (target is null) return;

        var contractor = await _db.Contractors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == target.Id);

        if (contractor is null) return;

        IsNewContractor = false;
        EditingContractorId = contractor.Id;

        Name = contractor.Name;
        BusinessName = contractor.BusinessName ?? string.Empty;
        Tin = string.IsNullOrEmpty(contractor.EncryptedTin)
            ? string.Empty
            : _encryption.Decrypt(contractor.EncryptedTin);
        IsEin = contractor.IsEin;
        BusinessType = contractor.BusinessType;
        Address = contractor.Address;
        City = contractor.City;
        State = contractor.State;
        ZipCode = contractor.ZipCode;
        Email = contractor.Email ?? string.Empty;
        Phone = contractor.Phone ?? string.Empty;
        Is1099Exempt = contractor.Is1099Exempt;
        ValidationError = null;

        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveContractorAsync()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Contractor name is required.";
            return;
        }

        var cleanTin = Tin.Replace("-", "").Replace(" ", "");
        if (string.IsNullOrWhiteSpace(cleanTin) || cleanTin.Length != 9)
        {
            ValidationError = "TIN must be exactly 9 digits (dashes and spaces are allowed).";
            return;
        }
        if (!cleanTin.All(char.IsDigit))
        {
            ValidationError = "TIN must contain only digits (dashes and spaces are allowed as separators).";
            return;
        }

        if (IsEin)
        {
            // EIN validation: first two digits cannot be 00
            var prefix = cleanTin[..2];
            if (prefix == "00")
            {
                ValidationError = "EIN prefix (first 2 digits) cannot be 00.";
                return;
            }
        }
        else
        {
            // SSN validation (same as employee)
            if (cleanTin.Distinct().Count() == 1)
            {
                ValidationError = "SSN cannot be all the same digit.";
                return;
            }
            if (cleanTin == "000000000")
            {
                ValidationError = "SSN cannot be all zeros.";
                return;
            }
            var area = cleanTin[..3];
            var areaNum = int.Parse(area);
            if (area == "000")
            {
                ValidationError = "SSN area number (first 3 digits) cannot be 000.";
                return;
            }
            if (area == "666")
            {
                ValidationError = "SSN area number (first 3 digits) cannot be 666.";
                return;
            }
            if (areaNum >= 900 && areaNum <= 999)
            {
                ValidationError = "SSN area number (first 3 digits) cannot be in the range 900-999.";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            ValidationError = "Address is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(City))
        {
            ValidationError = "City is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ZipCode))
        {
            ValidationError = "ZIP code is required.";
            return;
        }

        ValidationError = null;

        var encryptedTin = _encryption.Encrypt(cleanTin);
        var tinLast4 = cleanTin[^4..];

        if (IsNewContractor)
        {
            var contractor = new Contractor
            {
                Name = Name.Trim(),
                BusinessName = string.IsNullOrWhiteSpace(BusinessName) ? null : BusinessName.Trim(),
                EncryptedTin = encryptedTin,
                TinLast4 = tinLast4,
                IsEin = IsEin,
                BusinessType = BusinessType,
                Address = Address.Trim(),
                City = City.Trim(),
                State = State.Trim(),
                ZipCode = ZipCode.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                Is1099Exempt = Is1099Exempt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Contractors.Add(contractor);
            await _db.SaveChangesAsync();

            AppLogger.Information($"Contractor created: {contractor.Name} (ID: {contractor.Id})");
            await _audit.LogAsync("Created", "Contractor", contractor.Id,
                newValue: $"{contractor.Name}");
        }
        else
        {
            var contractor = await _db.Contractors.FindAsync(EditingContractorId);
            if (contractor is null) return;

            var oldValue = $"{contractor.Name} | {contractor.BusinessType}";

            contractor.Name = Name.Trim();
            contractor.BusinessName = string.IsNullOrWhiteSpace(BusinessName) ? null : BusinessName.Trim();
            contractor.EncryptedTin = encryptedTin;
            contractor.TinLast4 = tinLast4;
            contractor.IsEin = IsEin;
            contractor.BusinessType = BusinessType;
            contractor.Address = Address.Trim();
            contractor.City = City.Trim();
            contractor.State = State.Trim();
            contractor.ZipCode = ZipCode.Trim();
            contractor.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
            contractor.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
            contractor.Is1099Exempt = Is1099Exempt;
            contractor.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            AppLogger.Information($"Contractor updated: {contractor.Name} (ID: {contractor.Id})");
            await _audit.LogAsync("Updated", "Contractor", contractor.Id,
                oldValue: oldValue,
                newValue: $"{contractor.Name} | {contractor.BusinessType}");
        }

        IsEditing = false;
        await LoadContractorsAsync();
    }

    [RelayCommand]
    private async Task DeactivateContractorAsync(ContractorRowViewModel? row)
    {
        var target = row ?? SelectedContractor;
        if (target is null) return;

        var contractor = await _db.Contractors.FindAsync(target.Id);
        if (contractor is null) return;

        contractor.IsActive = false;
        contractor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        AppLogger.Information($"Contractor deactivated: {contractor.Name} (ID: {contractor.Id})");
        await _audit.LogAsync("Deactivated", "Contractor", contractor.Id,
            oldValue: "Active",
            newValue: "Inactive");

        await LoadContractorsAsync();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ValidationError = null;
    }

    [RelayCommand]
    private async Task ViewPaymentsAsync(ContractorRowViewModel? row)
    {
        var target = row ?? SelectedContractor;
        if (target is null) return;

        _viewingContractorId = target.Id;
        ViewingContractorName = target.Name;
        IsAddingPayment = false;
        PaymentValidationError = null;

        await LoadPaymentsAsync();

        IsViewingPayments = true;
    }

    [RelayCommand]
    private void BackToList()
    {
        IsViewingPayments = false;
        IsAddingPayment = false;
        PaymentValidationError = null;
    }

    [RelayCommand]
    private void AddPayment()
    {
        ClearPaymentForm();
        IsAddingPayment = true;
    }

    [RelayCommand]
    private async Task SavePaymentAsync()
    {
        if (PaymentAmount <= 0)
        {
            PaymentValidationError = "Payment amount must be greater than zero.";
            return;
        }
        if (string.IsNullOrWhiteSpace(PaymentDescription))
        {
            PaymentValidationError = "Payment description is required.";
            return;
        }

        PaymentValidationError = null;

        var payment = new ContractorPayment
        {
            ContractorId = _viewingContractorId,
            PaymentDate = PaymentDate.DateTime,
            Amount = PaymentAmount,
            Description = PaymentDescription.Trim(),
            PaymentMethod = PaymentMethod,
            CheckNumber = string.IsNullOrWhiteSpace(PaymentCheckNumber) ? null : PaymentCheckNumber.Trim(),
            Reference = string.IsNullOrWhiteSpace(PaymentReference) ? null : PaymentReference.Trim(),
            TaxYear = PaymentDate.Year,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ContractorPayments.Add(payment);
        await _db.SaveChangesAsync();

        AppLogger.Information($"Contractor payment created: {payment.Amount:C} for contractor ID {_viewingContractorId}");
        await _audit.LogAsync("Created", "ContractorPayment", payment.Id,
            newValue: $"{payment.Amount:C} on {payment.PaymentDate:MM/dd/yyyy}");

        IsAddingPayment = false;
        await LoadPaymentsAsync();
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(ContractorPaymentRow? payment)
    {
        if (payment is null) return;

        var entity = await _db.ContractorPayments.FindAsync(payment.Id);
        if (entity is null) return;

        _db.ContractorPayments.Remove(entity);
        await _db.SaveChangesAsync();

        AppLogger.Information($"Contractor payment deleted: {entity.Amount:C} (ID: {entity.Id})");
        await _audit.LogAsync("Deleted", "ContractorPayment", entity.Id,
            oldValue: $"{entity.Amount:C} on {entity.PaymentDate:MM/dd/yyyy}");

        await LoadPaymentsAsync();
    }

    [RelayCommand]
    private void CancelPayment()
    {
        IsAddingPayment = false;
        PaymentValidationError = null;
    }

    private async Task LoadPaymentsAsync()
    {
        var payments = await _db.ContractorPayments
            .AsNoTracking()
            .Where(p => p.ContractorId == _viewingContractorId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new ContractorPaymentRow
            {
                Id = p.Id,
                PaymentDate = p.PaymentDate,
                Amount = p.Amount,
                Description = p.Description,
                PaymentMethod = p.PaymentMethod,
                CheckNumber = p.CheckNumber,
                Reference = p.Reference
            })
            .ToListAsync();

        Payments = new ObservableCollection<ContractorPaymentRow>(payments);

        var currentYear = DateTime.Now.Year;
        var ytdTotal = await _db.ContractorPayments
            .AsNoTracking()
            .Where(p => p.ContractorId == _viewingContractorId && p.TaxYear == currentYear)
            .SumAsync(p => p.Amount);

        PaymentsTotalDisplay = ytdTotal.ToString("C");
    }

    private void ClearForm()
    {
        Name = string.Empty;
        BusinessName = string.Empty;
        Tin = string.Empty;
        IsEin = false;
        BusinessType = ContractorBusinessType.Individual;
        Address = string.Empty;
        City = string.Empty;
        State = "OH";
        ZipCode = string.Empty;
        Email = string.Empty;
        Phone = string.Empty;
        Is1099Exempt = false;
        ValidationError = null;
    }

    private void ClearPaymentForm()
    {
        PaymentDate = DateTimeOffset.Now;
        PaymentAmount = 0;
        PaymentDescription = string.Empty;
        PaymentMethod = ContractorPaymentMethod.Check;
        PaymentCheckNumber = string.Empty;
        PaymentReference = string.Empty;
        PaymentValidationError = null;
    }
}
