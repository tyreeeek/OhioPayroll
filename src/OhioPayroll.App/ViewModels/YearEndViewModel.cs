using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Documents;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;
using QuestPDF.Fluent;

namespace OhioPayroll.App.ViewModels;

public class YearEndEmployeeRow
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string SsnLast4 { get; set; } = string.Empty;
    public decimal TotalWages { get; set; }
    public decimal FederalTax { get; set; }
    public decimal StateTax { get; set; }
    public decimal SsTax { get; set; }
    public decimal MedicareTax { get; set; }
    public decimal LocalTax { get; set; }

    public string TotalWagesDisplay => TotalWages.ToString("C");
    public string FederalTaxDisplay => FederalTax.ToString("C");
    public string StateTaxDisplay => StateTax.ToString("C");
    public string SsTaxDisplay => SsTax.ToString("C");
    public string MedicareTaxDisplay => MedicareTax.ToString("C");
    public string LocalTaxDisplay => LocalTax.ToString("C");
}

public partial class YearEndViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;

    // Social Security wage base - must be updated annually per IRS guidelines
    private const decimal SsWageBase = 176100m;

    [ObservableProperty]
    private string _title = "Year-End / W-2";

    [ObservableProperty]
    private string _subtitle = "Generate W-2 forms and year-end tax documents.";

    [ObservableProperty]
    private int _selectedYear = DateTime.Now.Year;

    [ObservableProperty]
    private ObservableCollection<YearEndEmployeeRow> _employeeRows = new();

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _statusMessage;

    // Summary card values
    [ObservableProperty]
    private string _employeeCountDisplay = "0";

    [ObservableProperty]
    private string _totalWagesDisplay = "$0.00";

    [ObservableProperty]
    private string _totalFederalTaxDisplay = "$0.00";

    [ObservableProperty]
    private string _totalSsTaxDisplay = "$0.00";

    [ObservableProperty]
    private string _totalMedicareTaxDisplay = "$0.00";

    [ObservableProperty]
    private string _totalStateTaxDisplay = "$0.00";

    public List<int> AvailableYears { get; private set; } = new();

    public YearEndViewModel(PayrollDbContext db, IEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
        _ = InitializeAsync();
    }

    partial void OnSelectedYearChanged(int value)
    {
        StatusMessage = null;
        _ = LoadEmployeeDataAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadAvailableYearsAsync();
        await LoadEmployeeDataAsync();
    }

    private async Task LoadAvailableYearsAsync()
    {
        try
        {
            var years = await _db.PayrollRuns
                .AsNoTracking()
                .Where(r => r.Status == PayrollRunStatus.Finalized)
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

    private async Task LoadEmployeeDataAsync()
    {
        try
        {
            var year = SelectedYear;

            var employees = await _db.Employees
                .AsNoTracking()
                .Where(e => e.Paychecks.Any(p =>
                    p.PayrollRun.PayDate.Year == year
                    && p.PayrollRun.Status == PayrollRunStatus.Finalized
                    && !p.IsVoid))
                .ToListAsync();

            var rows = new List<YearEndEmployeeRow>();

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

                rows.Add(new YearEndEmployeeRow
                {
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.LastName}, {emp.FirstName}",
                    SsnLast4 = emp.SsnLast4,
                    TotalWages = paychecks.Sum(p => p.GrossPay),
                    FederalTax = paychecks.Sum(p => p.FederalWithholding),
                    StateTax = paychecks.Sum(p => p.OhioStateWithholding),
                    SsTax = paychecks.Sum(p => p.SocialSecurityTax),
                    MedicareTax = paychecks.Sum(p => p.MedicareTax),
                    LocalTax = paychecks.Sum(p => p.LocalMunicipalityTax) + paychecks.Sum(p => p.SchoolDistrictTax)
                });
            }

            rows = rows.OrderBy(r => r.EmployeeName).ToList();
            EmployeeRows = new ObservableCollection<YearEndEmployeeRow>(rows);

            // Update summary cards
            EmployeeCountDisplay = rows.Count.ToString();
            TotalWagesDisplay = rows.Sum(r => r.TotalWages).ToString("C");
            TotalFederalTaxDisplay = rows.Sum(r => r.FederalTax).ToString("C");
            TotalSsTaxDisplay = rows.Sum(r => r.SsTax).ToString("C");
            TotalMedicareTaxDisplay = rows.Sum(r => r.MedicareTax).ToString("C");
            TotalStateTaxDisplay = rows.Sum(r => r.StateTax).ToString("C");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
        }
    }

    private string GetOutputDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "OhioPayroll",
            "YearEnd");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [RelayCommand]
    private async Task GenerateW2sAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating W-2 forms...";

        try
        {
            var w2DataList = await BuildW2DataListAsync();
            var outputDir = GetOutputDirectory();
            var generatedFiles = new List<string>();

            foreach (var w2Data in w2DataList)
            {
                var doc = new W2Document(w2Data);
                var fileName = $"W2_{w2Data.TaxYear}_{w2Data.EmployeeLastName}_{w2Data.EmployeeFirstName}.pdf";
                var filePath = Path.Combine(outputDir, fileName);
                doc.GeneratePdf(filePath);
                generatedFiles.Add(fileName);
            }

            AppLogger.Information($"Generated {generatedFiles.Count} W-2 form(s) for tax year {SelectedYear}");
            StatusMessage = $"Generated {generatedFiles.Count} W-2 form(s) in: {outputDir}";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating W-2s: {ex.Message}", ex);
            StatusMessage = $"Error generating W-2s: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateW3Async()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating W-3 form...";

        try
        {
            var w2DataList = await BuildW2DataListAsync();
            var w3Data = BuildW3Data(w2DataList);

            var doc = new W3Document(w3Data);
            var fileName = $"W3_{SelectedYear}.pdf";
            var filePath = Path.Combine(GetOutputDirectory(), fileName);
            doc.GeneratePdf(filePath);

            AppLogger.Information($"Generated W-3 for tax year {SelectedYear}");
            StatusMessage = $"W-3 saved to: {filePath}";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating W-3: {ex.Message}", ex);
            StatusMessage = $"Error generating W-3: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateAllAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating all year-end documents...";

        try
        {
            var w2DataList = await BuildW2DataListAsync();
            var outputDir = GetOutputDirectory();

            // Generate W-2s
            int w2Count = 0;
            foreach (var w2Data in w2DataList)
            {
                var doc = new W2Document(w2Data);
                var fileName = $"W2_{w2Data.TaxYear}_{w2Data.EmployeeLastName}_{w2Data.EmployeeFirstName}.pdf";
                var filePath = Path.Combine(outputDir, fileName);
                doc.GeneratePdf(filePath);
                w2Count++;
            }

            // Generate W-3
            var w3Data = BuildW3Data(w2DataList);
            var w3Doc = new W3Document(w3Data);
            var w3FileName = $"W3_{SelectedYear}.pdf";
            var w3FilePath = Path.Combine(outputDir, w3FileName);
            w3Doc.GeneratePdf(w3FilePath);

            AppLogger.Information($"Generated {w2Count} W-2(s) and 1 W-3 for tax year {SelectedYear}");
            StatusMessage = $"Generated {w2Count} W-2(s) and 1 W-3 in: {outputDir}";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating year-end documents: {ex.Message}", ex);
            StatusMessage = $"Error generating year-end documents: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task<List<W2Data>> BuildW2DataListAsync()
    {
        var year = SelectedYear;
        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.Paychecks.Any(p =>
                p.PayrollRun.PayDate.Year == year
                && p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid))
            .ToListAsync();

        var w2List = new List<W2Data>();

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

            var grossPay = paychecks.Sum(p => p.GrossPay);

            var w2 = new W2Data
            {
                EmployerEin = company?.Ein ?? "",
                StateWithholdingId = company?.StateWithholdingId ?? "",
                EmployerName = company?.CompanyName ?? "",
                EmployerAddress = company?.Address ?? "",
                EmployerCity = company?.City ?? "",
                EmployerState = company?.State ?? "OH",
                EmployerZip = company?.ZipCode ?? "",
                EmployeeSsn = _encryption.Decrypt(emp.EncryptedSsn),
                EmployeeFirstName = emp.FirstName,
                EmployeeLastName = emp.LastName,
                EmployeeAddress = emp.Address,
                EmployeeCity = emp.City,
                EmployeeState = emp.State,
                EmployeeZip = emp.ZipCode,
                Box1WagesTips = grossPay,
                Box2FederalTaxWithheld = paychecks.Sum(p => p.FederalWithholding),
                Box3SocialSecurityWages = Math.Min(grossPay, SsWageBase),
                Box4SocialSecurityTax = paychecks.Sum(p => p.SocialSecurityTax),
                Box5MedicareWages = grossPay,
                Box6MedicareTax = paychecks.Sum(p => p.MedicareTax),
                Box16StateWages = grossPay,
                Box17StateTax = paychecks.Sum(p => p.OhioStateWithholding),
                Box18LocalWages = grossPay,
                Box19LocalTax = paychecks.Sum(p => p.LocalMunicipalityTax) + paychecks.Sum(p => p.SchoolDistrictTax),
                Box20LocalityName = emp.MunicipalityCode ?? "",
                TaxYear = year
            };

            w2List.Add(w2);
        }

        return w2List.OrderBy(w => w.EmployeeLastName).ThenBy(w => w.EmployeeFirstName).ToList();
    }

    private W3Data BuildW3Data(List<W2Data> w2DataList)
    {
        var company = w2DataList.FirstOrDefault();

        return new W3Data
        {
            EmployerEin = company?.EmployerEin ?? "",
            StateWithholdingId = company?.StateWithholdingId ?? "",
            EmployerName = company?.EmployerName ?? "",
            EmployerAddress = company?.EmployerAddress ?? "",
            EmployerCity = company?.EmployerCity ?? "",
            EmployerState = company?.EmployerState ?? "OH",
            EmployerZip = company?.EmployerZip ?? "",
            NumberOfW2s = w2DataList.Count,
            TotalWages = w2DataList.Sum(w => w.Box1WagesTips),
            TotalFederalTax = w2DataList.Sum(w => w.Box2FederalTaxWithheld),
            TotalSsWages = w2DataList.Sum(w => w.Box3SocialSecurityWages),
            TotalSsTax = w2DataList.Sum(w => w.Box4SocialSecurityTax),
            TotalMedicareWages = w2DataList.Sum(w => w.Box5MedicareWages),
            TotalMedicareTax = w2DataList.Sum(w => w.Box6MedicareTax),
            TotalStateWages = w2DataList.Sum(w => w.Box16StateWages),
            TotalStateTax = w2DataList.Sum(w => w.Box17StateTax),
            TotalLocalWages = w2DataList.Sum(w => w.Box18LocalWages),
            TotalLocalTax = w2DataList.Sum(w => w.Box19LocalTax),
            TaxYear = SelectedYear
        };
    }
}

