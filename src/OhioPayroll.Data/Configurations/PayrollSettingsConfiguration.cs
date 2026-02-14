using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class PayrollSettingsConfiguration : IEntityTypeConfiguration<PayrollSettings>
{
    public void Configure(EntityTypeBuilder<PayrollSettings> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.LocalTaxRate).HasPrecision(10, 6);
        builder.Property(s => s.SchoolDistrictRate).HasPrecision(10, 6);
        builder.Property(s => s.SutaRate).HasPrecision(10, 6);
        builder.Property(s => s.SchoolDistrictCode).HasMaxLength(10);
        builder.Property(s => s.BackupDirectory).HasMaxLength(500);
        builder.Property(s => s.CheckOffsetX).HasPrecision(8, 2);
        builder.Property(s => s.CheckOffsetY).HasPrecision(8, 2);
    }
}
