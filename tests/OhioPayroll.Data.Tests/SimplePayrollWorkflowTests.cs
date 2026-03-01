using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;
using OhioPayroll.Data.Services;

namespace OhioPayroll.Data.Tests;

/// <summary>
/// Simplified integration tests for payroll workflows.
/// Tests core functionality without complex scenarios.
/// </summary>
public class SimplePayrollWorkflowTests : IDisposable
{
    private readonly PayrollDbContext _db;
    private readonly EncryptionService _encryption;

    public SimplePayrollWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new PayrollDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        _encryption = new EncryptionService(key);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var company = new CompanyInfo
        {
            CompanyName = "Test Corporation",
            Address = "123 Test St",
            City = "Columbus",
            State = "OH",
            ZipCode = "43215"
        };

        var settings = new PayrollSettings
        {
            NextCheckNumber = 1001,
            SutaRate = 0.027m
        };

        _db.CompanyInfo.Add(company);
        _db.PayrollSettings.Add(settings);
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreateEmployee_SavesSuccessfully()
    {
        // ARRANGE
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            EncryptedSsn = _encryption.Encrypt("123-45-6789"),
            HireDate = new DateTime(2024, 1, 1),
            PayType = PayType.Hourly,
            HourlyRate = 25.00m,
            FederalFilingStatus = FilingStatus.Single,
            FederalAllowances = 0,
            OhioFilingStatus = FilingStatus.Single,
            OhioExemptions = 1,
            State = "OH",
            IsActive = true
        };

        // ACT
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        // ASSERT
        var saved = await _db.Employees.FindAsync(employee.Id);
        saved.Should().NotBeNull();
        saved!.FirstName.Should().Be("John");
        saved.LastName.Should().Be("Doe");

