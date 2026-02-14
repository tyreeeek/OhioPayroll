using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OhioPayroll.Core.Models;

namespace OhioPayroll.Data.Configurations;

public class PaycheckConfiguration : IEntityTypeConfiguration<Paycheck>
{
    public void Configure(EntityTypeBuilder<Paycheck> builder)
    {
        builder.HasKey(p => p.Id);

        // Earnings
        builder.Property(p => p.RegularHours).HasPrecision(8, 2);
        builder.Property(p => p.OvertimeHours).HasPrecision(8, 2);
        builder.Property(p => p.RegularPay).HasPrecision(18, 2);
        builder.Property(p => p.OvertimePay).HasPrecision(18, 2);
        builder.Property(p => p.GrossPay).HasPrecision(18, 2);

        // Employee Taxes
        builder.Property(p => p.FederalWithholding).HasPrecision(18, 2);
        builder.Property(p => p.OhioStateWithholding).HasPrecision(18, 2);
        builder.Property(p => p.SchoolDistrictTax).HasPrecision(18, 2);
        builder.Property(p => p.LocalMunicipalityTax).HasPrecision(18, 2);
        builder.Property(p => p.SocialSecurityTax).HasPrecision(18, 2);
        builder.Property(p => p.MedicareTax).HasPrecision(18, 2);

        // Employer Taxes
        builder.Property(p => p.EmployerSocialSecurity).HasPrecision(18, 2);
        builder.Property(p => p.EmployerMedicare).HasPrecision(18, 2);
        builder.Property(p => p.EmployerFuta).HasPrecision(18, 2);
        builder.Property(p => p.EmployerSuta).HasPrecision(18, 2);

        // Totals
        builder.Property(p => p.TotalDeductions).HasPrecision(18, 2);
        builder.Property(p => p.NetPay).HasPrecision(18, 2);

        // YTD
        builder.Property(p => p.YtdGrossPay).HasPrecision(18, 2);
        builder.Property(p => p.YtdFederalWithholding).HasPrecision(18, 2);
        builder.Property(p => p.YtdOhioStateWithholding).HasPrecision(18, 2);
        builder.Property(p => p.YtdSchoolDistrictTax).HasPrecision(18, 2);
        builder.Property(p => p.YtdLocalTax).HasPrecision(18, 2);
        builder.Property(p => p.YtdSocialSecurity).HasPrecision(18, 2);
        builder.Property(p => p.YtdMedicare).HasPrecision(18, 2);
        builder.Property(p => p.YtdNetPay).HasPrecision(18, 2);

        // Payment
        builder.Property(p => p.AchTraceNumber).HasMaxLength(50);
        builder.Property(p => p.VoidReason).HasMaxLength(500);

        // Relationships
        builder.HasOne(p => p.PayrollRun)
            .WithMany(r => r.Paychecks)
            .HasForeignKey(p => p.PayrollRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Employee)
            .WithMany(e => e.Paychecks)
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.PayrollRunId);
        builder.HasIndex(p => p.EmployeeId);
        builder.HasIndex(p => p.CheckNumber);
    }
}

