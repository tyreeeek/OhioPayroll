using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EncryptedSsn).IsRequired();
        builder.Property(e => e.SsnLast4).IsRequired().HasMaxLength(4);
        builder.Property(e => e.Address).HasMaxLength(200);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.State).HasMaxLength(2);
        builder.Property(e => e.ZipCode).HasMaxLength(10);
        builder.Property(e => e.SchoolDistrictCode).HasMaxLength(10);
        builder.Property(e => e.MunicipalityCode).HasMaxLength(10);
        builder.Property(e => e.HourlyRate).HasPrecision(18, 4);
        builder.Property(e => e.AnnualSalary).HasPrecision(18, 2);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.LastName, e.FirstName });
        builder.Ignore(e => e.FullName);
    }
}