        // Verify encryption
        var decryptedSsn = _encryption.Decrypt(saved.EncryptedSsn);
        decryptedSsn.Should().Be("123-45-6789");
    }

    [Fact]
    public async Task CreatePayrollRun_WithMultiplePaychecks_Success()
    {
        // ARRANGE: Create employees
        var employee1 = new Employee
        {
            FirstName = "Alice", LastName = "Smith",
            EncryptedSsn = _encryption.Encrypt("111-11-1111"),
            HireDate = DateTime.Now, PayType = PayType.Hourly,
            HourlyRate = 20m, State = "OH",
            FederalFilingStatus = FilingStatus.Single,
            OhioFilingStatus = FilingStatus.Single,
            IsActive = true
        };

        var employee2 = new Employee
        {
            FirstName = "Bob", LastName = "Johnson",
            EncryptedSsn = _encryption.Encrypt("222-22-2222"),
            HireDate = DateTime.Now, PayType = PayType.Salary,
            AnnualSalary = 52000m, State = "OH",
            FederalFilingStatus = FilingStatus.Married,
            OhioFilingStatus = FilingStatus.Married,
            IsActive = true
        };

        _db.Employees.AddRange(employee1, employee2);
        await _db.SaveChangesAsync();

        var payrollRun = new PayrollRun
        {
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 15),
            PayDate = new DateTime(2024, 1, 20),
            Status = PayrollRunStatus.Draft,
            PayFrequency = PayFrequency.BiWeekly
        };

        _db.PayrollRuns.Add(payrollRun);
        await _db.SaveChangesAsync();

        // ACT: Create paychecks
        var paycheck1 = new Paycheck
        {
            EmployeeId = employee1.Id,
            PayrollRunId = payrollRun.Id,
            RegularHours = 80, RegularPay = 1600m,
            GrossPay = 1600m, NetPay = 1200m,
            CheckNumber = 1001
        };

        var paycheck2 = new Paycheck
        {
            EmployeeId = employee2.Id,
            PayrollRunId = payrollRun.Id,
            RegularPay = 2000m,
            GrossPay = 2000m, NetPay = 1500m,
            CheckNumber = 1002
        };

        _db.Paychecks.AddRange(paycheck1, paycheck2);
        await _db.SaveChangesAsync();

        // ASSERT
        var paychecks = await _db.Paychecks
            .Where(p => p.PayrollRunId == payrollRun.Id)
            .ToListAsync();

        paychecks.Should().HaveCount(2);
        paychecks.Sum(p => p.GrossPay).Should().Be(3600m);
    }

    [Fact]
    public async Task FinalizePayrollRun_UpdatesStatus()
    {
        // ARRANGE
        var payrollRun = new PayrollRun
        {
            PeriodStart = DateTime.Now,
            PeriodEnd = DateTime.Now.AddDays(14),
            PayDate = DateTime.Now.AddDays(20),
            Status = PayrollRunStatus.Draft,
            PayFrequency = PayFrequency.BiWeekly
        };

        _db.PayrollRuns.Add(payrollRun);
        await _db.SaveChangesAsync();

        // ACT
        payrollRun.Status = PayrollRunStatus.Finalized;
        payrollRun.FinalizedAt = DateTime.UtcNow;
        payrollRun.TotalGrossPay = 5000m;
        payrollRun.TotalNetPay = 3800m;

        await _db.SaveChangesAsync();

        // ASSERT
        var saved = await _db.PayrollRuns.FindAsync(payrollRun.Id);
        saved!.Status.Should().Be(PayrollRunStatus.Finalized);
        saved.FinalizedAt.Should().NotBeNull();
        saved.TotalGrossPay.Should().Be(5000m);
    }

    [Fact]
    public async Task VoidPaycheck_SetsVoidFlag()
    {
        // ARRANGE
        var employee = new Employee
        {
            FirstName = "Test", LastName = "Void",
            EncryptedSsn = _encryption.Encrypt("555-55-5555"),
            HireDate = DateTime.Now, PayType = PayType.Hourly,
            HourlyRate = 25m, State = "OH",
            FederalFilingStatus = FilingStatus.Single,
            OhioFilingStatus = FilingStatus.Single,
            IsActive = true
        };

        _db.Employees.Add(employee);

        var payrollRun = new PayrollRun
        {
            PeriodStart = DateTime.Now,
            PeriodEnd = DateTime.Now.AddDays(14),
            PayDate = DateTime.Now.AddDays(20),
            Status = PayrollRunStatus.Finalized,
            PayFrequency = PayFrequency.BiWeekly
        };

        _db.PayrollRuns.Add(payrollRun);
        await _db.SaveChangesAsync();

        var paycheck = new Paycheck
        {
            EmployeeId = employee.Id,
            PayrollRunId = payrollRun.Id,
            GrossPay = 2000m, NetPay = 1500m,
            CheckNumber = 5001
        };

        _db.Paychecks.Add(paycheck);
        await _db.SaveChangesAsync();

        // ACT
        paycheck.IsVoid = true;
        paycheck.VoidDate = DateTime.UtcNow;
        paycheck.VoidReason = "Check lost";

        await _db.SaveChangesAsync();

        // ASSERT
        var saved = await _db.Paychecks.FindAsync(paycheck.Id);
        saved!.IsVoid.Should().BeTrue();
        saved.VoidReason.Should().Be("Check lost");
        saved.VoidDate.Should().NotBeNull();
    }

    [Fact]
    public async Task TransactionRollback_LeavesNoPartialData()
    {
        // ARRANGE
        var initialCount = await _db.PayrollRuns.CountAsync();

        // ACT: Transaction that will be rolled back
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var run = new PayrollRun
            {
                PeriodStart = DateTime.Now,
                PeriodEnd = DateTime.Now.AddDays(14),
                PayDate = DateTime.Now.AddDays(20),
                Status = PayrollRunStatus.Draft,
                PayFrequency = PayFrequency.BiWeekly
            };

            _db.PayrollRuns.Add(run);
            await _db.SaveChangesAsync();

            // Simulate error
            throw new InvalidOperationException("Simulated error");
        }
