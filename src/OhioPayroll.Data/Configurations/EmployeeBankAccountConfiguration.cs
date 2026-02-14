using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class EmployeeBankAccountConfiguration : IEntityTypeConfiguration<EmployeeBankAccount>
{
    public void Configure(EntityTypeBuilder<EmployeeBankAccount> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.EncryptedRoutingNumber).IsRequired();
        builder.Property(b => b.EncryptedAccountNumber).IsRequired();

        builder.HasOne(b => b.Employee)
            .WithMany(e => e.BankAccounts)
            .HasForeignKey(b => b.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
