using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.Data.Tests;

/// <summary>
/// Integration tests for the void paycheck workflow.
/// Tests that voiding a paycheck correctly updates:
/// - Paycheck void status, date, and reason
/// - Check register entry status
/// - Tax liabilities (reduced by voided amounts)
/// - Payroll run totals
/// </summary>
public class VoidPaycheckTests : IDisposable
{
    private readonly PayrollDbContext _db;

    public VoidPaycheckTests()
    {
        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new PayrollDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private Employee CreateTestEmployee(string firstName = "John", string lastName = "Doe")
    {
        var employee = new Employee
        {
            FirstName = firstName,
            LastName = lastName,
            EncryptedSsn = "encrypted_ssn",
            SsnLast4 = "1234",
            PayType = PayType.Hourly,
            HourlyRate = 25.00m,
            FederalFilingStatus = FilingStatus.Single,
            OhioFilingStatus = FilingStatus.Single,
            Address = "123 Main St",
            City = "Columbus",
            State = "OH",
            ZipCode = "43215",
            HireDate = new DateTime(2023, 1, 1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Employees.Add(employee);
        return employee;
    }

    private PayrollRun CreateTestPayrollRun(DateTime payDate)
    {
        var run = new PayrollRun
        {
            PeriodStart = payDate.AddDays(-13),
            PeriodEnd = payDate.AddDays(-1),
            PayDate = payDate,
            PayFrequency = PayFrequency.BiWeekly,
            Status = PayrollRunStatus.Finalized,
            TotalGrossPay = 2000.00m,
            TotalNetPay = 1500.00m,
            TotalFederalTax = 200.00m,
            TotalStateTax = 80.00m,
            TotalLocalTax = 40.00m,
            TotalSocialSecurity = 124.00m,
            TotalMedicare = 29.00m,
            TotalEmployerSocialSecurity = 124.00m,
            TotalEmployerMedicare = 29.00m,
            TotalEmployerFuta = 12.00m,
            TotalEmployerSuta = 54.00m,
            CreatedAt = DateTime.UtcNow,
            FinalizedAt = DateTime.UtcNow
        };
        _db.PayrollRuns.Add(run);
        return run;
    }

    private Paycheck CreateTestPaycheck(PayrollRun run, Employee employee, int checkNumber = 1001)
    {
        var paycheck = new Paycheck
        {
            PayrollRun = run,
            Employee = employee,
            RegularHours = 80m,
            OvertimeHours = 0m,
            RegularPay = 2000.00m,
            OvertimePay = 0m,
            GrossPay = 2000.00m,
            FederalWithholding = 200.00m,
            OhioStateWithholding = 80.00m,
            SchoolDistrictTax = 0m,
            LocalMunicipalityTax = 40.00m,
            SocialSecurityTax = 124.00m,
            MedicareTax = 29.00m,
            EmployerSocialSecurity = 124.00m,
            EmployerMedicare = 29.00m,
            EmployerFuta = 12.00m,
            EmployerSuta = 54.00m,
            TotalDeductions = 473.00m,
            NetPay = 1527.00m,
            PaymentMethod = PaymentMethod.Check,
            CheckNumber = checkNumber,
            IsVoid = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Paychecks.Add(paycheck);
        return paycheck;
    }

    private CheckRegisterEntry CreateCheckRegisterEntry(Paycheck paycheck)
    {
        var entry = new CheckRegisterEntry
        {
            CheckNumber = paycheck.CheckNumber ?? 0,
            Paycheck = paycheck,
            Status = CheckStatus.Issued,
            Amount = paycheck.NetPay,
            IssuedDate = paycheck.PayrollRun.PayDate,
            CreatedAt = DateTime.UtcNow
        };
        _db.CheckRegister.Add(entry);
        return entry;
    }

    private TaxLiability CreateTaxLiability(TaxType taxType, int year, int quarter, decimal amount)
    {
        var liability = new TaxLiability
        {
            TaxType = taxType,
            TaxYear = year,
            Quarter = quarter,
            PeriodStart = new DateTime(year, (quarter - 1) * 3 + 1, 1),
            PeriodEnd = new DateTime(year, quarter * 3, DateTime.DaysInMonth(year, quarter * 3)),
            AmountOwed = amount,
            AmountPaid = 0m,
            Status = TaxLiabilityStatus.Unpaid,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.TaxLiabilities.Add(liability);
        return liability;
    }

    private async Task SetupStandardScenario(DateTime payDate = default)
    {
        if (payDate == default)
            payDate = new DateTime(2026, 1, 15);

        var employee = CreateTestEmployee();
        var run = CreateTestPayrollRun(payDate);
        var paycheck = CreateTestPaycheck(run, employee);
        CreateCheckRegisterEntry(paycheck);

        int quarter = (payDate.Month - 1) / 3 + 1;
        int year = payDate.Year;

        // Create tax liabilities matching the paycheck amounts
        CreateTaxLiability(TaxType.Federal, year, quarter, 200.00m);
        CreateTaxLiability(TaxType.Ohio, year, quarter, 80.00m);
        CreateTaxLiability(TaxType.Local, year, quarter, 40.00m);
        CreateTaxLiability(TaxType.FICA_SS, year, quarter, 248.00m); // Employee + Employer
        CreateTaxLiability(TaxType.FICA_Med, year, quarter, 58.00m); // Employee + Employer
        CreateTaxLiability(TaxType.FUTA, year, quarter, 12.00m);
        CreateTaxLiability(TaxType.SUTA, year, quarter, 54.00m);

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Simulates the void paycheck workflow from PayrollRunViewModel.
    /// </summary>
    private async Task VoidPaycheckAsync(int paycheckId, string reason)
    {
        var paycheck = await _db.Paychecks
            .Include(p => p.PayrollRun)
            .FirstOrDefaultAsync(p => p.Id == paycheckId);

        if (paycheck is null)
            throw new InvalidOperationException("Paycheck not found.");

        if (paycheck.IsVoid)
            throw new InvalidOperationException("Paycheck already voided.");

        // Mark paycheck as void
        paycheck.IsVoid = true;
        paycheck.VoidDate = DateTime.UtcNow;
        paycheck.VoidReason = reason;

        // Update check register
        var checkEntry = await _db.CheckRegister
            .FirstOrDefaultAsync(c => c.PaycheckId == paycheckId);
        if (checkEntry is not null)
        {
            checkEntry.Status = CheckStatus.Voided;
            checkEntry.VoidDate = DateTime.UtcNow;
            checkEntry.VoidReason = reason;
        }

        // Reduce tax liabilities
        int quarter = (paycheck.PayrollRun.PayDate.Month - 1) / 3 + 1;
        int year = paycheck.PayrollRun.PayDate.Year;

        await ReduceTaxLiabilityAsync(TaxType.Federal, year, quarter, paycheck.FederalWithholding);
        await ReduceTaxLiabilityAsync(TaxType.Ohio, year, quarter, paycheck.OhioStateWithholding);
        await ReduceTaxLiabilityAsync(TaxType.Local, year, quarter, paycheck.LocalMunicipalityTax);
        await ReduceTaxLiabilityAsync(TaxType.SchoolDistrict, year, quarter, paycheck.SchoolDistrictTax);
        await ReduceTaxLiabilityAsync(TaxType.FICA_SS, year, quarter,
            paycheck.SocialSecurityTax + paycheck.EmployerSocialSecurity);
        await ReduceTaxLiabilityAsync(TaxType.FICA_Med, year, quarter,
            paycheck.MedicareTax + paycheck.EmployerMedicare);
        await ReduceTaxLiabilityAsync(TaxType.FUTA, year, quarter, paycheck.EmployerFuta);
        await ReduceTaxLiabilityAsync(TaxType.SUTA, year, quarter, paycheck.EmployerSuta);

        // Update payroll run totals
        var run = paycheck.PayrollRun;
        run.TotalGrossPay -= paycheck.GrossPay;
        run.TotalNetPay -= paycheck.NetPay;
        run.TotalFederalTax -= paycheck.FederalWithholding;
        run.TotalStateTax -= paycheck.OhioStateWithholding;
        run.TotalLocalTax -= paycheck.LocalMunicipalityTax;
        run.TotalSocialSecurity -= paycheck.SocialSecurityTax;
        run.TotalMedicare -= paycheck.MedicareTax;
        run.TotalEmployerSocialSecurity -= paycheck.EmployerSocialSecurity;
        run.TotalEmployerMedicare -= paycheck.EmployerMedicare;
        run.TotalEmployerFuta -= paycheck.EmployerFuta;
        run.TotalEmployerSuta -= paycheck.EmployerSuta;

        await _db.SaveChangesAsync();
    }

    private async Task ReduceTaxLiabilityAsync(TaxType taxType, int year, int quarter, decimal amount)
    {
        if (amount <= 0) return;

        var liability = await _db.TaxLiabilities
            .FirstOrDefaultAsync(t => t.TaxType == taxType
                && t.TaxYear == year
                && t.Quarter == quarter);

        if (liability is not null)
        {
            liability.AmountOwed = Math.Max(0, liability.AmountOwed - amount);
            liability.UpdatedAt = DateTime.UtcNow;

            if (liability.AmountOwed <= liability.AmountPaid)
            {
                liability.Status = TaxLiabilityStatus.Paid;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task VoidPaycheck_MarksPaycheckAsVoid()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();
        var reason = "Employee quit before pay date";

        // Act
        await VoidPaycheckAsync(paycheck.Id, reason);

        // Assert
        var voidedPaycheck = await _db.Paychecks.FirstAsync(p => p.Id == paycheck.Id);
        voidedPaycheck.IsVoid.Should().BeTrue();
        voidedPaycheck.VoidDate.Should().NotBeNull();
        voidedPaycheck.VoidDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        voidedPaycheck.VoidReason.Should().Be(reason);
    }

    [Fact]
    public async Task VoidPaycheck_UpdatesCheckRegisterEntry()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();
        var reason = "Wrong check amount";

        // Act
        await VoidPaycheckAsync(paycheck.Id, reason);

        // Assert
        var checkEntry = await _db.CheckRegister.FirstAsync(c => c.PaycheckId == paycheck.Id);
        checkEntry.Status.Should().Be(CheckStatus.Voided);
        checkEntry.VoidDate.Should().NotBeNull();
        checkEntry.VoidReason.Should().Be(reason);
    }

    [Fact]
    public async Task VoidPaycheck_ReducesFederalTaxLiability()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();
        var originalLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Federal);
        var originalAmount = originalLiability.AmountOwed;

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert
        var updatedLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Federal);
        updatedLiability.AmountOwed.Should().Be(originalAmount - paycheck.FederalWithholding);
        updatedLiability.AmountOwed.Should().Be(0m);
    }

    [Fact]
    public async Task VoidPaycheck_ReducesStateTaxLiability()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert
        var liability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Ohio);
        liability.AmountOwed.Should().Be(0m);
    }

    [Fact]
    public async Task VoidPaycheck_ReducesFicaLiabilities()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert
        var ssLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.FICA_SS);
        ssLiability.AmountOwed.Should().Be(0m);

        var medLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.FICA_Med);
        medLiability.AmountOwed.Should().Be(0m);
    }

    [Fact]
    public async Task VoidPaycheck_ReducesFutaAndSutaLiabilities()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert
        var futaLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.FUTA);
        futaLiability.AmountOwed.Should().Be(0m);

        var sutaLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.SUTA);
        sutaLiability.AmountOwed.Should().Be(0m);
    }

    [Fact]
    public async Task VoidPaycheck_UpdatesPayrollRunTotals()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.Include(p => p.PayrollRun).FirstAsync();
        var run = paycheck.PayrollRun;
        var originalGross = run.TotalGrossPay;
        var originalNet = run.TotalNetPay;

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert
        var updatedRun = await _db.PayrollRuns.FirstAsync(r => r.Id == run.Id);
        updatedRun.TotalGrossPay.Should().Be(originalGross - paycheck.GrossPay);
        updatedRun.TotalNetPay.Should().Be(originalNet - paycheck.NetPay);
        updatedRun.TotalFederalTax.Should().Be(0m);
        updatedRun.TotalStateTax.Should().Be(0m);
        updatedRun.TotalSocialSecurity.Should().Be(0m);
        updatedRun.TotalMedicare.Should().Be(0m);
        updatedRun.TotalEmployerSocialSecurity.Should().Be(0m);
        updatedRun.TotalEmployerMedicare.Should().Be(0m);
        updatedRun.TotalEmployerFuta.Should().Be(0m);
        updatedRun.TotalEmployerSuta.Should().Be(0m);
    }

    [Fact]
    public async Task VoidPaycheck_AlreadyVoided_ThrowsException()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();
        await VoidPaycheckAsync(paycheck.Id, "First void");

        // Act
        var act = async () => await VoidPaycheckAsync(paycheck.Id, "Second void attempt");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already voided*");
    }

    [Fact]
    public async Task VoidPaycheck_PaycheckNotFound_ThrowsException()
    {
        // Arrange
        await SetupStandardScenario();

        // Act
        var act = async () => await VoidPaycheckAsync(99999, "Test void");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task VoidPaycheck_PartialLiabilityReduction_LeavesRemainder()
    {
        // Arrange: Create scenario with two paychecks, void only one
        var payDate = new DateTime(2026, 1, 15);
        var employee1 = CreateTestEmployee("John", "Doe");
        var employee2 = CreateTestEmployee("Jane", "Smith");
        var run = CreateTestPayrollRun(payDate);

        // Adjust run totals for two employees
        run.TotalGrossPay = 4000.00m;
        run.TotalNetPay = 3000.00m;
        run.TotalFederalTax = 400.00m;

        var paycheck1 = CreateTestPaycheck(run, employee1, 1001);
        var paycheck2 = CreateTestPaycheck(run, employee2, 1002);
        CreateCheckRegisterEntry(paycheck1);
        CreateCheckRegisterEntry(paycheck2);

        // Create combined tax liability for both paychecks
        CreateTaxLiability(TaxType.Federal, 2026, 1, 400.00m);

        await _db.SaveChangesAsync();

        // Act: Void only the first paycheck
        await VoidPaycheckAsync(paycheck1.Id, "Test void");

        // Assert: Federal tax liability should be reduced by one paycheck amount
        var federalLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Federal);
        federalLiability.AmountOwed.Should().Be(200.00m); // 400 - 200 = 200 remaining
    }

    [Fact]
    public async Task VoidPaycheck_LiabilityReducedToZero_UpdatesStatus()
    {
        // Arrange
        await SetupStandardScenario();
        var paycheck = await _db.Paychecks.FirstAsync();

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert: When liability is reduced to zero, status should be Paid
        var federalLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Federal);
        federalLiability.AmountOwed.Should().Be(0m);
        federalLiability.Status.Should().Be(TaxLiabilityStatus.Paid);
    }

    [Fact]
    public async Task VoidPaycheck_PreservesOtherQuarterLiabilities()
    {
        // Arrange: Create paycheck in Q1, liability in both Q1 and Q2
        var q1PayDate = new DateTime(2026, 2, 15);
        var employee = CreateTestEmployee();
        var run = CreateTestPayrollRun(q1PayDate);
        var paycheck = CreateTestPaycheck(run, employee);
        CreateCheckRegisterEntry(paycheck);

        // Q1 liability (will be reduced)
        CreateTaxLiability(TaxType.Federal, 2026, 1, 200.00m);
        // Q2 liability (should NOT be affected)
        CreateTaxLiability(TaxType.Federal, 2026, 2, 500.00m);

        await _db.SaveChangesAsync();

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert
        var q1Liability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Federal && t.Quarter == 1);
        q1Liability.AmountOwed.Should().Be(0m);

        var q2Liability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Federal && t.Quarter == 2);
        q2Liability.AmountOwed.Should().Be(500.00m); // Unchanged
    }

    [Fact]
    public async Task VoidPaycheck_WithNoCheckRegisterEntry_StillVoidsPaycheck()
    {
        // Arrange: Create paycheck without check register entry (e.g., direct deposit)
        var payDate = new DateTime(2026, 1, 15);
        var employee = CreateTestEmployee();
        var run = CreateTestPayrollRun(payDate);
        var paycheck = CreateTestPaycheck(run, employee);
        paycheck.PaymentMethod = PaymentMethod.DirectDeposit;
        paycheck.CheckNumber = null;
        // Note: No CheckRegisterEntry created

        CreateTaxLiability(TaxType.Federal, 2026, 1, 200.00m);
        await _db.SaveChangesAsync();

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Direct deposit reversal");

        // Assert
        var voidedPaycheck = await _db.Paychecks.FirstAsync(p => p.Id == paycheck.Id);
        voidedPaycheck.IsVoid.Should().BeTrue();
        voidedPaycheck.VoidReason.Should().Be("Direct deposit reversal");
    }

    [Fact]
    public async Task VoidPaycheck_NegativeLiabilityPrevented_CapsAtZero()
    {
        // Arrange: Create scenario where void amount exceeds liability
        // (edge case - shouldn't happen in practice but tests defensive coding)
        var payDate = new DateTime(2026, 1, 15);
        var employee = CreateTestEmployee();
        var run = CreateTestPayrollRun(payDate);
        var paycheck = CreateTestPaycheck(run, employee);
        CreateCheckRegisterEntry(paycheck);

        // Create liability smaller than paycheck amount
        CreateTaxLiability(TaxType.Federal, 2026, 1, 100.00m); // Less than 200.00

        await _db.SaveChangesAsync();

        // Act
        await VoidPaycheckAsync(paycheck.Id, "Test void");

        // Assert: Liability should be capped at 0, not negative
        var federalLiability = await _db.TaxLiabilities
            .FirstAsync(t => t.TaxType == TaxType.Federal);
        federalLiability.AmountOwed.Should().Be(0m);
        federalLiability.AmountOwed.Should().BeGreaterThanOrEqualTo(0m);
    }
}
