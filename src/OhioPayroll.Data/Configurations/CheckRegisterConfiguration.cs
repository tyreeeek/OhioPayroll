using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class CheckRegisterConfiguration : IEntityTypeConfiguration<CheckRegisterEntry>
{
    public void Configure(EntityTypeBuilder<CheckRegisterEntry> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.Property(c => c.VoidReason).HasMaxLength(500);
        builder.HasIndex(c => c.CheckNumber).IsUnique();
        builder.HasIndex(c => c.Status);

        builder.HasOne(c => c.Paycheck)
            .WithOne()
            .HasForeignKey<CheckRegisterEntry>(c => c.PaycheckId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
