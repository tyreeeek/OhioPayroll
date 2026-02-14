using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Data.Services;

public class DatabaseInitializer
{
    private readonly PayrollDbContext _db;

    public DatabaseInitializer(PayrollDbContext db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        await _db.Database.MigrateAsync();

        if (!await _db.PayrollSettings.AnyAsync())
        {
            _db.PayrollSettings.Add(new PayrollSettings
            {
                PayFrequency = PayFrequency.BiWeekly,
                CurrentTaxYear = DateTime.Now.Year,
                SutaRate = 0.027m,
                BackupDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OhioPayroll", "Backups"),
                NextCheckNumber = 1001,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (!await _db.CompanyInfo.AnyAsync())
        {
            _db.CompanyInfo.Add(new CompanyInfo
            {
                CompanyName = "My Company",
                Ein = "00-0000000",
                State = "OH",
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Seed tax tables for the current year if they don't exist yet
        int currentYear = DateTime.Now.Year;
        if (!await _db.TaxTables.AnyAsync(t => t.TaxYear == currentYear))
        {
            SeedFederalTaxTables(currentYear);
            SeedOhioTaxTables(currentYear);
        }

        await _db.SaveChangesAsync();
    }

    private void SeedFederalTaxTables(int taxYear)
    {
        // IRS Publication 15-T - Percentage Method Tables
        // WARNING: These values MUST be validated against the official IRS publication
        // before use in production. Bracket amounts may change year-to-year due to inflation adjustments.
        // Source: https://www.irs.gov/pub/irs-pdf/p15t.pdf

        var singleBrackets = new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
        {
            (0, 6_000, 0.00m, 0),
            (6_000, 17_600, 0.10m, 0),
            (17_600, 53_150, 0.12m, 1_160),
            (53_150, 106_525, 0.22m, 5_426),
            (106_525, 197_950, 0.24m, 17_168.50m),
            (197_950, 249_725, 0.32m, 39_110.50m),
            (249_725, 615_350, 0.35m, 55_678.50m),
            (615_350, decimal.MaxValue, 0.37m, 183_647.25m),
        };

        foreach (var (start, end, rate, baseAmt) in singleBrackets)
        {
            _db.TaxTables.Add(new TaxTable
            {
                TaxYear = taxYear,
                Type = TaxType.Federal,
                FilingStatus = FilingStatus.Single,
                BracketStart = start,
                BracketEnd = end,
                Rate = rate,
                BaseAmount = baseAmt
            });
        }

        var marriedBrackets = new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
        {
            (0, 16_300, 0.00m, 0),
            (16_300, 39_500, 0.10m, 0),
            (39_500, 110_600, 0.12m, 2_320),
            (110_600, 217_350, 0.22m, 10_852),
            (217_350, 400_200, 0.24m, 34_337),
            (400_200, 503_750, 0.32m, 78_221),
            (503_750, 747_500, 0.35m, 111_357),
            (747_500, decimal.MaxValue, 0.37m, 196_669.50m),
        };

        foreach (var (start, end, rate, baseAmt) in marriedBrackets)
        {
            _db.TaxTables.Add(new TaxTable
            {
                TaxYear = taxYear,
                Type = TaxType.Federal,
                FilingStatus = FilingStatus.Married,
                BracketStart = start,
                BracketEnd = end,
                Rate = rate,
                BaseAmount = baseAmt
            });
        }
    }

    private void SeedOhioTaxTables(int taxYear)
    {
        // Ohio IT 1040 Withholding Tables
        // WARNING: These values MUST be validated against the Ohio Department of Taxation
        // Source: https://tax.ohio.gov

        var ohioBrackets = new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
        {
            (0, 26_050, 0.0000m, 0),
            (26_050, 100_000, 0.0275m, 0),
            (100_000, decimal.MaxValue, 0.03125m, 2_033.63m),
        };

        foreach (var filing in new[] { FilingStatus.Single, FilingStatus.Married })
        {
            foreach (var (start, end, rate, baseAmt) in ohioBrackets)
            {
                _db.TaxTables.Add(new TaxTable
                {
                    TaxYear = taxYear,
                    Type = TaxType.Ohio,
                    FilingStatus = filing,
                    BracketStart = start,
                    BracketEnd = end,
                    Rate = rate,
                    BaseAmount = baseAmt
                });
            }
        }
    }
}

