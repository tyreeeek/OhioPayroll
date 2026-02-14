using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(50);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(50);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
