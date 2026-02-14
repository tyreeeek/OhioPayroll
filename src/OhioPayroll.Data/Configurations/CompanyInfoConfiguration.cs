using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class CompanyInfoConfiguration : IEntityTypeConfiguration<CompanyInfo>
{
    public void Configure(EntityTypeBuilder<CompanyInfo> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Ein).IsRequired().HasMaxLength(20);
        builder.Property(c => c.StateWithholdingId).HasMaxLength(30);
        builder.Property(c => c.Address).HasMaxLength(200);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.State).HasMaxLength(2);
        builder.Property(c => c.ZipCode).HasMaxLength(10);
        builder.Property(c => c.Phone).HasMaxLength(20);
    }
}

