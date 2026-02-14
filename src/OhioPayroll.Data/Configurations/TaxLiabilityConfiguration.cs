using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class TaxLiabilityConfiguration : IEntityTypeConfiguration<TaxLiability>
{
    public void Configure(EntityTypeBuilder<TaxLiability> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.AmountOwed).HasPrecision(18, 2);
        builder.Property(t => t.AmountPaid).HasPrecision(18, 2);
        builder.Property(t => t.PaymentReference).HasMaxLength(100);
        builder.HasIndex(t => new { t.TaxYear, t.Quarter, t.TaxType });
    }
}
