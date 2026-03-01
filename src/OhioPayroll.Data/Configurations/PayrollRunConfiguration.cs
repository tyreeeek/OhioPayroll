using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TotalGrossPay).HasPrecision(18, 2);
        builder.Property(p => p.TotalNetPay).HasPrecision(18, 2);
        builder.Property(p => p.TotalFederalTax).HasPrecision(18, 2);
        builder.Property(p => p.TotalStateTax).HasPrecision(18, 2);
        builder.Property(p => p.TotalLocalTax).HasPrecision(18, 2);
        builder.Property(p => p.TotalSocialSecurity).HasPrecision(18, 2);
        builder.Property(p => p.TotalMedicare).HasPrecision(18, 2);
        builder.Property(p => p.TotalEmployerSocialSecurity).HasPrecision(18, 2);
        builder.Property(p => p.TotalEmployerMedicare).HasPrecision(18, 2);
        builder.Property(p => p.TotalEmployerFuta).HasPrecision(18, 2);
        builder.Property(p => p.TotalEmployerSuta).HasPrecision(18, 2);
        builder.HasIndex(p => p.PayDate);
        builder.HasIndex(p => p.Status);

        // Optimistic concurrency token
        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}

