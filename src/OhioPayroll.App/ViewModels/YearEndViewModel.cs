using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Documents;
using OhioPayroll.App.Extensions;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;
using OhioPayroll.Engine.Calculators;
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

public class YearEndContractorRow
{
    public int ContractorId { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public string TinLast4 { get; set; } = string.Empty;
    public decimal TotalPayments { get; set; }
    public bool Requires1099 { get; set; }
    public string TotalPaymentsDisplay => TotalPayments.ToString("C");
    public string StatusDisplay => Requires1099 ? "1099 Required" : (TotalPayments > 0 ? "Below $600" : "No Payments");
    public string MaskedTin => $"***-**-{TinLast4}";
}

public partial class YearEndViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;

    [ObservableProperty]
    private string _title = "Year-End / W-2 & 1099";

    [ObservableProperty]
    private string _subtitle = "Generate W-2, W-3, 1099-NEC, and 1096 year-end tax documents.";

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

    // Contractor summary values
    [ObservableProperty]
    private ObservableCollection<YearEndContractorRow> _contractorRows = new();

    [ObservableProperty]
    private string _contractorCountDisplay = "0";

    [ObservableProperty]
    private string _totalContractorPaymentsDisplay = "$0.00";

    [ObservableProperty]
    private string _form1099CountDisplay = "0";

    [ObservableProperty]
    private string? _form940ValidationWarning;

    public List<int> AvailableYears { get; private set; } = new();

    public YearEndViewModel(PayrollDbContext db, IEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
        ExecuteWithLoadingAsync(InitializeAsync, "Loading year-end data...")
            .FireAndForgetSafeAsync(errorContext: "initializing year-end data");
    }

    partial void OnSelectedYearChanged(int value)
    {
        StatusMessage = null;
        ExecuteWithLoadingAsync(LoadEmployeeDataAsync, "Loading employee data...")
            .FireAndForgetSafeAsync(errorContext: "loading employee year-end data");
        ExecuteWithLoadingAsync(LoadContractorDataAsync, "Loading contractor data...")
            .FireAndForgetSafeAsync(errorContext: "loading contractor year-end data");
    }

    private async Task InitializeAsync()
    {
        await LoadAvailableYearsAsync();
        await LoadEmployeeDataAsync();
        await LoadContractorDataAsync();
    }

    private async Task LoadAvailableYearsAsync()
    {
        try
        {
            var payrollYears = await _db.PayrollRuns
                .AsNoTracking()
                .Where(r => r.Status == PayrollRunStatus.Finalized)
                .Select(r => r.PayDate.Year)
                .Distinct()
                .ToListAsync();

            var contractorYears = await _db.ContractorPayments
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => p.TaxYear)
                .Distinct()
                .ToListAsync();

            var years = payrollYears.Union(contractorYears).ToList();

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

            // Batch-load employees with their paychecks to avoid N+1 queries
            var employees = await _db.Employees
                .AsNoTracking()
                .Where(e => e.Paychecks.Any(p =>
                    p.PayrollRun.PayDate.Year == year
                    && p.PayrollRun.Status == PayrollRunStatus.Finalized
                    && !p.IsVoid))
                .Include(e => e.Paychecks.Where(p =>
                    p.PayrollRun.PayDate.Year == year
                    && p.PayrollRun.Status == PayrollRunStatus.Finalized
                    && !p.IsVoid))
                .ToListAsync();

            var rows = employees
                .Where(emp => emp.Paychecks.Count > 0)
                .Select(emp => new YearEndEmployeeRow
                {
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.LastName}, {emp.FirstName}",
                    SsnLast4 = emp.SsnLast4,
                    TotalWages = emp.Paychecks.Sum(p => p.GrossPay),
                    FederalTax = emp.Paychecks.Sum(p => p.FederalWithholding),
                    StateTax = emp.Paychecks.Sum(p => p.OhioStateWithholding),
                    SsTax = emp.Paychecks.Sum(p => p.SocialSecurityTax),
                    MedicareTax = emp.Paychecks.Sum(p => p.MedicareTax),
                    LocalTax = emp.Paychecks.Sum(p => p.LocalMunicipalityTax) + emp.Paychecks.Sum(p => p.SchoolDistrictTax)
                })
                .OrderBy(r => r.EmployeeName)
                .ToList();

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

