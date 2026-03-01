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

public partial class EmployeeRowViewModel : ObservableObject
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string SsnLast4 { get; set; } = string.Empty;
    public PayType PayType { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal AnnualSalary { get; set; }
    public bool IsActive { get; set; }
    public DateTime HireDate { get; set; }

    public string DisplayRate => PayType == PayType.Hourly
        ? $"{HourlyRate:C}/hr"
        : $"{AnnualSalary:C}/yr";

    public string MaskedSsn => string.IsNullOrEmpty(SsnLast4) ? "---" : $"***-**-{SsnLast4}";

    public string StatusText => IsActive ? "Active" : "Inactive";
}

public partial class EmployeeListViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    [ObservableProperty]
    private ObservableCollection<EmployeeRowViewModel> _employees = new();

    [ObservableProperty]
    private EmployeeRowViewModel? _selectedEmployee;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isNewEmployee;

    [ObservableProperty]
    private int _editingEmployeeId;

    [ObservableProperty]
    private bool _isConfirmingAction;

    [ObservableProperty]
    private string _confirmMessage = string.Empty;

    private Func<Task>? _pendingConfirmAction;

    // --- Form fields ---

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _ssn = string.Empty;

    [ObservableProperty]
    private PayType _payType;

    [ObservableProperty]
    private decimal _hourlyRate;

    [ObservableProperty]
    private decimal _annualSalary;

    [ObservableProperty]
    private FilingStatus _federalFilingStatus;

    [ObservableProperty]
    private FilingStatus _ohioFilingStatus;

    [ObservableProperty]
    private int _federalAllowances;

    [ObservableProperty]
    private int _ohioExemptions;

    [ObservableProperty]
    private string? _schoolDistrictCode;

    [ObservableProperty]
    private string? _municipalityCode;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _city = string.Empty;

    [ObservableProperty]
    private string _state = "OH";

    [ObservableProperty]
    private string _zipCode = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _hireDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string? _validationError;

    // --- Enum sources for ComboBoxes ---

    public PayType[] PayTypeValues { get; } = Enum.GetValues<PayType>();
    public FilingStatus[] FilingStatusValues { get; } = Enum.GetValues<FilingStatus>();

    // --- Computed properties ---

    public bool IsHourly => PayType == PayType.Hourly;
    public bool IsSalary => PayType == PayType.Salary;
    public string FormTitle => IsNewEmployee ? "Add Employee" : "Edit Employee";

    public EmployeeListViewModel(
        PayrollDbContext db,
        IEncryptionService encryption,
        IAuditService audit)
    {
        _db = db;
        _encryption = encryption;
        _audit = audit;

        ExecuteWithLoadingAsync(LoadEmployeesAsync, "Loading employees...")
            .FireAndForgetSafeAsync(errorContext: "loading employees");
    }

    partial void OnPayTypeChanged(PayType value)
    {
        OnPropertyChanged(nameof(IsHourly));
        OnPropertyChanged(nameof(IsSalary));
    }

    partial void OnIsNewEmployeeChanged(bool value)
    {
        OnPropertyChanged(nameof(FormTitle));
    }

    partial void OnSearchTextChanged(string value)
    {
        ExecuteWithLoadingAsync(LoadEmployeesAsync, "Searching employees...")
            .FireAndForgetSafeAsync(errorContext: "searching employees");
    }

    partial void OnShowInactiveChanged(bool value)
    {
        ExecuteWithLoadingAsync(LoadEmployeesAsync, "Loading employees...")
            .FireAndForgetSafeAsync(errorContext: "loading employees");
    }

    [RelayCommand]
    private async Task LoadEmployeesAsync()
    {
        IQueryable<Employee> query = _db.Employees.AsNoTracking();

        if (!ShowInactive)
            query = query.Where(e => e.IsActive);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLower();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(search) ||
                e.LastName.ToLower().Contains(search) ||
                e.SsnLast4.Contains(search));
        }

        var employees = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new EmployeeRowViewModel
            {
                Id = e.Id,
                FullName = e.FullName,
                SsnLast4 = e.SsnLast4,
                PayType = e.PayType,
                HourlyRate = e.HourlyRate,
                AnnualSalary = e.AnnualSalary,
                IsActive = e.IsActive,
                HireDate = e.HireDate
            })
            .ToListAsync();

        Employees = new ObservableCollection<EmployeeRowViewModel>(employees);
        ValidationError = null;
    }

    [RelayCommand]
    private void AddEmployee()
    {
        IsNewEmployee = true;
        EditingEmployeeId = 0;
        ClearForm();
        IsEditing = true;
    }

    [RelayCommand]
    private async Task EditEmployeeAsync(EmployeeRowViewModel? row)
    {
        try
        {
            var target = row ?? SelectedEmployee;
            if (target is null) return;

            var employee = await _db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == target.Id);

            if (employee is null)
            {
                ValidationError = "Employee not found. They may have been deleted.";
                return;
            }

            IsNewEmployee = false;
            EditingEmployeeId = employee.Id;

            FirstName = employee.FirstName;
            LastName = employee.LastName;
            Ssn = string.IsNullOrEmpty(employee.EncryptedSsn)
                ? string.Empty
                : _encryption.Decrypt(employee.EncryptedSsn);
            PayType = employee.PayType;
            HourlyRate = employee.HourlyRate;
            AnnualSalary = employee.AnnualSalary;
            FederalFilingStatus = employee.FederalFilingStatus;
            OhioFilingStatus = employee.OhioFilingStatus;
            FederalAllowances = employee.FederalAllowances;
            OhioExemptions = employee.OhioExemptions;
            SchoolDistrictCode = employee.SchoolDistrictCode;
            MunicipalityCode = employee.MunicipalityCode;
            Address = employee.Address;
            City = employee.City;
            State = employee.State;
            ZipCode = employee.ZipCode;
            HireDate = new DateTimeOffset(employee.HireDate);
            ValidationError = null;

            IsEditing = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error loading employee for edit: {ex.Message}", ex);
            ValidationError = $"Error loading employee: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveEmployeeAsync()
    {
        try
        {
            await SaveEmployeeInternalAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error saving employee: {ex.Message}", ex);
            ValidationError = $"Error saving employee: {ex.Message}";
        }
    }

    private async Task SaveEmployeeInternalAsync()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            ValidationError = "First name is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(LastName))
        {
            ValidationError = "Last name is required.";
            return;
        }
        var cleanSsnValidation = Ssn.Replace("-", "").Replace(" ", "");
        if (string.IsNullOrWhiteSpace(cleanSsnValidation) || cleanSsnValidation.Length != 9)
        {
            ValidationError = "SSN must be exactly 9 digits (dashes and spaces are allowed).";
            return;
        }
        if (!cleanSsnValidation.All(char.IsDigit))
        {
            ValidationError = "SSN must contain only digits (dashes and spaces are allowed as separators).";
            return;
        }
        if (cleanSsnValidation.Distinct().Count() == 1)
        {
            ValidationError = "SSN cannot be all the same digit.";
            return;
        }
        if (cleanSsnValidation == "000000000")
        {
            ValidationError = "SSN cannot be all zeros.";
            return;
        }
        {
            var area = cleanSsnValidation[..3];
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
        if (PayType == PayType.Hourly && HourlyRate <= 0)
        {
            ValidationError = "Hourly rate must be greater than zero.";
            return;
        }
        if (PayType == PayType.Salary && AnnualSalary <= 0)
        {
            ValidationError = "Annual salary must be greater than zero.";
            return;
        }

        ValidationError = null;

        var cleanSsn = Ssn.Replace("-", "").Replace(" ", "");
        var encryptedSsn = _encryption.Encrypt(cleanSsn);
        var ssnLast4 = cleanSsn[^4..];

        // Check for duplicate SSN (only among active employees)
        var possibleDuplicates = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive && e.SsnLast4 == ssnLast4 && e.Id != EditingEmployeeId)
            .ToListAsync();

        foreach (var dup in possibleDuplicates)
        {
            try
            {
                var existingSsn = _encryption.Decrypt(dup.EncryptedSsn).Replace("-", "").Replace(" ", "");
                if (existingSsn == cleanSsn)
                {
                    ValidationError = $"An employee with this SSN already exists: {dup.FullName}.";
                    return;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Failed to decrypt SSN for employee {dup.Id} ({dup.FullName}): {ex.Message}");
            }
        }

        if (IsNewEmployee)
        {
            var employee = new Employee
            {
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                EncryptedSsn = encryptedSsn,
                SsnLast4 = ssnLast4,
                PayType = PayType,
                HourlyRate = HourlyRate,
                AnnualSalary = AnnualSalary,
                FederalFilingStatus = FederalFilingStatus,
                OhioFilingStatus = OhioFilingStatus,
                FederalAllowances = FederalAllowances,
                OhioExemptions = OhioExemptions,
                SchoolDistrictCode = SchoolDistrictCode,
                MunicipalityCode = MunicipalityCode,
                Address = Address.Trim(),
                City = City.Trim(),
                State = State.Trim(),
                ZipCode = ZipCode.Trim(),
                HireDate = HireDate.DateTime,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            AppLogger.Information($"Employee created: {employee.FullName} (ID: {employee.Id})");
            await _audit.LogAsync("Created", "Employee", employee.Id,
                newValue: $"{employee.FullName}");
        }
        else
        {
            var employee = await _db.Employees.FindAsync(EditingEmployeeId);
            if (employee is null) return;

            var oldValue = $"{employee.FullName} | {employee.PayType}";

            employee.FirstName = FirstName.Trim();
            employee.LastName = LastName.Trim();
            employee.EncryptedSsn = encryptedSsn;
            employee.SsnLast4 = ssnLast4;
            employee.PayType = PayType;
            employee.HourlyRate = HourlyRate;
            employee.AnnualSalary = AnnualSalary;
            employee.FederalFilingStatus = FederalFilingStatus;
            employee.OhioFilingStatus = OhioFilingStatus;
            employee.FederalAllowances = FederalAllowances;
            employee.OhioExemptions = OhioExemptions;
            employee.SchoolDistrictCode = SchoolDistrictCode;
            employee.MunicipalityCode = MunicipalityCode;
            employee.Address = Address.Trim();
            employee.City = City.Trim();
            employee.State = State.Trim();
            employee.ZipCode = ZipCode.Trim();
            employee.HireDate = HireDate.DateTime;
            employee.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            AppLogger.Information($"Employee updated: {employee.FullName} (ID: {employee.Id})");
            await _audit.LogAsync("Updated", "Employee", employee.Id,
                oldValue: oldValue,
                newValue: $"{employee.FullName} | {employee.PayType}");
        }

        IsEditing = false;
        await LoadEmployeesAsync();
    }

    [RelayCommand]
    private void RequestDeactivation(EmployeeRowViewModel? row)
    {
        var target = row ?? SelectedEmployee;
        if (target is null) return;

        ConfirmMessage = $"Deactivate employee {target.FullName}? This will mark them as terminated.";
        _pendingConfirmAction = async () =>
        {
            var employee = await _db.Employees.FindAsync(target.Id);
            if (employee is null) return;

            employee.IsActive = false;
            employee.TerminationDate = DateTime.UtcNow;
            employee.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            AppLogger.Information($"Employee deactivated: {employee.FullName} (ID: {employee.Id})");
            await _audit.LogAsync("Deactivated", "Employee", employee.Id,
                oldValue: "Active",
                newValue: "Inactive");

            await LoadEmployeesAsync();
        };
        IsConfirmingAction = true;
    }

    [RelayCommand]
    private void RequestActivation(EmployeeRowViewModel? row)
    {
        var target = row ?? SelectedEmployee;
        if (target is null) return;

        ConfirmMessage = $"Activate employee {target.FullName}?";
        _pendingConfirmAction = async () =>
        {
            var employee = await _db.Employees.FindAsync(target.Id);
            if (employee is null) return;

            employee.IsActive = true;
            employee.TerminationDate = null;
            employee.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            AppLogger.Information($"Employee activated: {employee.FullName} (ID: {employee.Id})");
            await _audit.LogAsync("Activated", "Employee", employee.Id,
                oldValue: "Inactive",
                newValue: "Active");

            await LoadEmployeesAsync();
        };
        IsConfirmingAction = true;
    }

    [RelayCommand]
    private void RequestDelete(EmployeeRowViewModel? row)
    {
        var target = row ?? SelectedEmployee;
        if (target is null) return;

        ConfirmMessage = $"Permanently delete employee {target.FullName}? This action cannot be undone.";
        _pendingConfirmAction = async () =>
        {
            var employee = await _db.Employees
                .Include(e => e.Paychecks)
                .FirstOrDefaultAsync(e => e.Id == target.Id);

            if (employee is null) return;

            // Check if employee has any paychecks
            if (employee.Paychecks.Any())
            {
                ValidationError = "Cannot delete employee with existing paychecks. Deactivate instead.";
                return;
            }

            _db.Employees.Remove(employee);
            await _db.SaveChangesAsync();

            AppLogger.Information($"Employee deleted: {employee.FullName} (ID: {employee.Id})");
            await _audit.LogAsync("Deleted", "Employee", employee.Id,
                oldValue: $"{employee.FullName}");

            await LoadEmployeesAsync();
        };
        IsConfirmingAction = true;
    }

    [RelayCommand]
    private async Task ConfirmActionAsync()
    {
        IsConfirmingAction = false;
        ConfirmMessage = string.Empty;
        var action = _pendingConfirmAction;
        _pendingConfirmAction = null;

        if (action != null)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error executing confirmed action: {ex.Message}", ex);
                ValidationError = $"Error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void CancelConfirmation()
    {
        IsConfirmingAction = false;
        ConfirmMessage = string.Empty;
        _pendingConfirmAction = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ValidationError = null;
    }

    private void ClearForm()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Ssn = string.Empty;
        PayType = PayType.Hourly;
        HourlyRate = 0;
        AnnualSalary = 0;
        FederalFilingStatus = FilingStatus.Single;
        OhioFilingStatus = FilingStatus.Single;
        FederalAllowances = 0;
        OhioExemptions = 0;
        SchoolDistrictCode = null;
        MunicipalityCode = null;
        Address = string.Empty;
        City = string.Empty;
        State = "OH";
        ZipCode = string.Empty;
        HireDate = DateTimeOffset.Now;
        ValidationError = null;
    }
}

