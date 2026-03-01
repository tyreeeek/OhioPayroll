using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class ContractorConfiguration : IEntityTypeConfiguration<Contractor>
{
    public void Configure(EntityTypeBuilder<Contractor> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.BusinessName).HasMaxLength(200);
        builder.Property(c => c.EncryptedTin).IsRequired();
        builder.Property(c => c.TinLast4).IsRequired().HasMaxLength(4);
        builder.Property(c => c.Address).HasMaxLength(200);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.State).HasMaxLength(2);
        builder.Property(c => c.ZipCode).HasMaxLength(10);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.HasIndex(c => c.IsActive);
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.TinLast4);
    }
}
