using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class ContractorPayrollRunConfiguration : IEntityTypeConfiguration<ContractorPayrollRun>
{
    public void Configure(EntityTypeBuilder<ContractorPayrollRun> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TotalAmount).HasPrecision(18, 2);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(c => c.FinalizedBy).HasMaxLength(100);
        builder.HasIndex(c => c.PayDate);
        builder.HasIndex(c => c.Status);

        // Optimistic concurrency token
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}
