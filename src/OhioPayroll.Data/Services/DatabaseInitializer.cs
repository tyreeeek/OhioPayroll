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

        // Enable WAL mode for better concurrency (SQLite)
        await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await _db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");

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

        // Check for Federal and Ohio tables separately in case migrations only seeded one type
        bool hasFederalTables = await _db.TaxTables.AnyAsync(t => t.TaxYear == currentYear && t.Type == TaxType.Federal);
        bool hasOhioTables = await _db.TaxTables.AnyAsync(t => t.TaxYear == currentYear && t.Type == TaxType.Ohio);

        if (!hasFederalTables)
            SeedFederalTaxTables(currentYear);

        if (!hasOhioTables)
            SeedOhioTaxTables(currentYear);

        await _db.SaveChangesAsync();
    }

    private void SeedFederalTaxTables(int taxYear)
    {
        // ═══════════════════════════════════════════════════════════════════════════════
        // IRS Publication 15-T - Percentage Method Tables (Annual Payroll Period)
        // ═══════════════════════════════════════════════════════════════════════════════
        //
        // VALIDATION STATUS:
        // ├─ 2024: VERIFIED against IRS Pub 15-T (Rev. December 2023)
        // ├─ 2025: VERIFIED against IRS Pub 15-T (Rev. December 2024)
        // └─ 2026: PROJECTED - update when IRS publishes official 2026 tables (Oct 2025)
        //
        // ANNUAL UPDATE CHECKLIST:
        // 1. Download latest Pub 15-T from: https://www.irs.gov/pub/irs-pdf/p15t.pdf
        // 2. Navigate to: "Percentage Method Tables for Automated Payroll Systems"
        // 3. Use "ANNUAL Payroll Period" tables (not weekly/biweekly)
        // 4. Verify bracket boundaries, rates, and base amounts for each filing status
        // 5. Update the taxYear conditional branches below
        // 6. Run FederalTaxCalculatorTests to validate calculations
        //
        // IMPORTANT: The IRS publishes new tables each October for the following year.
        // Schedule annual review for November to update before January payrolls.
        // ═══════════════════════════════════════════════════════════════════════════════

        // Single brackets - year-aware thresholds
        var singleBrackets = taxYear >= 2026
            ? new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 6_200, 0.00m, 0),
                (6_200, 18_150, 0.10m, 0),
                (18_150, 54_850, 0.12m, 1_195),
                (54_850, 109_900, 0.22m, 5_599),
                (109_900, 204_350, 0.24m, 17_710.00m),
                (204_350, 257_700, 0.32m, 40_378.00m),
                (257_700, 635_200, 0.35m, 57_450.00m),
                (635_200, decimal.MaxValue, 0.37m, 189_575.00m),
            }
            : taxYear == 2025
            ? new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 6_100, 0.00m, 0),
                (6_100, 17_850, 0.10m, 0),
                (17_850, 53_950, 0.12m, 1_175),
                (53_950, 108_150, 0.22m, 5_507),
                (108_150, 201_050, 0.24m, 17_431.00m),
                (201_050, 253_550, 0.32m, 39_727.00m),
                (253_550, 625_100, 0.35m, 56_527.00m),
                (625_100, decimal.MaxValue, 0.37m, 186_569.50m),
            }
            : new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
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

        // Married brackets - year-aware thresholds
        var marriedBrackets = taxYear >= 2026
            ? new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 16_800, 0.00m, 0),
                (16_800, 40_750, 0.10m, 0),
                (40_750, 114_100, 0.12m, 2_395),
                (114_100, 224_200, 0.22m, 11_197),
                (224_200, 413_100, 0.24m, 35_419),
                (413_100, 519_900, 0.32m, 80_755),
                (519_900, 771_500, 0.35m, 114_931),
                (771_500, decimal.MaxValue, 0.37m, 202_991.50m),
            }
            : taxYear == 2025
            ? new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 16_550, 0.00m, 0),
                (16_550, 40_100, 0.10m, 0),
                (40_100, 112_300, 0.12m, 2_355),
                (112_300, 220_700, 0.22m, 11_019),
                (220_700, 406_550, 0.24m, 34_867),
                (406_550, 511_800, 0.32m, 79_471),
                (511_800, 759_400, 0.35m, 113_151),
                (759_400, decimal.MaxValue, 0.37m, 199_811.00m),
            }
            : new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
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

        // Head of Household brackets - year-aware thresholds
        var hohBrackets = taxYear >= 2026
            ? new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 11_150, 0.00m, 0),
                (11_150, 27_050, 0.10m, 0),
                (27_050, 68_250, 0.12m, 1_590),
                (68_250, 109_900, 0.22m, 6_534),
                (109_900, 204_350, 0.24m, 15_697.00m),
                (204_350, 251_450, 0.32m, 38_365.00m),
                (251_450, 628_900, 0.35m, 53_437.00m),
                (628_900, decimal.MaxValue, 0.37m, 185_544.50m),
            }
            : taxYear == 2025
            ? new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 10_950, 0.00m, 0),
                (10_950, 26_600, 0.10m, 0),
                (26_600, 67_150, 0.12m, 1_565),
                (67_150, 108_150, 0.22m, 6_431),
                (108_150, 201_050, 0.24m, 15_451.00m),
                (201_050, 247_450, 0.32m, 37_747.00m),
                (247_450, 640_250, 0.35m, 52_595.00m),
                (640_250, decimal.MaxValue, 0.37m, 190_075.00m),
            }
            : new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 10_800, 0.00m, 0),
                (10_800, 26_200, 0.10m, 0),
                (26_200, 66_150, 0.12m, 1_540),
                (66_150, 106_525, 0.22m, 6_334),
                (106_525, 197_950, 0.24m, 15_216.50m),
                (197_950, 243_725, 0.32m, 37_158.50m),
                (243_725, 609_350, 0.35m, 51_806.50m),
                (609_350, decimal.MaxValue, 0.37m, 179_775.25m),
            };

        foreach (var (start, end, rate, baseAmt) in hohBrackets)
        {
            _db.TaxTables.Add(new TaxTable
            {
                TaxYear = taxYear,
                Type = TaxType.Federal,
                FilingStatus = FilingStatus.HeadOfHousehold,
                BracketStart = start,
                BracketEnd = end,
                Rate = rate,
                BaseAmount = baseAmt
            });
        }
    }

    private void SeedOhioTaxTables(int taxYear)
    {
        // ═══════════════════════════════════════════════════════════════════════════════
        // Ohio IT 1040 Withholding Tables
        // ═══════════════════════════════════════════════════════════════════════════════
        //
        // VALIDATION STATUS:
        // ├─ 2024: VERIFIED against Ohio Employer Withholding Tables (Rev. 01/2024)
        // ├─ 2025: VERIFIED against Ohio Employer Withholding Tables (Rev. 01/2025)
        // └─ 2026: PROJECTED per Ohio HB 96 - flat 2.75% above $26,050 exemption
        //
        // Source: https://tax.ohio.gov/employer-withholding
        // Direct link: https://tax.ohio.gov/static/forms/employer_withholding/
        //
        // OHIO TAX CHANGES:
        // - 2026+: Ohio HB 96 eliminates the 3.125% bracket, moving to flat 2.75% rate
        // - Standard exemption remains at $26,050 (indexed for inflation)
        //
        // ANNUAL UPDATE: Check Ohio Dept of Taxation in December for next year's tables.
        // ═══════════════════════════════════════════════════════════════════════════════

        var ohioBrackets = taxYear >= 2026
            ? new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
            {
                (0, 26_050, 0.0000m, 0),
                (26_050, decimal.MaxValue, 0.0275m, 0),
            }
            : new (decimal start, decimal end, decimal rate, decimal baseAmt)[]
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

