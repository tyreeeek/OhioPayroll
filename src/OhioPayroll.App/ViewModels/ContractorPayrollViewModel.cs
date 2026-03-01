using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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

public partial class ContractorPayrollViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly ContractorPayrollService _payrollService;
    private readonly IEncryptionService _encryption;
    private readonly MainWindowViewModel _mainWindow;

    [ObservableProperty] private int _currentStep = 1;
    [ObservableProperty] private DateTime _periodStart = DateTime.Today.AddDays(-14);
    [ObservableProperty] private DateTime _periodEnd = DateTime.Today;
    [ObservableProperty] private DateTime _payDate = DateTime.Today.AddDays(3);
    [ObservableProperty] private PayFrequency _payFrequency = PayFrequency.BiWeekly;
    [ObservableProperty] private ObservableCollection<ContractorPaymentEntryRow> _paymentEntries = new();
    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private ContractorPayrollRun? _currentRun;

    public PayFrequency[] PayFrequencyValues { get; } = Enum.GetValues<PayFrequency>();
    public ContractorPaymentMethod[] PaymentMethodValues { get; } = Enum.GetValues<ContractorPaymentMethod>();

    public bool CanGoNext => CurrentStep < 4 && !IsProcessing;
    public bool CanGoBack => CurrentStep > 1 && !IsProcessing;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;

    public ContractorPayrollViewModel(
        PayrollDbContext db,
        ContractorPayrollService payrollService,
        IEncryptionService encryption,
        MainWindowViewModel mainWindow)
    {
        _db = db;
        _encryption = encryption;
        _payrollService = payrollService;
        _mainWindow = mainWindow;
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
    }

    partial void OnIsProcessingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
    }

    [RelayCommand]
    private async Task NextStep()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = string.Empty;

            if (CurrentStep == 1)
            {
                // Validate period dates
                if (PeriodEnd <= PeriodStart)
                {
                    StatusMessage = "Period end date must be after period start date";
                    IsProcessing = false;
                    return;
                }

                if (PayDate < PeriodEnd)
                {
                    StatusMessage = "Pay date must be after period end date";
                    IsProcessing = false;
                    return;
                }

                // Prevent far-future pay dates
                if (PayDate > DateTime.Today.AddDays(60))
                {
                    StatusMessage = "Pay date cannot be more than 60 days in the future";
                    IsProcessing = false;
                    return;
                }

                // Check if there's an existing draft with the same dates - if so, load and edit it
                var existingDraft = await _db.ContractorPayrollRuns
                    .Include(r => r.Payments)
                    .FirstOrDefaultAsync(r => r.Status == ContractorPayrollRunStatus.Draft
                        && r.PeriodStart.Date == PeriodStart.Date
                        && r.PeriodEnd.Date == PeriodEnd.Date);

                if (existingDraft != null)
                {
                    // Load existing draft for editing
                    CurrentRun = existingDraft;
                    StatusMessage = $"Editing existing draft payroll run #{CurrentRun.Id} from {CurrentRun.CreatedAt:g}";
                    AppLogger.Information($"Loading existing draft contractor payroll run #{CurrentRun.Id} for editing");
                }
                else
                {
                    // Create new draft payroll run
                    var result = await _payrollService.CreateDraftPayrollRunAsync(
                        PeriodStart, PeriodEnd, PayDate, PayFrequency);

                    if (!result.success)
                    {
                        StatusMessage = result.error;
                        IsProcessing = false;
                        return;
                    }

                    CurrentRun = result.run;
                    StatusMessage = $"Created new draft payroll run #{CurrentRun.Id}";
                }

                // Load active contractors with valid rates
                var contractors = await LoadContractorsForPayrollAsync();

                // If editing existing draft, pre-fill with existing payment data
                if (existingDraft != null && existingDraft.Payments.Any())
                {
                    var existingPayments = existingDraft.Payments.ToDictionary(p => p.ContractorId);

                    foreach (var contractor in contractors)
                    {
                        if (existingPayments.TryGetValue(contractor.ContractorId, out var payment))
                        {
                            // Pre-fill with existing data
                            contractor.HoursOrDays = payment.HoursWorked ?? payment.DaysWorked ?? 0;
                            contractor.PaymentMethod = payment.PaymentMethod;
                        }
                    }
                }

                PaymentEntries = new ObservableCollection<ContractorPaymentEntryRow>(contractors);

                if (!PaymentEntries.Any())
                {
                    // CRITICAL: Clean up orphaned draft if no contractors found
                    if (existingDraft == null && CurrentRun != null)
                    {
                        // This was a newly created draft - delete it to avoid orphaned runs
                        _db.ContractorPayrollRuns.Remove(CurrentRun);
                        await _db.SaveChangesAsync();
                        AppLogger.Information($"Deleted orphaned draft payroll run #{CurrentRun.Id} - no contractors found");
                        CurrentRun = null;
                    }

                    StatusMessage = "No active contractors with valid rates found for payroll processing";
                    CurrentStep = 1; // Go back to step 1
                    IsProcessing = false;
                    return;
                }
            }
            else if (CurrentStep == 2)
            {
                // Validate hours/days entry
                var invalidEntries = PaymentEntries.Where(e => e.HoursOrDays > 0 && !e.HasValidEntry).ToList();
                if (invalidEntries.Any())
                {
                    StatusMessage = "Please ensure all entries with hours/days are valid";
                    IsProcessing = false;
                    return;
                }

                // Filter to only contractors with hours/days entered
                var activeEntries = PaymentEntries.Where(e => e.HoursOrDays > 0).ToList();
                if (!activeEntries.Any())
                {
                    StatusMessage = "Please enter hours or days worked for at least one contractor";
                    IsProcessing = false;
                    return;
                }

                // Calculate preview
                TotalAmount = activeEntries.Sum(e => e.CalculatedAmount);
            }
            else if (CurrentStep == 3)
            {
                // Ready for finalization - no validation needed
            }

            CurrentStep++;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            AppLogger.Error($"Error in contractor payroll NextStep: {ex.Message}", ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task FinalizePayroll()
    {
        AppLogger.Information("FinalizePayroll button clicked - starting finalization process");

        try
        {
            IsProcessing = true;
            StatusMessage = "Finalizing payroll...";
            AppLogger.Information($"CurrentRun is {(CurrentRun == null ? "NULL" : $"ID: {CurrentRun.Id}")}");

            if (CurrentRun == null)
            {
                StatusMessage = "Error: No payroll run created";
                AppLogger.Error("FinalizePayroll failed: CurrentRun is null");
                IsProcessing = false;
                return;
            }

            // Delete any existing draft payments (in case we're editing an existing draft)
            var existingPayments = await _db.ContractorPayments
                .Where(p => p.ContractorPayrollRunId == CurrentRun.Id && !p.IsLocked && !p.IsDeleted)
                .ToListAsync();

            if (existingPayments.Any())
            {
                _db.ContractorPayments.RemoveRange(existingPayments);
                await _db.SaveChangesAsync();
                AppLogger.Information($"Removed {existingPayments.Count} existing draft payments before finalizing");
            }

            // Create fresh payments for contractors with hours/days entered
            var activeEntries = PaymentEntries.Where(e => e.HoursOrDays > 0).ToList();
            AppLogger.Information($"Creating payments for {activeEntries.Count} contractors with hours/days entered");

            if (!activeEntries.Any())
            {
                StatusMessage = "No contractors with hours/days entered. Cannot finalize empty payroll.";
                AppLogger.Warning("Finalization blocked: No active payment entries");
                IsProcessing = false;
                return;
            }

            var payments = new List<ContractorPayment>();

            foreach (var entry in activeEntries)
            {
                var payment = new ContractorPayment
                {
                    ContractorId = entry.ContractorId,
                    ContractorPayrollRunId = CurrentRun.Id,
                    PaymentDate = CurrentRun.PayDate,
                    Amount = entry.CalculatedAmount,
                    PaymentMethod = entry.PaymentMethod,
                    Description = $"Payroll: {CurrentRun.PeriodStart:d} to {CurrentRun.PeriodEnd:d}",
                    HoursWorked = entry.RateType == ContractorRateType.Hourly ? entry.HoursOrDays : null,
                    DaysWorked = entry.RateType == ContractorRateType.Daily ? entry.HoursOrDays : null,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Environment.UserName
                };

                _db.ContractorPayments.Add(payment);
                payments.Add(payment);
            }

            // Save payments first to assign IDs before passing to service
            await _db.SaveChangesAsync();

            // Update run total
            CurrentRun.TotalAmount = TotalAmount;

            // Finalize the payroll run - service handles its own transaction
            var result = await _payrollService.FinalizePayrollRunAsync(CurrentRun, payments);

            if (!result.success)
            {
                StatusMessage = $"Finalization failed: {result.error}";
                AppLogger.Error($"Payroll finalization failed: {result.error}");
                IsProcessing = false;
                return;
            }

            StatusMessage = "Payroll finalized successfully!";
            CurrentStep = 4;

            AppLogger.Information($"Contractor payroll finalized: {payments.Count} payments, Total: {TotalAmount:C}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error finalizing payroll: {ex.Message}";
            AppLogger.Error($"Error finalizing contractor payroll: {ex.Message}", ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _mainWindow.NavigateToCommand.Execute("Contractors");
    }

    [RelayCommand]
    private async Task GeneratePaystubs()
    {
        try
        {
            if (CurrentRun == null) return;

            IsProcessing = true;
            StatusMessage = "Generating paystubs...";

            // Load payments WITH tracking so we can update HasPaystub flag
            var payments = await _db.ContractorPayments
                .Where(p => p.ContractorPayrollRunId == CurrentRun.Id)
                .ToListAsync();

            if (!payments.Any())
            {
                StatusMessage = "No payments found to generate paystubs.";
                IsProcessing = false;
                return;
            }

            var paystubDoc = new Documents.ContractorPaystubDocument(_db);
            int successCount = 0;
            var errors = new List<string>();

            foreach (var payment in payments)
            {
                try
                {
                    // Get contractor first - fail early if not found
                    var contractor = await _db.Contractors.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == payment.ContractorId);

                    if (contractor == null)
                    {
                        errors.Add($"Payment {payment.Id}: Contractor not found");
                        AppLogger.Error($"Cannot generate paystub for payment {payment.Id}: Contractor {payment.ContractorId} not found");
                        continue;
                    }

                    // Generate PDF
                    var pdfBytes = paystubDoc.Generate(payment);

                    // Prepare file path
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var paystubsPath = Path.Combine(documentsPath, "OhioPayroll", "Contractor Paystubs");
                    Directory.CreateDirectory(paystubsPath);

                    var fileName = $"Paystub_{contractor.Name.Replace(" ", "_")}_{payment.PaymentDate:yyyyMMdd}.pdf";
                    var filePath = Path.Combine(paystubsPath, fileName);

                    // Write file FIRST - only set flag after successful write
                    await File.WriteAllBytesAsync(filePath, pdfBytes);

                    // CRITICAL: Only set flag after successful file write
                    payment.HasPaystub = true;
                    successCount++;
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Payment {payment.Id}: {ex.Message}";
                    errors.Add(errorMsg);
                    AppLogger.Error($"Error generating paystub for payment {payment.Id}: {ex.Message}", ex);
                }
            }

            // Save HasPaystub flags - save even if some failed
            if (successCount > 0 || errors.Any())
            {
                await _db.SaveChangesAsync();
            }

            if (errors.Any())
            {
                StatusMessage = $"Generated {successCount} paystub(s). {errors.Count} failed:\n{string.Join("\n", errors)}";
                AppLogger.Warning($"Generated {successCount} contractor paystubs with {errors.Count} errors for payroll run {CurrentRun.Id}");
            }
            else
            {
                StatusMessage = $"Generated {successCount} paystubs. Saved to Documents\\OhioPayroll\\Contractor Paystubs";
                AppLogger.Information($"Generated {successCount} contractor paystubs for payroll run {CurrentRun.Id}");
            }

            // Open folder
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OhioPayroll", "Contractor Paystubs");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating paystubs: {ex.Message}";
            AppLogger.Error($"Error generating contractor paystubs: {ex.Message}", ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task GenerateChecks()
    {
        try
        {
            if (CurrentRun == null) return;

            IsProcessing = true;
            StatusMessage = "Generating checks...";

            // CRITICAL: Remove AsNoTracking() so entity changes are tracked and persisted
            var checkEntries = await _db.CheckRegister
                .Include(e => e.ContractorPayment)
                // REMOVED: .AsNoTracking() - entities must be tracked to save HasCheck flag
                .Where(e => e.ContractorPaymentId != null &&
                           e.ContractorPayment!.ContractorPayrollRunId == CurrentRun.Id)
                .ToListAsync();

            if (!checkEntries.Any())
            {
                StatusMessage = "No checks found to generate. All contractors may be paid via ACH.";
                IsProcessing = false;
                return;
            }

            var checkDoc = new Documents.ContractorCheckDocument(_db, _encryption);
            int successCount = 0;
            var errors = new List<string>();

            foreach (var checkEntry in checkEntries)
            {
                try
                {
                    // Get contractor name for file
                    var contractor = await _db.Contractors.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == checkEntry.ContractorPayment!.ContractorId);

                    if (contractor == null)
                    {
                        errors.Add($"Check {checkEntry.CheckNumber}: Contractor not found");
                        AppLogger.Error($"Cannot generate check {checkEntry.CheckNumber}: Contractor {checkEntry.ContractorPayment!.ContractorId} not found");
                        continue;
                    }

                    // Generate check PDF
                    var pdfBytes = checkDoc.Generate(checkEntry.ContractorPayment!, checkEntry);

                    // Save to Documents folder
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var checksPath = Path.Combine(documentsPath, "OhioPayroll", "Contractor Checks");
                    Directory.CreateDirectory(checksPath);

                    var fileName = $"Check_{checkEntry.CheckNumber}_{contractor.Name.Replace(" ", "_")}.pdf";
                    var filePath = Path.Combine(checksPath, fileName);

                    // Write file FIRST
                    await File.WriteAllBytesAsync(filePath, pdfBytes);

                    // CRITICAL: Set flag only after successful write - now tracked and will be saved
                    checkEntry.ContractorPayment!.HasCheck = true;
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Check {checkEntry.CheckNumber}: {ex.Message}");
                    AppLogger.Error($"Error generating check {checkEntry.CheckNumber}: {ex.Message}", ex);
                }
            }

            // Save tracked changes to database
            await _db.SaveChangesAsync();

            if (errors.Any())
            {
                StatusMessage = $"Generated {successCount} check(s). {errors.Count} failed:\n{string.Join("\n", errors)}";
                AppLogger.Warning($"Generated {successCount} contractor checks with {errors.Count} errors for payroll run {CurrentRun.Id}");
            }
            else
            {
                StatusMessage = $"Generated {successCount} checks. Saved to Documents\\OhioPayroll\\Contractor Checks";
                AppLogger.Information($"Generated {successCount} contractor checks for payroll run {CurrentRun.Id}");
            }

            // Open folder
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OhioPayroll", "Contractor Checks");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating checks: {ex.Message}";
            AppLogger.Error($"Error generating contractor checks: {ex.Message}", ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task<List<ContractorPaymentEntryRow>> LoadContractorsForPayrollAsync()
    {
        var contractors = await _db.Contractors
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return contractors
            .Where(c => c.RateType != ContractorRateType.Flat) // Flat rate uses ad-hoc payments
            .Where(c => c.HasValidRate)
            .Select(c => new ContractorPaymentEntryRow
            {
                ContractorId = c.Id,
                ContractorName = c.Name,
                RateType = c.RateType,
                Rate = c.RateType == ContractorRateType.Hourly ? c.HourlyRate!.Value : c.DailyRate!.Value
                // PaymentMethod defaults to Check via the property initializer
            })
            .ToList();
    }
}

/// <summary>
/// Represents a single contractor's payment entry in the payroll run
/// </summary>
public partial class ContractorPaymentEntryRow : ObservableObject
{
    public int ContractorId { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public ContractorRateType RateType { get; set; }
    public decimal Rate { get; set; }

    [ObservableProperty] private ContractorPaymentMethod _paymentMethod = ContractorPaymentMethod.Check;
    [ObservableProperty] private decimal _hoursOrDays;

    private CancellationTokenSource? _recalcCts;

    public string RateTypeDisplay => RateType switch
    {
        ContractorRateType.Hourly => "Hourly",
        ContractorRateType.Daily => "Daily",
        _ => "Unknown"
    };

    public string RateDisplay => Rate.ToString("C2");

    public string UnitLabel => RateType switch
    {
        ContractorRateType.Hourly => "Hours",
        ContractorRateType.Daily => "Days",
        _ => "Units"
    };

    public decimal CalculatedAmount => RateType switch
    {
        ContractorRateType.Hourly => Rate * HoursOrDays,
        ContractorRateType.Daily => Rate * HoursOrDays,
        _ => 0
    };

    public bool HasValidEntry => HoursOrDays > 0 && CalculatedAmount > 0;

    // Debounce recalculations to avoid recalc on every keystroke
    partial void OnHoursOrDaysChanged(decimal value)
    {
        // Validate hours/days range
        if (value < 0)
        {
            AppLogger.Warning($"Negative hours/days rejected for contractor {ContractorId}: {value}");
            HoursOrDays = 0;
            return;
        }

        // Maximum 744 hours per month (31 days * 24 hours)
        if (value > 744)
        {
            AppLogger.Warning($"Excessive hours/days rejected for contractor {ContractorId}: {value}");
            HoursOrDays = 744;
            return;
        }

        _recalcCts?.Cancel();
        _recalcCts = new CancellationTokenSource();
        var token = _recalcCts.Token;

        DebouncedRecalculateAsync(token).FireAndForgetSafeAsync(errorContext: "recalculating contractor payment");
    }

    private async Task DebouncedRecalculateAsync(CancellationToken token)
    {
        await Task.Delay(300, token);
        if (!token.IsCancellationRequested)
        {
            OnPropertyChanged(nameof(CalculatedAmount));
            OnPropertyChanged(nameof(HasValidEntry));
        }
    }
}
