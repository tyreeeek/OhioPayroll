using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class ContractorPaymentConfiguration : IEntityTypeConfiguration<ContractorPayment>
{
    public void Configure(EntityTypeBuilder<ContractorPayment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.CheckNumber).HasMaxLength(20);
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.HasOne(p => p.Contractor)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.ContractorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(p => p.TaxYear).IsRequired();
        builder.HasIndex(p => new { p.ContractorId, p.TaxYear });
        builder.HasIndex(p => p.PaymentDate);
        builder.HasIndex(p => new { p.ContractorId, p.IsDeleted });
    }
}