    private async Task LoadContractorDataAsync()
    {
        try
        {
            var year = SelectedYear;

            var contractors = await _db.Contractors
                .AsNoTracking()
                .Where(c => c.IsActive || c.Payments.Any(p => p.TaxYear == year && !p.IsDeleted))
                .Include(c => c.Payments.Where(p => p.TaxYear == year && !p.IsDeleted))
                .ToListAsync();

            var contractorRows = contractors.Select(c => new YearEndContractorRow
            {
                ContractorId = c.Id,
                ContractorName = c.Name,
                TinLast4 = c.TinLast4,
                TotalPayments = c.Payments.Where(p => !p.IsDeleted).Sum(p => p.Amount),
                Requires1099 = !c.Is1099Exempt && c.Payments.Where(p => !p.IsDeleted).Sum(p => p.Amount) >= 600
            }).OrderByDescending(r => r.TotalPayments).ToList();

            ContractorRows = new ObservableCollection<YearEndContractorRow>(contractorRows);
            ContractorCountDisplay = contractorRows.Count.ToString();
            TotalContractorPaymentsDisplay = contractorRows.Sum(r => r.TotalPayments).ToString("C");
            Form1099CountDisplay = contractorRows.Count(r => r.Requires1099).ToString();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading contractor data: {ex.Message}";
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

    private static void OpenFolder(string path) => PlatformHelper.OpenFolder(path);

    [RelayCommand]
    private async Task GenerateW2sAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating W-2 forms...";

        try
        {
            var w2DataList = await BuildW2DataListAsync();

            if (w2DataList.Count == 0)
            {
                StatusMessage = "No employees with finalized payroll found for the selected year. Run and finalize payroll first.";
                return;
            }

            var outputDir = GetOutputDirectory();
            var generatedFiles = new List<string>();

            foreach (var w2Data in w2DataList)
            {
                var doc = new W2Document(w2Data);
                var safeLast = SanitizeFileName(w2Data.EmployeeLastName);
                var safeFirst = SanitizeFileName(w2Data.EmployeeFirstName);
                var fileName = $"W2_{w2Data.TaxYear}_{safeLast}_{safeFirst}.pdf";
                var filePath = Path.Combine(outputDir, fileName);
                doc.GeneratePdf(filePath);
                generatedFiles.Add(fileName);
            }

            AppLogger.Information($"Generated {generatedFiles.Count} W-2 form(s) for tax year {SelectedYear}");
            StatusMessage = $"Generated {generatedFiles.Count} W-2 form(s) in: {outputDir}";
            OpenFolder(outputDir);
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

            if (w2DataList.Count == 0)
            {
                StatusMessage = "No employees with finalized payroll found for the selected year.";
                return;
            }

            var w3Data = BuildW3Data(w2DataList);
            var outputDir = GetOutputDirectory();
            var doc = new W3Document(w3Data);
            var fileName = $"W3_{SelectedYear}.pdf";
            var filePath = Path.Combine(outputDir, fileName);
            doc.GeneratePdf(filePath);

            AppLogger.Information($"Generated W-3 for tax year {SelectedYear}");
            StatusMessage = $"W-3 saved to: {filePath}";
            OpenFolder(outputDir);
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
    private async Task Generate1099sAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating 1099-NEC forms...";

        try
        {
            var form1099DataList = await Build1099NecDataListAsync();

            if (form1099DataList.Count == 0)
            {
                StatusMessage = "No contractors with payments >= $600 found for the selected year.";
                return;
            }

            var outputDir = GetOutputDirectory();
            var generatedFiles = new List<string>();

            foreach (var formData in form1099DataList)
            {
                var doc = new Form1099NecDocument(formData);
                var safeName = SanitizeFileName(formData.RecipientName);
                var fileName = $"1099NEC_{formData.TaxYear}_{safeName}.pdf";
                var filePath = Path.Combine(outputDir, fileName);
                doc.GeneratePdf(filePath);
                generatedFiles.Add(fileName);
            }

            AppLogger.Information($"Generated {generatedFiles.Count} 1099-NEC form(s) for tax year {SelectedYear}");
            StatusMessage = $"Generated {generatedFiles.Count} 1099-NEC form(s) in: {outputDir}";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating 1099-NECs: {ex.Message}", ex);
            StatusMessage = $"Error generating 1099-NECs: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task Generate1096Async()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating 1096 form...";

        try
        {
            var form1099DataList = await Build1099NecDataListAsync();

            if (form1099DataList.Count == 0)
            {
                StatusMessage = "No contractors with payments >= $600 found for the selected year. 1096 requires at least one 1099-NEC.";
                return;
            }

            var form1096Data = Build1096Data(form1099DataList);
            var outputDir = GetOutputDirectory();
            var doc = new Form1096Document(form1096Data);
            var fileName = $"1096_{SelectedYear}.pdf";
            var filePath = Path.Combine(outputDir, fileName);
            doc.GeneratePdf(filePath);

            AppLogger.Information($"Generated 1096 for tax year {SelectedYear}");
            StatusMessage = $"1096 saved to: {filePath}";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating 1096: {ex.Message}", ex);
            StatusMessage = $"Error generating 1096: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateForm940Async()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating Form 940...";

        try
        {
            var form940Data = await BuildForm940DataAsync();
            var outputDir = GetOutputDirectory();
            var doc = new Form940Document(form940Data);
            var fileName = $"Form940_{SelectedYear}.pdf";
            var filePath = Path.Combine(outputDir, fileName);
            doc.GeneratePdf(filePath);

            AppLogger.Information($"Generated Form 940 for tax year {SelectedYear}");
            StatusMessage = $"Form 940 saved to: {filePath}";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating Form 940: {ex.Message}", ex);
            StatusMessage = $"Error generating Form 940: {ex.Message}";
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
        StatusMessage = "Generating all year-end documents (official IRS forms)...";

        try
        {
            var w2DataList = await BuildW2DataListAsync();
            var outputDir = GetOutputDirectory();

            // Generate W-2s (Official IRS Forms)
            int w2Count = 0;
            if (w2DataList.Count > 0)
            {
                var w2Filler = new Services.PdfFormFillers.W2FormFiller();
                var w2Files = w2Filler.FillAndSaveMultiple(
                    w2DataList,
                    outputDir,
                    w2Data =>
                    {
                        var safeLast = SanitizeFileName(w2Data.EmployeeLastName);
                        var safeFirst = SanitizeFileName(w2Data.EmployeeFirstName);
                        return $"W2_Official_{w2Data.TaxYear}_{safeLast}_{safeFirst}.pdf";
                    });
                w2Count = w2Files.Count;
            }

            // Generate W-3
            var w3Data = BuildW3Data(w2DataList);
            var w3Doc = new W3Document(w3Data);
            var w3FileName = $"W3_{SelectedYear}.pdf";
            var w3FilePath = Path.Combine(outputDir, w3FileName);
            w3Doc.GeneratePdf(w3FilePath);

            // Generate 1099-NECs (Official IRS Forms)
            var form1099DataList = await Build1099NecDataListAsync();
            int nec1099Count = 0;
            if (form1099DataList.Count > 0)
            {
                var nec1099Filler = new Services.PdfFormFillers.Form1099NecFormFiller();
                var nec1099Files = nec1099Filler.FillAndSaveMultiple(
                    form1099DataList,
                    outputDir,
                    data =>
                    {
                        var safeName = SanitizeFileName(data.RecipientName);
                        return $"1099NEC_Official_{data.TaxYear}_{safeName}.pdf";
                    });
                nec1099Count = nec1099Files.Count;
            }

            // Generate 1096
            int form1096Count = 0;
            if (form1099DataList.Count > 0)
            {
                var form1096Data = Build1096Data(form1099DataList);
                var form1096Doc = new Form1096Document(form1096Data);
                var form1096FileName = $"1096_{SelectedYear}.pdf";
                var form1096FilePath = Path.Combine(outputDir, form1096FileName);
                form1096Doc.GeneratePdf(form1096FilePath);
                form1096Count = 1;
            }

            // Generate Form 940 (Official IRS Form)
            int form940Count = 0;
            if (w2Count > 0)
            {
                var form940Data = await BuildForm940DataAsync();
                var form940Filler = new Services.PdfFormFillers.Form940FormFiller();
                var form940FileName = $"Form940_Official_{SelectedYear}.pdf";
                var form940FilePath = Path.Combine(outputDir, form940FileName);
                form940Filler.FillAndSave(form940Data, form940FilePath);
                form940Count = 1;
            }

            if (w2Count == 0 && nec1099Count == 0)
            {
                StatusMessage = "No data found for the selected year. Run payroll and/or add contractor payments first.";
                return;
            }

            AppLogger.Information($"Generated {w2Count} W-2(s), 1 W-3, {nec1099Count} 1099-NEC(s), {form1096Count} 1096, and {form940Count} Form 940 for tax year {SelectedYear}");
            StatusMessage = $"Generated {w2Count} official W-2(s), 1 W-3, {nec1099Count} official 1099-NEC(s), {form1096Count} 1096, and {form940Count} official Form 940 in: {outputDir}";
            OpenFolder(outputDir);
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

    [RelayCommand]
    private async Task ExportEfw2Async()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating EFW2 file for SSA electronic filing...";

        try
        {
            var w2DataList = await BuildW2DataListAsync();

            if (w2DataList.Count == 0)
            {
                StatusMessage = "No employees with finalized payroll found for the selected year.";
                return;
            }

            var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();
            if (company == null)
            {
                StatusMessage = "Company information is required for EFW2 filing. Set up company info first.";
                return;
            }

            var efw2Content = Efw2FileService.GenerateEfw2(
                submitterEin: company.Ein,
                submitterName: company.CompanyName,
                submitterAddress: company.Address ?? "",
                submitterCity: company.City ?? "",
                submitterState: company.State ?? "OH",
                submitterZip: company.ZipCode ?? "",
                contactName: company.CompanyName,
                contactPhone: company.Phone ?? "",
                contactEmail: "",
                w2DataList: w2DataList,
                taxYear: SelectedYear);

            var outputDir = GetOutputDirectory();
            var fileName = $"EFW2_{SelectedYear}.txt";
            var filePath = Path.Combine(outputDir, fileName);
            await File.WriteAllTextAsync(filePath, efw2Content, new UTF8Encoding(false));

            AppLogger.Information($"Generated EFW2 file with {w2DataList.Count} W-2(s) for tax year {SelectedYear}");
            StatusMessage = $"EFW2 file saved to: {filePath} — Upload to SSA Business Services Online (BSO)";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating EFW2: {ex.Message}", ex);
            StatusMessage = $"Error generating EFW2: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task ExportIrisCsvAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating IRIS CSV file for IRS electronic 1099-NEC filing...";

        try
        {
            var form1099DataList = await Build1099NecDataListAsync();

            if (form1099DataList.Count == 0)
            {
                StatusMessage = "No contractors with payments >= $600 found for the selected year.";
                return;
            }

            if (form1099DataList.Count > IrisCsvService.MaxRecordsPerFile)
            {
                StatusMessage = $"IRIS CSV supports a maximum of {IrisCsvService.MaxRecordsPerFile} records per file. " +
                    $"You have {form1099DataList.Count} contractors. Split into multiple files manually.";
                return;
            }

            var csvContent = IrisCsvService.GenerateIrisCsv(form1099DataList, SelectedYear);

            var outputDir = GetOutputDirectory();
            var fileName = $"1099NEC_IRIS_{SelectedYear}.csv";
            var filePath = Path.Combine(outputDir, fileName);
            await File.WriteAllTextAsync(filePath, csvContent, new UTF8Encoding(false));

            AppLogger.Information($"Generated IRIS CSV with {form1099DataList.Count} 1099-NEC(s) for tax year {SelectedYear}");
            StatusMessage = $"IRIS CSV saved to: {filePath} — Upload to IRS IRIS portal";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating IRIS CSV: {ex.Message}", ex);
            StatusMessage = $"Error generating IRIS CSV: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Official IRS Form Fillers (using actual IRS PDFs with auto-filled fields)
    // ═══════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateOfficialW2sAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating official IRS W-2 forms (fillable PDFs)...";

        try
        {
            var w2DataList = await BuildW2DataListAsync();

            if (w2DataList.Count == 0)
            {
                StatusMessage = "No employees with finalized payroll found for the selected year. Run and finalize payroll first.";
                return;
            }

            var outputDir = GetOutputDirectory();
            var filler = new Services.PdfFormFillers.W2FormFiller();

            var generatedFiles = filler.FillAndSaveMultiple(
                w2DataList,
                outputDir,
                w2Data =>
                {
                    var safeLast = SanitizeFileName(w2Data.EmployeeLastName);
                    var safeFirst = SanitizeFileName(w2Data.EmployeeFirstName);
                    return $"W2_Official_{w2Data.TaxYear}_{safeLast}_{safeFirst}.pdf";
                });

            AppLogger.Information($"Generated {generatedFiles.Count} official W-2 form(s) for tax year {SelectedYear}");
            StatusMessage = $"Generated {generatedFiles.Count} official W-2 form(s) in: {outputDir}";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating official W-2s: {ex.Message}", ex);
            StatusMessage = $"Error generating official W-2s: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateOfficial1099sAsync()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating official IRS 1099-NEC forms (fillable PDFs)...";

        try
        {
            var form1099DataList = await Build1099NecDataListAsync();

            if (form1099DataList.Count == 0)
            {
                StatusMessage = "No contractors with payments >= $600 found for the selected year.";
                return;
            }

            var outputDir = GetOutputDirectory();
            var filler = new Services.PdfFormFillers.Form1099NecFormFiller();

            var generatedFiles = filler.FillAndSaveMultiple(
                form1099DataList,
                outputDir,
                data =>
                {
                    var safeName = SanitizeFileName(data.RecipientName);
                    return $"1099NEC_Official_{data.TaxYear}_{safeName}.pdf";
                });

            AppLogger.Information($"Generated {generatedFiles.Count} official 1099-NEC form(s) for tax year {SelectedYear}");
            StatusMessage = $"Generated {generatedFiles.Count} official 1099-NEC form(s) in: {outputDir}";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating official 1099-NECs: {ex.Message}", ex);
            StatusMessage = $"Error generating official 1099-NECs: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateOfficialForm940Async()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Generating official IRS Form 940 (fillable PDF)...";

        try
        {
            var form940Data = await BuildForm940DataAsync();

            var outputDir = GetOutputDirectory();
            var filler = new Services.PdfFormFillers.Form940FormFiller();

            var fileName = $"Form940_Official_{form940Data.TaxYear}.pdf";
            var filePath = Path.Combine(outputDir, fileName);

            filler.FillAndSave(form940Data, filePath);

            AppLogger.Information($"Generated official Form 940 for tax year {SelectedYear}");
            StatusMessage = $"Generated official Form 940 in: {outputDir}";
            OpenFolder(outputDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error generating official Form 940: {ex.Message}", ex);
            StatusMessage = $"Error generating official Form 940: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<Form940Data> BuildForm940DataAsync()
    {
        var year = SelectedYear;
        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();

        // Get all finalized, non-void paychecks for the year
        var paychecks = await _db.Paychecks
            .AsNoTracking()
            .Include(p => p.PayrollRun)
            .Where(p => p.PayrollRun.PayDate.Year == year
                && p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid)
            .ToListAsync();

        // Total payments to all employees
        var totalPayments = paychecks.Sum(p => p.GrossPay);

        // Taxable FUTA wages: per-employee gross capped at $7,000
        var employeeGroups = paychecks.GroupBy(p => p.EmployeeId);
        decimal taxableFutaWages = 0m;
        foreach (var group in employeeGroups)
        {
            var empGross = group.Sum(p => p.GrossPay);
            taxableFutaWages += Math.Min(empGross, EmployerTaxCalculator.FutaWageCap);
        }

        var futaTaxBeforeAdj = Math.Round(taxableFutaWages * EmployerTaxCalculator.DefaultFutaRate, 2, MidpointRounding.AwayFromZero);
        var totalFutaTax = futaTaxBeforeAdj; // No adjustments (Ohio is not a credit reduction state)

        // FUTA deposits from TaxLiability
        var deposits = await _db.TaxLiabilities
            .AsNoTracking()
            .Where(t => t.TaxYear == year
                && t.TaxType == TaxType.FUTA
                && t.Status == TaxLiabilityStatus.Paid)
            .SumAsync(t => t.AmountPaid);

        // Quarterly FUTA liabilities
        var quarterlyLiabilities = await _db.TaxLiabilities
            .AsNoTracking()
            .Where(t => t.TaxYear == year && t.TaxType == TaxType.FUTA)
            .GroupBy(t => t.Quarter)
            .Select(g => new { Quarter = g.Key, Amount = g.Sum(t => t.AmountOwed) })
            .ToDictionaryAsync(x => x.Quarter, x => x.Amount);

        // Validate quarterly liabilities against computed FUTA tax
        var quarterlyTotal = quarterlyLiabilities.Values.Sum();
        if (quarterlyLiabilities.Count == 0 && totalFutaTax > 0)
        {
            Form940ValidationWarning = $"Warning: No quarterly FUTA liability records found for {year}. " +
                "Quarterly breakdown on Form 940 may be incomplete. Consider recording quarterly tax liabilities.";
            AppLogger.Warning($"Form 940 for {year}: No quarterly FUTA liability records found but computed FUTA tax is {totalFutaTax:C}");
        }
        else if (Math.Abs(quarterlyTotal - totalFutaTax) > 0.01m)
        {
            Form940ValidationWarning = $"Warning: Quarterly FUTA liabilities ({quarterlyTotal:C}) do not match " +
                $"computed total FUTA tax ({totalFutaTax:C}). Review quarterly tax liability entries.";
            AppLogger.Warning($"Form 940 for {year}: Quarterly liabilities mismatch - recorded {quarterlyTotal:C} vs computed {totalFutaTax:C}");
        }
        else if (Math.Abs(deposits - totalFutaTax) > 0.01m && deposits > 0)
        {
            Form940ValidationWarning = $"Note: FUTA deposits ({deposits:C}) differ from total FUTA tax ({totalFutaTax:C}). " +
                "This may indicate a balance due or overpayment.";
        }
        else
        {
            Form940ValidationWarning = null;
        }

        return new Form940Data
        {
            TaxYear = year,
            EmployerEin = company?.Ein ?? "",
            EmployerName = company?.CompanyName ?? "",
            EmployerAddress = company?.Address ?? "",
            EmployerCity = company?.City ?? "",
            EmployerState = company?.State ?? "OH",
            EmployerZip = company?.ZipCode ?? "",
            EmployeeCount = employeeGroups.Count(),
            Line1a_State = "OH",
            Line3_TotalPayments = totalPayments,
            Line4_ExemptPayments = 0m,
            Line5_TaxableFutaWages = taxableFutaWages,
            Line6_FutaTaxBeforeAdjustments = futaTaxBeforeAdj,
            Line7_Adjustments = 0m,
            Line8_TotalFutaTax = totalFutaTax,
            Line12_TotalDeposits = deposits,
            Line14_BalanceDue = totalFutaTax - deposits,
            Q1Liability = quarterlyLiabilities.GetValueOrDefault(1, 0m),
            Q2Liability = quarterlyLiabilities.GetValueOrDefault(2, 0m),
            Q3Liability = quarterlyLiabilities.GetValueOrDefault(3, 0m),
            Q4Liability = quarterlyLiabilities.GetValueOrDefault(4, 0m)
        };
    }

    private async Task<List<W2Data>> BuildW2DataListAsync()
    {
        var year = SelectedYear;

        // Validate year is within supported range before calling GetSocialSecurityWageCap
        if (year < 2020)
        {
            throw new InvalidOperationException(
                $"W-2 forms cannot be generated for tax year {year}. " +
                $"Social Security wage base data is only available for years 2020 and later.");
        }

        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.Paychecks.Any(p =>
                p.PayrollRun.PayDate.Year == year
                && p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid))
            .Include(e => e.Paychecks.Where(p =>
                p.PayrollRun.PayDate.Year == year
                && p.PayrollRun.Status == PayrollRunStatus.Finalized
                && !p.IsVoid))
            .ToListAsync();

        var w2List = new List<W2Data>();

        foreach (var emp in employees)
        {
            var paychecks = emp.Paychecks;

            if (paychecks.Count == 0) continue;

            var grossPay = paychecks.Sum(p => p.GrossPay);

            // Calculate actual Social Security wages from the tax withheld
            // SS wages = SS tax / 6.2% (the rate), summed across all paychecks
            // This correctly handles mid-year hires and employees who hit the wage cap
            var totalSsTax = paychecks.Sum(p => p.SocialSecurityTax);
            decimal actualSsWages;
            if (totalSsTax > 0 && FicaCalculator.SocialSecurityRate > 0)
            {
                // Derive SS wages from actual tax paid (handles cap correctly)
                actualSsWages = Math.Round(totalSsTax / FicaCalculator.SocialSecurityRate, 2, MidpointRounding.AwayFromZero);
                // Ensure we don't exceed gross pay due to rounding
                actualSsWages = Math.Min(actualSsWages, grossPay);
            }
            else
            {
                actualSsWages = 0m;
            }

            string decryptedSsn;
            try
            {
                decryptedSsn = _encryption.Decrypt(emp.EncryptedSsn);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Failed to decrypt SSN for employee {emp.FirstName} {emp.LastName} (ID: {emp.Id}): {ex.Message}");
                continue;
            }

            var w2 = new W2Data
            {
                EmployerEin = company?.Ein ?? "",
                StateWithholdingId = company?.StateWithholdingId ?? "",
                EmployerName = company?.CompanyName ?? "",
                EmployerAddress = company?.Address ?? "",
                EmployerCity = company?.City ?? "",
                EmployerState = company?.State ?? "OH",
                EmployerZip = company?.ZipCode ?? "",
                EmployeeSsn = decryptedSsn,
                EmployeeFirstName = emp.FirstName,
                EmployeeLastName = emp.LastName,
                EmployeeAddress = emp.Address,
                EmployeeCity = emp.City,
                EmployeeState = emp.State,
                EmployeeZip = emp.ZipCode,
                Box1WagesTips = grossPay,
                Box2FederalTaxWithheld = paychecks.Sum(p => p.FederalWithholding),
                Box3SocialSecurityWages = actualSsWages,
                Box4SocialSecurityTax = totalSsTax,
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

    private async Task<List<Form1099NecData>> Build1099NecDataListAsync()
    {
        var year = SelectedYear;
        var company = await _db.CompanyInfo.AsNoTracking().FirstOrDefaultAsync();

        var contractors = await _db.Contractors
            .AsNoTracking()
            .Where(c => !c.Is1099Exempt && c.Payments.Any(p => p.TaxYear == year && !p.IsDeleted))
            .Include(c => c.Payments.Where(p => p.TaxYear == year && !p.IsDeleted))
            .ToListAsync();

        var formList = new List<Form1099NecData>();

        foreach (var contractor in contractors)
        {
            var totalPayments = contractor.Payments.Sum(p => p.Amount);
            if (totalPayments < 600) continue; // IRS threshold for 1099-NEC

            string decryptedTin;
            try
            {
                decryptedTin = _encryption.Decrypt(contractor.EncryptedTin);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Failed to decrypt TIN for contractor {contractor.Name} (ID: {contractor.Id}): {ex.Message}");
                continue;
            }

            var formData = new Form1099NecData
            {
                PayerName = company?.CompanyName ?? "",
                PayerAddress = company?.Address ?? "",
                PayerCity = company?.City ?? "",
                PayerState = company?.State ?? "OH",
                PayerZip = company?.ZipCode ?? "",
                PayerTin = company?.Ein ?? "",
                PayerPhone = company?.Phone,
                RecipientName = contractor.BusinessName ?? contractor.Name,
                RecipientAddress = contractor.Address,
                RecipientCity = contractor.City,
                RecipientState = contractor.State,
                RecipientZip = contractor.ZipCode,
                RecipientTin = decryptedTin,
                RecipientTinIsEin = contractor.IsEin,
                Box1_NonemployeeCompensation = totalPayments,
                Box4_FederalTaxWithheld = 0m, // Typically no federal backup withholding
                StateCode = "OH",
                StateName = "Ohio",
                StatePayerNo = company?.StateWithholdingId ?? "",
                StateIncome = totalPayments,
                StateTaxWithheld = 0m, // Typically no state withholding for contractors
                TaxYear = year,
                AccountNumber = contractor.Id.ToString()
            };

            formList.Add(formData);
        }

        return formList.OrderBy(f => f.RecipientName).ToList();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace(" ", "_").Replace(",", "");
    }

    private Form1096Data Build1096Data(List<Form1099NecData> form1099DataList)
    {
        var company = form1099DataList.FirstOrDefault();

        return new Form1096Data
        {
            FilerName = company?.PayerName ?? "",
            FilerAddress = company?.PayerAddress ?? "",
            FilerCity = company?.PayerCity ?? "",
            FilerState = company?.PayerState ?? "OH",
            FilerZip = company?.PayerZip ?? "",
            FilerTin = company?.PayerTin ?? "",
            ContactPhone = company?.PayerPhone,
            Box3_TotalForms = form1099DataList.Count,
            Box4_FederalTaxWithheld = form1099DataList.Sum(f => f.Box4_FederalTaxWithheld),
            Box5_TotalAmount = form1099DataList.Sum(f => f.Box1_NonemployeeCompensation),
            TaxYear = SelectedYear,
            FormType = "1099-NEC"
        };
    }
}

