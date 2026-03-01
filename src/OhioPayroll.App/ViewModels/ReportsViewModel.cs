using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Documents.Reports;
using OhioPayroll.App.Extensions;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;
using OhioPayroll.Engine.Calculators;
using QuestPDF.Fluent;

namespace OhioPayroll.App.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;

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
        ExecuteWithLoadingAsync(LoadAvailableYearsAsync, "Loading...")
            .FireAndForgetSafeAsync(errorContext: "loading available years");
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
            OpenFolder(Path.GetDirectoryName(filePath)!);
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

    private static void OpenFolder(string path) => PlatformHelper.OpenFolder(path);

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

        // Batch-load paycheck counts to avoid N+1 queries
        var runIds = runs.Select(r => r.Id).ToList();
        var paycheckCounts = await _db.Paychecks
            .AsNoTracking()
            .Where(p => runIds.Contains(p.PayrollRunId) && !p.IsVoid)
            .GroupBy(p => p.PayrollRunId)
            .Select(g => new { RunId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RunId, x => x.Count);

        var summaryRuns = runs.Select(run => new PayrollRunSummary
        {
            RunId = run.Id,
            PayDate = run.PayDate,
            EmployeeCount = paycheckCounts.GetValueOrDefault(run.Id, 0),
            GrossPay = run.TotalGrossPay,
            FederalTax = run.TotalFederalTax,
            StateTax = run.TotalStateTax,
            LocalTax = run.TotalLocalTax,
            SocialSecurity = run.TotalSocialSecurity,
            Medicare = run.TotalMedicare,
            EmployerTaxes = run.TotalEmployerSocialSecurity + run.TotalEmployerMedicare
                + run.TotalEmployerFuta + run.TotalEmployerSuta,
            NetPay = run.TotalNetPay
        }).ToList();

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

        // Batch-load employees with their paychecks to avoid N+1 queries
        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive || e.Paychecks.Any(p =>
                p.PayrollRun.PayDate.Year == year
                && p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid))
            .Include(e => e.Paychecks.Where(p =>
                p.PayrollRun.PayDate.Year == year
                && p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid))
            .ToListAsync();

        var lines = employees
            .Where(emp => emp.Paychecks.Count > 0)
            .Select(emp => new YtdEmployeeLine
            {
                EmployeeName = $"{emp.LastName}, {emp.FirstName}",
                SsnLast4 = emp.SsnLast4,
                GrossPay = emp.Paychecks.Sum(p => p.GrossPay),
                FederalTax = emp.Paychecks.Sum(p => p.FederalWithholding),
                StateTax = emp.Paychecks.Sum(p => p.OhioStateWithholding),
                LocalTax = emp.Paychecks.Sum(p => p.LocalMunicipalityTax) + emp.Paychecks.Sum(p => p.SchoolDistrictTax),
                SocialSecurity = emp.Paychecks.Sum(p => p.SocialSecurityTax),
                Medicare = emp.Paychecks.Sum(p => p.MedicareTax),
                NetPay = emp.Paychecks.Sum(p => p.NetPay)
            })
            .ToList();

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
            .Include(c => c.Paycheck!)
                .ThenInclude(p => p.Employee)
            .Include(c => c.ContractorPayment!)
                .ThenInclude(p => p.Contractor)
            .Where(c => c.IssuedDate >= start && c.IssuedDate <= end)
            .OrderBy(c => c.CheckNumber)
            .ToListAsync();

        decimal runningTotal = 0m;
        var lines = entries.Select(e =>
        {
            if (e.Status != CheckStatus.Voided)
                runningTotal += e.Amount;

            // Handle both employee paychecks and contractor payments
            var payeeName = e.Paycheck?.Employee?.FullName
                           ?? e.ContractorPayment?.Contractor?.Name
                           ?? e.ContractorPayment?.ContractorNameSnapshot
                           ?? "Unknown";

            return new CheckRegisterLine
            {
                CheckNumber = e.CheckNumber,
                Date = e.IssuedDate,
                PayeeName = payeeName,
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

        var (qStart, qEnd) = TaxCalendarHelper.GetQuarterDates(year, quarter);

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

        // Line 5a: Taxable SS wages (capped per employee at the year's wage base)
        if (year < 2020)
        {
            throw new InvalidOperationException(
                $"Form 941 Worksheet cannot be generated for tax year {year}. " +
                $"Social Security wage base data is only available for years 2020 and later.");
        }
        var ssWageBase = FicaCalculator.GetSocialSecurityWageCap(year);
        var employeeGroups = paychecks.GroupBy(p => p.EmployeeId);
        decimal taxableSsWages = 0m;
        foreach (var group in employeeGroups)
        {
            var empGross = group.Sum(p => p.GrossPay);
            taxableSsWages += Math.Min(empGross, ssWageBase);
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

