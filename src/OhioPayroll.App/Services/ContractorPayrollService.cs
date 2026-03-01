using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.Services;

/// <summary>
/// Handles contractor payroll run operations with transactional integrity,
/// immutability enforcement, and complete audit trails.
/// </summary>
public class ContractorPayrollService
{
    private readonly PayrollDbContext _db;

    public ContractorPayrollService(PayrollDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Finalizes a contractor payroll run with full transactional integrity.
    /// Creates snapshots, locks payments, generates check register entries.
    /// </summary>
    public async Task<(bool success, string error)> FinalizePayrollRunAsync(
        ContractorPayrollRun run,
        List<ContractorPayment> payments)
    {
        // Create correlation context for this contractor payroll operation
        using var correlationContext = new PayrollCorrelationContext();

        AppLogger.Information($"Starting contractor payroll finalization: Period={run.PeriodStart:d} to {run.PeriodEnd:d}, " +
            $"PayDate={run.PayDate:d}, Contractors={payments.Count}, EstimatedTotal={run.TotalAmount:C}");

        // Optimistic concurrency: Retry up to 3 times on conflict
        const int maxRetries = 3;
        int attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    // 1. Validate period overlap (DATABASE-LEVEL CHECK)
                    var overlap = await _db.ContractorPayrollRuns
                        .AnyAsync(r => r.Status == ContractorPayrollRunStatus.Finalized &&
                                       r.Id != run.Id &&
                                       r.PeriodStart <= run.PeriodEnd &&
                                       r.PeriodEnd >= run.PeriodStart);

                    if (overlap)
                        return (false, "Period overlaps with existing finalized payroll run");

                    // 2. Validate lock date (if configured)
                    var settings = await _db.PayrollSettings.FirstOrDefaultAsync();
                    if (settings == null)
                        return (false, "Payroll settings not initialized. Configure in Settings first.");

                    if (settings.ContractorPayrollLockDate.HasValue &&
                        run.PeriodEnd < settings.ContractorPayrollLockDate.Value)
                    {
                        return (false, $"Cannot finalize payroll before lock date {settings.ContractorPayrollLockDate.Value:d}");
                    }

                    // 3. Set finalization metadata
                    run.Status = ContractorPayrollRunStatus.Finalized;
                    run.FinalizedAt = DateTime.UtcNow;
                    run.FinalizedBy = Environment.UserName;

                    // 4. Lock all payments and snapshot data (IMMUTABLE)
                    foreach (var payment in payments)
                    {
                        payment.IsLocked = true;
                        payment.PaymentType = ContractorPaymentType.BatchPayroll;

                        // Snapshot contractor data for historical accuracy
                        var contractor = await _db.Contractors.FindAsync(payment.ContractorId);
                        if (contractor == null)
                            return (false, $"Contractor with ID {payment.ContractorId} not found");

                        payment.ContractorNameSnapshot = contractor.Name;
                        payment.RateTypeAtPayment = contractor.RateType;
                        payment.RateAtPayment = contractor.RateType switch
                        {
                            ContractorRateType.Hourly => contractor.HourlyRate,
                            ContractorRateType.Daily => contractor.DailyRate,
                            _ => null
                        };
                    }

                    // 5. Create check register entries (with XOR constraint validation)
                    int checkNumber = settings.NextCheckNumber;
                    foreach (var payment in payments.Where(p => p.PaymentMethod == ContractorPaymentMethod.Check))
                    {
                        var checkEntry = new CheckRegisterEntry
                        {
                            CheckNumber = checkNumber,
                            ContractorPaymentId = payment.Id,
                            PaycheckId = null,  // XOR: exactly one must be set
                            Amount = payment.Amount,
                            Status = CheckStatus.Issued,
                            IssuedDate = run.PayDate,
                            CreatedAt = DateTime.UtcNow
                        };

                        // Validate XOR constraint
                        ValidateCheckRegisterEntry(checkEntry);

                        _db.CheckRegister.Add(checkEntry);
                        payment.CheckNumber = checkEntry.CheckNumber.ToString();
                        payment.HasCheck = true;

                        checkNumber++; // Increment for next check
                    }

                    // 6. Update next check number
                    settings.NextCheckNumber = checkNumber;

                    // 7. Commit transaction
                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    AppLogger.Information($"Contractor payroll run finalized: Period {run.PeriodStart:d} to {run.PeriodEnd:d}, {payments.Count} payments, Total: {run.TotalAmount:C}");

                    return (true, string.Empty);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                attempt++;
                if (attempt >= maxRetries)
                {
                    AppLogger.Error($"Concurrency conflict after {maxRetries} retries on ContractorPayrollRun finalization", ex);
                    return (false, "This payroll run was modified by another user. Please refresh and try again.");
                }

                AppLogger.Information($"Concurrency conflict on ContractorPayrollRun finalization, retry {attempt}/{maxRetries}");

                // Reload all affected entities
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync();
                }

                // Exponential backoff
                await Task.Delay(100 * attempt);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to finalize contractor payroll: {ex.Message}", ex);
                return (false, $"Finalization failed: {ex.Message}");
            }
        }

        // Should never reach here, but satisfy compiler
        return (false, "Unexpected error during finalization");
    }

    /// <summary>
    /// Edits a contractor payment. Enforces IsLocked constraint.
    /// </summary>
    public async Task<(bool success, string error)> EditPaymentAsync(ContractorPayment payment)
    {
        // BLOCK: Cannot edit locked payments
        if (payment.IsLocked)
            return (false, "Cannot edit finalized payment");

        // BLOCK: Cannot edit if part of finalized run
        if (payment.ContractorPayrollRunId.HasValue)
        {
            var run = await _db.ContractorPayrollRuns.FindAsync(payment.ContractorPayrollRunId.Value);
            if (run?.Status == ContractorPayrollRunStatus.Finalized)
                return (false, "Cannot edit payment from finalized payroll run");
        }

        payment.UpdatedBy = Environment.UserName;
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        AppLogger.Information($"Contractor payment edited: ID {payment.Id}, Amount: {payment.Amount:C}");
        return (true, string.Empty);
    }

    /// <summary>
    /// Deletes a contractor payroll run. Enforces finalization constraint.
    /// </summary>
    public async Task<(bool success, string error)> DeletePayrollRunAsync(int runId)
    {
        var run = await _db.ContractorPayrollRuns
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == runId);

        if (run == null)
            return (false, "Payroll run not found");

        if (run.Status == ContractorPayrollRunStatus.Finalized)
            return (false, "Cannot delete finalized payroll run");

        // Delete associated payments that are not locked
        foreach (var payment in run.Payments.ToList())
        {
            if (payment.IsLocked)
                return (false, $"Cannot delete run with locked payment (ID: {payment.Id})");

            _db.ContractorPayments.Remove(payment);
        }

        _db.ContractorPayrollRuns.Remove(run);
        await _db.SaveChangesAsync();

        AppLogger.Information($"Contractor payroll run deleted: ID {runId}");
        return (true, string.Empty);
    }

    /// <summary>
    /// Validates XOR constraint: exactly ONE of PaycheckId or ContractorPaymentId must be non-null.
    /// </summary>
    private void ValidateCheckRegisterEntry(CheckRegisterEntry entry)
    {
        bool hasPaycheck = entry.PaycheckId.HasValue;
        bool hasContractor = entry.ContractorPaymentId.HasValue;

        if (hasPaycheck == hasContractor)  // Both null OR both set = invalid
        {
            throw new InvalidOperationException(
                "CheckRegisterEntry must reference exactly ONE payment type (XOR constraint). " +
                $"PaycheckId: {entry.PaycheckId}, ContractorPaymentId: {entry.ContractorPaymentId}");
        }
    }

    /// <summary>
    /// Checks if a contractor can have their rate edited (not in finalized runs).
    /// </summary>
    public async Task<bool> CanEditContractorRateAsync(int contractorId)
    {
        return !await _db.ContractorPayments
            .Include(p => p.ContractorPayrollRun)
            .AnyAsync(p => p.ContractorId == contractorId &&
                          p.ContractorPayrollRun != null &&
                          p.ContractorPayrollRun.Status == ContractorPayrollRunStatus.Finalized);
    }

    /// <summary>
    /// Gets all contractor payroll runs with summary information.
    /// </summary>
    public async Task<List<ContractorPayrollRun>> GetPayrollRunsAsync(bool includeDrafts = true)
    {
        var query = _db.ContractorPayrollRuns
            .Include(r => r.Payments)
            .AsQueryable();

        if (!includeDrafts)
            query = query.Where(r => r.Status == ContractorPayrollRunStatus.Finalized);

        return await query
            .OrderByDescending(r => r.PayDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a single payroll run with all payments and contractor details.
    /// </summary>
    public async Task<ContractorPayrollRun?> GetPayrollRunByIdAsync(int id)
    {
        return await _db.ContractorPayrollRuns
            .Include(r => r.Payments)
                .ThenInclude(p => p.Contractor)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>
    /// Creates a new draft contractor payroll run.
    /// </summary>
    public async Task<(bool success, string error, ContractorPayrollRun? run)> CreateDraftPayrollRunAsync(
        DateTime periodStart,
        DateTime periodEnd,
        DateTime payDate,
        PayFrequency payFrequency)
    {
        try
        {
            // Validate dates
            if (periodEnd < periodStart)
                return (false, "Period end date must be after period start date", null);

            if (payDate < periodEnd)
                return (false, "Pay date must be on or after period end date", null);

            var run = new ContractorPayrollRun
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                PayDate = payDate,
                PayFrequency = payFrequency,
                Status = ContractorPayrollRunStatus.Draft,
                TotalAmount = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Environment.UserName
            };

            _db.ContractorPayrollRuns.Add(run);
            await _db.SaveChangesAsync();

            AppLogger.Information($"Contractor payroll run created (draft): Period {periodStart:d} to {periodEnd:d}");
            return (true, string.Empty, run);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to create contractor payroll run: {ex.Message}", ex);
            return (false, $"Failed to create payroll run: {ex.Message}", null);
        }
    }
}
