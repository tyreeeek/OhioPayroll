using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class TaxTableConfiguration : IEntityTypeConfiguration<TaxTable>
{
    public void Configure(EntityTypeBuilder<TaxTable> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.BracketStart).HasPrecision(18, 2);
        builder.Property(t => t.BracketEnd).HasPrecision(18, 2);
        builder.Property(t => t.Rate).HasPrecision(10, 6);
        builder.Property(t => t.BaseAmount).HasPrecision(18, 2);
        builder.Property(t => t.DistrictCode).HasMaxLength(10);
        builder.HasIndex(t => new { t.TaxYear, t.Type, t.FilingStatus });
    }
}