#pragma warning disable CA1031 // Intentional catch-all for rollback demonstration
        catch
#pragma warning restore CA1031
        {
            await transaction.RollbackAsync();
        }

        // ASSERT
        var finalCount = await _db.PayrollRuns.CountAsync();
        finalCount.Should().Be(initialCount);
    }

    [Fact]
    public async Task EmployeeBankAccount_CreatedSuccessfully()
    {
        // ARRANGE
        var employee = new Employee
        {
            FirstName = "Direct", LastName = "Deposit",
            EncryptedSsn = _encryption.Encrypt("999-99-9999"),
            HireDate = DateTime.Now, PayType = PayType.Salary,
            AnnualSalary = 52000m, State = "OH",
            FederalFilingStatus = FilingStatus.Single,
            OhioFilingStatus = FilingStatus.Single,
            IsActive = true
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        // ACT
        var bankAccount = new EmployeeBankAccount
        {
            EmployeeId = employee.Id,
            EncryptedRoutingNumber = _encryption.Encrypt("123456789"),
            EncryptedAccountNumber = _encryption.Encrypt("987654321"),
            AccountType = BankAccountType.Checking,
            IsActive = true
        };

        _db.EmployeeBankAccounts.Add(bankAccount);
        await _db.SaveChangesAsync();

        // ASSERT
        var saved = await _db.EmployeeBankAccounts
            .FirstAsync(b => b.EmployeeId == employee.Id);

        saved.Should().NotBeNull();
        saved.AccountType.Should().Be(BankAccountType.Checking);
        saved.IsActive.Should().BeTrue();

        // Verify encryption
        var routing = _encryption.Decrypt(saved.EncryptedRoutingNumber);
        var account = _encryption.Decrypt(saved.EncryptedAccountNumber);

        routing.Should().Be("123456789");
        account.Should().Be("987654321");
    }

    [Fact]
    public async Task NetPayCalculation_MatchesDeductions()
    {
        // ARRANGE
        var employee = new Employee
        {
            FirstName = "Math", LastName = "Check",
            EncryptedSsn = _encryption.Encrypt("777-77-7777"),
            HireDate = DateTime.Now, PayType = PayType.Hourly,
            HourlyRate = 30m, State = "OH",
            FederalFilingStatus = FilingStatus.Single,
            OhioFilingStatus = FilingStatus.Single,
            IsActive = true
        };

        _db.Employees.Add(employee);

        var payrollRun = new PayrollRun
        {
            PeriodStart = DateTime.Now,
            PeriodEnd = DateTime.Now.AddDays(14),
            PayDate = DateTime.Now.AddDays(20),
            Status = PayrollRunStatus.Draft,
            PayFrequency = PayFrequency.BiWeekly
        };

        _db.PayrollRuns.Add(payrollRun);
        await _db.SaveChangesAsync();

        // ACT
        var paycheck = new Paycheck
        {
            EmployeeId = employee.Id,
            PayrollRunId = payrollRun.Id,
            GrossPay = 2400m,
            FederalWithholding = 180m,
            SocialSecurityTax = 148.80m,
            MedicareTax = 34.80m,
            OhioStateWithholding = 50m,
            TotalDeductions = 413.60m,
            NetPay = 1986.40m
        };

        _db.Paychecks.Add(paycheck);
        await _db.SaveChangesAsync();

        // ASSERT
        var saved = await _db.Paychecks.FindAsync(paycheck.Id);
        var calculatedNet = saved!.GrossPay - saved.TotalDeductions;

        saved.NetPay.Should().Be(calculatedNet);
        saved.TotalDeductions.Should().Be(
            saved.FederalWithholding +
            saved.SocialSecurityTax +
            saved.MedicareTax +
            saved.OhioStateWithholding
        );
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        _encryption.Dispose();
        GC.SuppressFinalize(this);
    }
}
