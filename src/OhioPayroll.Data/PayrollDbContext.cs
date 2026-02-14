using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data;

public class PayrollDbContext : DbContext
{
    public PayrollDbContext(DbContextOptions<PayrollDbContext> options)
        : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Paycheck> Paychecks => Set<Paycheck>();
    public DbSet<TaxTable> TaxTables => Set<TaxTable>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<CompanyBankAccount> CompanyBankAccounts => Set<CompanyBankAccount>();
    public DbSet<EmployeeBankAccount> EmployeeBankAccounts => Set<EmployeeBankAccount>();
    public DbSet<CheckRegisterEntry> CheckRegister => Set<CheckRegisterEntry>();
    public DbSet<TaxLiability> TaxLiabilities => Set<TaxLiability>();
    public DbSet<CompanyInfo> CompanyInfo => Set<CompanyInfo>();
    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<ContractorPayment> ContractorPayments => Set<ContractorPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayrollDbContext).Assembly);
    }
}

