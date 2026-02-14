using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Documents.Reports;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;
using QuestPDF.Fluent;

namespace OhioPayroll.App.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;

    // Social Security wage base - must be updated annually per IRS guidelines
    private const decimal SsWageBase = 176100m;

    [ObservableProperty]
    private string _selectedReportType = "Payroll Summary";

    [ObservableProperty]
    private DateTimeOffset _startDate = new(new DateTime(DateTime.Now.Year, 1, 1));

    [ObservableProperty]
    private DateTimeOffset _endDate = new(DateTime.Today);

    [ObservableProperty]
    private int _selectedYear = DateTime.Now.Year;

    [ObservableProperty]
    private int _selectedQuarter = (DateTime.Now.Month - 1) / 3 + 1;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _statusMessage;

    public List<string> ReportTypes { get; } = new()
    {
        "Payroll Summary",
        "Tax Liability",
        "YTD Employee",
        "Check Register",
        "Form 941 Worksheet"
    };

    public List<int> AvailableYears { get; private set; } = new();
    public List<int> AvailableQuarters { get; } = new() { 1, 2, 3, 4 };

    public bool ShowDateRange => SelectedReportType is "Payroll Summary" or "Check Register";
    public bool ShowYearFilter => SelectedReportType is "Tax Liability" or "YTD Employee" or "Form 941 Worksheet";
    public bool ShowQuarterFilter => SelectedReportType is "Form 941 Worksheet";

    public ReportsViewModel(PayrollDbContext db)
    {
        _db = db;
        _ = LoadAvailableYearsAsync();
    }

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowDateRange));
        OnPropertyChanged(nameof(ShowYearFilter));
        OnPropertyChanged(nameof(ShowQuarterFilter));
        StatusMessage = null;
    }

    private async Task LoadAvailableYearsAsync()
    {
        try
        {
            var years = await _db.PayrollRuns
                .AsNoTracking()
                .Select(r => r.PayDate.Year)
                .Distinct()
                .ToListAsync();

            if (!years.Contains(DateTime.Now.Year))
                years.Add(DateTime.Now.Year);

            years.Sort();
            years.Reverse();

            AvailableYears = years;
            OnPropertyChanged(nameof(AvailableYears));
        }
        catch
        {
            AvailableYears = new List<int> { DateTime.Now.Year };
            OnPropertyChanged(nameof(AvailableYears));
        }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating report...";

        try
        {
            var filePath = SelectedReportType switch
            {
                "Payroll Summary" => await GeneratePayrollSummaryAsync(),
                "Tax Liability" => await GenerateTaxLiabilityAsync(),
                "YTD Employee" => await GenerateYtdEmployeeAsync(),
                "Check Register" => await GenerateCheckRegisterAsync(),
                "Form 941 Worksheet" => await GenerateForm941Async(),
                _ => throw new InvalidOperationException($"Unknown report type: {SelectedReportType}")
            };

            StatusMessage = $"Report saved to: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating report: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private string GetOutputDirectory(string subfolder = "Reports")
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "OhioPayroll",
            subfolder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private async Task<string> GeneratePayrollSummaryAsync()
    {
        var start = StartDate.DateTime;
        var end = EndDate.DateTime;

        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
        var companyName = company?.CompanyName ?? "Ohio Payroll";

        var runs = await _db.PayrollRuns
            .AsNoTracking()
            .Where(r => r.Status == PayrollRunStatus.Finalized
                && r.PayDate >= start
                && r.PayDate <= end)
            .OrderBy(r => r.PayDate)
            .ToListAsync();

        var summaryRuns = new List<PayrollRunSummary>();
        foreach (var run in runs)
        {
            var paycheckCount = await _db.Paychecks
                .AsNoTracking()
                .CountAsync(p => p.PayrollRunId == run.Id && !p.IsVoid);

            summaryRuns.Add(new PayrollRunSummary
            {
                RunId = run.Id,
                PayDate = run.PayDate,
                EmployeeCount = paycheckCount,
                GrossPay = run.TotalGrossPay,
                FederalTax = run.TotalFederalTax,
                StateTax = run.TotalStateTax,
                LocalTax = run.TotalLocalTax,
                SocialSecurity = run.TotalSocialSecurity,
                Medicare = run.TotalMedicare,
                EmployerTaxes = run.TotalEmployerSocialSecurity + run.TotalEmployerMedicare
                    + run.TotalEmployerFuta + run.TotalEmployerSuta,
                NetPay = run.TotalNetPay
            });
        }

        var data = new PayrollSummaryData
        {
            CompanyName = companyName,
            ReportDate = DateTime.Now,
            PeriodStart = start,
            PeriodEnd = end,
            Runs = summaryRuns
        };

        var doc = new PayrollSummaryDocument(data);
        var fileName = $"PayrollSummary_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf";
        var filePath = Path.Combine(GetOutputDirectory(), fileName);
        doc.GeneratePdf(filePath);
        return filePath;
    }

    private async Task<string> GenerateTaxLiabilityAsync()
    {
        var year = SelectedYear;
        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
        var companyName = company?.CompanyName ?? "Ohio Payroll";

        var liabilities = await _db.TaxLiabilities
            .AsNoTracking()
            .Where(t => t.TaxYear == year)
            .OrderBy(t => t.TaxType)
            .ThenBy(t => t.Quarter)
            .ToListAsync();

        var lines = liabilities.Select(t => new TaxLiabilityLine
        {
            TaxTypeName = FormatTaxType(t.TaxType),
            Quarter = t.Quarter,
            AmountOwed = t.AmountOwed,
            AmountPaid = t.AmountPaid,
            Status = t.Status.ToString()
        }).ToList();

        var data = new TaxLiabilityReportData
        {
            CompanyName = companyName,
            TaxYear = year,
            Lines = lines
        };

        var doc = new TaxLiabilityReportDocument(data);
        var fileName = $"TaxLiability_{year}.pdf";
        var filePath = Path.Combine(GetOutputDirectory(), fileName);
        doc.GeneratePdf(filePath);
        return filePath;
    }

    private async Task<string> GenerateYtdEmployeeAsync()
    {
        var year = SelectedYear;
        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
        var companyName = company?.CompanyName ?? "Ohio Payroll";

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive || e.Paychecks.Any(p =>
                p.PayrollRun.PayDate.Year == year
                && p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid))
            .ToListAsync();

        var lines = new List<YtdEmployeeLine>();
        foreach (var emp in employees)
        {
            var paychecks = await _db.Paychecks
                .AsNoTracking()
                .Where(p => p.EmployeeId == emp.Id
                    && p.PayrollRun.PayDate.Year == year
                    && p.PayrollRun.Status == PayrollRunStatus.Finalized
                    && !p.IsVoid)
                .ToListAsync();

            if (paychecks.Count == 0) continue;

            lines.Add(new YtdEmployeeLine
            {
                EmployeeName = $"{emp.LastName}, {emp.FirstName}",
                SsnLast4 = emp.SsnLast4,
                GrossPay = paychecks.Sum(p => p.GrossPay),
                FederalTax = paychecks.Sum(p => p.FederalWithholding),
                StateTax = paychecks.Sum(p => p.OhioStateWithholding),
                LocalTax = paychecks.Sum(p => p.LocalMunicipalityTax) + paychecks.Sum(p => p.SchoolDistrictTax),
                SocialSecurity = paychecks.Sum(p => p.SocialSecurityTax),
                Medicare = paychecks.Sum(p => p.MedicareTax),
                NetPay = paychecks.Sum(p => p.NetPay)
            });
        }

        var data = new YtdEmployeeReportData
        {
            CompanyName = companyName,
            TaxYear = year,
            Employees = lines.OrderBy(l => l.EmployeeName).ToList()
        };

        var doc = new YtdEmployeeReportDocument(data);
        var fileName = $"YtdEmployee_{year}.pdf";
        var filePath = Path.Combine(GetOutputDirectory(), fileName);
        doc.GeneratePdf(filePath);
        return filePath;
    }

    private async Task<string> GenerateCheckRegisterAsync()
    {
        var start = StartDate.DateTime;
        var end = EndDate.DateTime;

        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
        var companyName = company?.CompanyName ?? "Ohio Payroll";

        var entries = await _db.CheckRegister
            .AsNoTracking()
            .Include(c => c.Paycheck)
                .ThenInclude(p => p.Employee)
            .Where(c => c.IssuedDate >= start && c.IssuedDate <= end)
            .OrderBy(c => c.CheckNumber)
            .ToListAsync();

        decimal runningTotal = 0m;
        var lines = entries.Select(e =>
        {
            if (e.Status != CheckStatus.Voided)
                runningTotal += e.Amount;

            return new CheckRegisterLine
            {
                CheckNumber = e.CheckNumber,
                Date = e.IssuedDate,
                PayeeName = e.Paycheck?.Employee?.FullName ?? "Unknown",
                Amount = e.Amount,
                Status = e.Status.ToString(),
                RunningTotal = runningTotal
            };
        }).ToList();

        var data = new CheckRegisterReportData
        {
            CompanyName = companyName,
            StartDate = start,
            EndDate = end,
            Entries = lines
        };

        var doc = new CheckRegisterReportDocument(data);
        var fileName = $"CheckRegister_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf";
        var filePath = Path.Combine(GetOutputDirectory(), fileName);
        doc.GeneratePdf(filePath);
        return filePath;
    }

    private async Task<string> GenerateForm941Async()
    {
        var year = SelectedYear;
        var quarter = SelectedQuarter;
        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();

        var (qStart, qEnd) = GetQuarterDates(year, quarter);

        // Get all finalized paychecks in the quarter
        var paychecks = await _db.Paychecks
            .AsNoTracking()
            .Include(p => p.PayrollRun)
            .Where(p => p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid
                && p.PayrollRun.PayDate >= qStart
                && p.PayrollRun.PayDate <= qEnd)
            .ToListAsync();

        // Line 1: Number of employees who received wages
        var employeeCount = paychecks.Select(p => p.EmployeeId).Distinct().Count();

        // Line 2: Total wages
        var totalWages = paychecks.Sum(p => p.GrossPay);

        // Line 3: Federal tax withheld
        var federalTax = paychecks.Sum(p => p.FederalWithholding);

        // Line 5a: Taxable SS wages (capped at $176,100 per employee)
        var employeeGroups = paychecks.GroupBy(p => p.EmployeeId);
        decimal taxableSsWages = 0m;
        foreach (var group in employeeGroups)
        {
            var empGross = group.Sum(p => p.GrossPay);
            taxableSsWages += Math.Min(empGross, SsWageBase);
        }

        var ssTaxDue = taxableSsWages * 0.124m;

        // Line 5c: Taxable Medicare wages
        var taxableMedicareWages = totalWages;
        var medicareTaxDue = taxableMedicareWages * 0.029m;

        // Line 6: Total SS + Medicare
        var totalSsAndMedicare = ssTaxDue + medicareTaxDue;

        // Line 10: Total taxes
        var line10 = federalTax + totalSsAndMedicare;

        // Line 11: Total deposits
        var deposits = await _db.TaxLiabilities
            .AsNoTracking()
            .Where(t => t.TaxYear == year
                && t.Quarter == quarter
                && t.Status == TaxLiabilityStatus.Paid)
            .SumAsync(t => t.AmountPaid);

        // Line 14: Balance due or overpayment
        var line14 = line10 - deposits;

        var data = new Form941Data
        {
            TaxYear = year,
            Quarter = quarter,
            EmployerEin = company?.Ein ?? "",
            EmployerName = company?.CompanyName ?? "Ohio Payroll",
            NumberOfEmployees = employeeCount,
            Line2_WagesTipsCompensation = totalWages,
            Line3_FederalTaxWithheld = federalTax,
            Line5a_TaxableSsWages = taxableSsWages,
            Line5a_SsTaxDue = ssTaxDue,
            Line5c_TaxableMedicareWages = taxableMedicareWages,
            Line5c_MedicareTaxDue = medicareTaxDue,
            Line6_TotalTaxesBeforeAdjustments = totalSsAndMedicare,
            Line10_TotalTaxes = line10,
            Line11_TotalDeposits = deposits,
            Line14_BalanceDueOrOverpayment = line14
        };

        var doc = new Form941WorksheetDocument(data);
        var fileName = $"Form941_Worksheet_{year}_Q{quarter}.pdf";
        var filePath = Path.Combine(GetOutputDirectory(), fileName);
        doc.GeneratePdf(filePath);
        return filePath;
    }

    private static (DateTime start, DateTime end) GetQuarterDates(int year, int quarter)
    {
        return quarter switch
        {
            1 => (new DateTime(year, 1, 1), new DateTime(year, 3, 31)),
            2 => (new DateTime(year, 4, 1), new DateTime(year, 6, 30)),
            3 => (new DateTime(year, 7, 1), new DateTime(year, 9, 30)),
            4 => (new DateTime(year, 10, 1), new DateTime(year, 12, 31)),
            _ => throw new ArgumentOutOfRangeException(nameof(quarter))
        };
    }

    private static string FormatTaxType(TaxType type)
    {
        return type switch
        {
            TaxType.Federal => "Federal Income Tax",
            TaxType.Ohio => "Ohio State Income Tax",
            TaxType.SchoolDistrict => "School District Tax",
            TaxType.Local => "Local / Municipal Tax",
            TaxType.FICA_SS => "Social Security (FICA)",
            TaxType.FICA_Med => "Medicare (FICA)",
            TaxType.FUTA => "Federal Unemployment (FUTA)",
            TaxType.SUTA => "State Unemployment (SUTA)",
            _ => type.ToString()
        };
    }
}

