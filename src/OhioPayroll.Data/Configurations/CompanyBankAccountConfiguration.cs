using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class CompanyBankAccountConfiguration : IEntityTypeConfiguration<CompanyBankAccount>
{
    public void Configure(EntityTypeBuilder<CompanyBankAccount> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.BankName).IsRequired().HasMaxLength(100);
        builder.Property(b => b.EncryptedRoutingNumber).IsRequired();
        builder.Property(b => b.EncryptedAccountNumber).IsRequired();
    }
}

