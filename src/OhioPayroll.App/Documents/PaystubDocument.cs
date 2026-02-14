using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OhioPayroll.Core.Models;

namespace OhioPayroll.App.Documents;

public class PaystubDocument : IDocument
{
    private readonly CompanyInfo _company;
    private readonly Employee _employee;
    private readonly Paycheck _paycheck;
    private readonly PayrollRun _run;

    // Color palette
    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string SectionHeaderBg = "#E8EDF2";
    private static readonly string TableHeaderBg = "#2C5F8A";
    private static readonly string TableHeaderText = "#FFFFFF";
    private static readonly string AltRowBg = "#F5F7FA";
    private static readonly string NetPayBg = "#1B3A5C";
    private static readonly string NetPayText = "#FFFFFF";
    private static readonly string BorderColor = "#CBD5E1";

    public PaystubDocument(CompanyInfo company, Employee employee, Paycheck paycheck, PayrollRun run)
    {
        _company = company;
        _employee = employee;
        _paycheck = paycheck;
        _run = run;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(0.5f, Unit.Inch);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            // Company banner
            column.Item().Background(HeaderBg).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_company.CompanyName)
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text($"{_company.Address}")
                        .FontSize(8).FontColor(HeaderText);
                    col.Item().Text($"{_company.City}, {_company.State} {_company.ZipCode}")
                        .FontSize(8).FontColor(HeaderText);
                    if (!string.IsNullOrWhiteSpace(_company.Phone))
                        col.Item().Text($"Phone: {_company.Phone}")
                            .FontSize(8).FontColor(HeaderText);
                    col.Item().Text($"EIN: {FormatEin(_company.Ein)}")
                        .FontSize(8).FontColor(HeaderText);
                });

                row.ConstantItem(200).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text("EARNINGS STATEMENT")
                        .FontSize(14).Bold().FontColor(HeaderText);
                    col.Item().PaddingTop(4).AlignRight().Text($"Pay Date: {_run.PayDate:MM/dd/yyyy}")
                        .FontSize(9).FontColor(HeaderText);
                    if (_paycheck.CheckNumber.HasValue)
                        col.Item().AlignRight().Text($"Check #: {_paycheck.CheckNumber}")
                            .FontSize(9).FontColor(HeaderText);
                });
            });

            // Pay period and employee info row
            column.Item().PaddingTop(8).Row(row =>
            {
                // Employee info
                row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(8).Column(col =>
                {
                    col.Item().Text("EMPLOYEE").FontSize(7).Bold().FontColor("#64748B");
                    col.Item().PaddingTop(2).Text(_employee.FullName).FontSize(10).Bold();
                    col.Item().Text(_employee.Address).FontSize(8);
                    col.Item().Text($"{_employee.City}, {_employee.State} {_employee.ZipCode}").FontSize(8);
                    col.Item().Text($"SSN: XXX-XX-{_employee.SsnLast4}").FontSize(8);
                });

                row.ConstantItem(8);

                // Pay period info
                row.ConstantItem(200).Border(1).BorderColor(BorderColor).Padding(8).Column(col =>
                {
                    col.Item().Text("PAY PERIOD").FontSize(7).Bold().FontColor("#64748B");
                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text("Period Start:").FontSize(8);
                        r.ConstantItem(80).AlignRight().Text($"{_run.PeriodStart:MM/dd/yyyy}").FontSize(8).Bold();
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Period End:").FontSize(8);
                        r.ConstantItem(80).AlignRight().Text($"{_run.PeriodEnd:MM/dd/yyyy}").FontSize(8).Bold();
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Pay Date:").FontSize(8);
                        r.ConstantItem(80).AlignRight().Text($"{_run.PayDate:MM/dd/yyyy}").FontSize(8).Bold();
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Pay Frequency:").FontSize(8);
                        r.ConstantItem(80).AlignRight().Text($"{_run.PayFrequency}").FontSize(8).Bold();
                    });
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(BorderColor);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(8).Column(column =>
        {
            // Earnings section
            column.Item().Element(ComposeEarningsTable);

            column.Item().PaddingTop(10);

            // Employee tax deductions section
            column.Item().Element(ComposeEmployeeTaxTable);

            column.Item().PaddingTop(10);

            // Employer taxes section (informational)
            column.Item().Element(ComposeEmployerTaxTable);

            column.Item().PaddingTop(12);

            // Net Pay summary
            column.Item().Element(ComposeNetPaySummary);
        });
    }

    private void ComposeEarningsTable(IContainer container)
    {
        container.Column(column =>
        {
            // Section header
            column.Item().Background(SectionHeaderBg).Padding(6).Text("EARNINGS")
                .FontSize(10).Bold().FontColor("#1B3A5C");

            // Table header
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);  // Description
                    columns.RelativeColumn(1.5f); // Hours
                    columns.RelativeColumn(1.5f); // Rate
                    columns.RelativeColumn(2);  // Current
                    columns.RelativeColumn(2);  // YTD
                });

                // Header row
                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(5)
                        .Text("Description").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Hours").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Rate").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Current").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("YTD").FontSize(8).Bold().FontColor(TableHeaderText);
                });

                decimal regularRate = _paycheck.RegularHours != 0
                    ? _paycheck.RegularPay / _paycheck.RegularHours
                    : 0;
                decimal overtimeRate = _paycheck.OvertimeHours != 0
                    ? _paycheck.OvertimePay / _paycheck.OvertimeHours
                    : 0;

                // Regular Pay row
                EarningsRow(table, "Regular Pay", _paycheck.RegularHours, regularRate,
                    _paycheck.RegularPay, null, false);

                // Overtime Pay row
                EarningsRow(table, "Overtime Pay", _paycheck.OvertimeHours, overtimeRate,
                    _paycheck.OvertimePay, null, true);

                // Gross Pay total row
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5)
                    .Text("Gross Pay").FontSize(9).Bold();
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5).AlignRight()
                    .Text($"{_paycheck.RegularHours + _paycheck.OvertimeHours:F2}").FontSize(9).Bold();
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5)
                    .Text("");
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5).AlignRight()
                    .Text($"{_paycheck.GrossPay:C}").FontSize(9).Bold();
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5).AlignRight()
                    .Text($"{_paycheck.YtdGrossPay:C}").FontSize(9).Bold();
            });
        });
    }

    private static void EarningsRow(TableDescriptor table, string description,
        decimal hours, decimal rate, decimal current, decimal? ytd, bool altRow)
    {
        var bg = altRow ? AltRowBg : "#FFFFFF";

        table.Cell().Background(bg).Padding(5).Text(description).FontSize(8);
        table.Cell().Background(bg).Padding(5).AlignRight()
            .Text(hours != 0 ? $"{hours:F2}" : "-").FontSize(8);
        table.Cell().Background(bg).Padding(5).AlignRight()
            .Text(rate != 0 ? $"{rate:C}" : "-").FontSize(8);
        table.Cell().Background(bg).Padding(5).AlignRight()
            .Text($"{current:C}").FontSize(8);
        table.Cell().Background(bg).Padding(5).AlignRight()
            .Text(ytd.HasValue ? $"{ytd.Value:C}" : "").FontSize(8);
    }

    private void ComposeEmployeeTaxTable(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Background(SectionHeaderBg).Padding(6).Text("EMPLOYEE TAX DEDUCTIONS")
                .FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);  // Description
                    columns.RelativeColumn(2);  // Current
                    columns.RelativeColumn(2);  // YTD
                });

                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(5)
                        .Text("Description").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Current").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("YTD").FontSize(8).Bold().FontColor(TableHeaderText);
                });

                TaxRow(table, "Federal Withholding", _paycheck.FederalWithholding, _paycheck.YtdFederalWithholding, false);
                TaxRow(table, "Ohio State Withholding", _paycheck.OhioStateWithholding, _paycheck.YtdOhioStateWithholding, true);
                TaxRow(table, "School District Tax", _paycheck.SchoolDistrictTax, _paycheck.YtdSchoolDistrictTax, false);
                TaxRow(table, "Local/Municipality Tax", _paycheck.LocalMunicipalityTax, _paycheck.YtdLocalTax, true);
                TaxRow(table, "Social Security (Employee)", _paycheck.SocialSecurityTax, _paycheck.YtdSocialSecurity, false);
                TaxRow(table, "Medicare (Employee)", _paycheck.MedicareTax, _paycheck.YtdMedicare, true);

                // Total deductions row
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5)
                    .Text("Total Deductions").FontSize(9).Bold();
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5).AlignRight()
                    .Text($"{_paycheck.TotalDeductions:C}").FontSize(9).Bold();
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5).AlignRight()
                    .Text($"{TotalYtdDeductions():C}").FontSize(9).Bold();
            });
        });
    }

    private static void TaxRow(TableDescriptor table, string description,
        decimal current, decimal ytd, bool altRow)
    {
        var bg = altRow ? AltRowBg : "#FFFFFF";

        table.Cell().Background(bg).Padding(5).Text(description).FontSize(8);
        table.Cell().Background(bg).Padding(5).AlignRight().Text($"{current:C}").FontSize(8);
        table.Cell().Background(bg).Padding(5).AlignRight().Text($"{ytd:C}").FontSize(8);
    }

    private void ComposeEmployerTaxTable(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Background(SectionHeaderBg).Padding(6).Text("EMPLOYER TAXES (Informational)")
                .FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);  // Description
                    columns.RelativeColumn(2);  // Current
                });

                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(5)
                        .Text("Description").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Current").FontSize(8).Bold().FontColor(TableHeaderText);
                });

                EmployerTaxRow(table, "Employer Social Security", _paycheck.EmployerSocialSecurity, false);
                EmployerTaxRow(table, "Employer Medicare", _paycheck.EmployerMedicare, true);
                EmployerTaxRow(table, "Federal Unemployment (FUTA)", _paycheck.EmployerFuta, false);
                EmployerTaxRow(table, "State Unemployment (SUTA)", _paycheck.EmployerSuta, true);

                // Total
                decimal totalEmployer = _paycheck.EmployerSocialSecurity + _paycheck.EmployerMedicare
                    + _paycheck.EmployerFuta + _paycheck.EmployerSuta;
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5)
                    .Text("Total Employer Taxes").FontSize(9).Bold();
                table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(5).AlignRight()
                    .Text($"{totalEmployer:C}").FontSize(9).Bold();
            });
        });
    }

    private static void EmployerTaxRow(TableDescriptor table, string description,
        decimal current, bool altRow)
    {
        var bg = altRow ? AltRowBg : "#FFFFFF";

        table.Cell().Background(bg).Padding(5).Text(description).FontSize(8);
        table.Cell().Background(bg).Padding(5).AlignRight().Text($"{current:C}").FontSize(8);
    }

    private void ComposeNetPaySummary(IContainer container)
    {
        container.Column(column =>
        {
            // Summary row: Gross - Deductions = Net
            column.Item().Border(2).BorderColor("#1B3A5C").Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1); // Gross Pay
                    columns.RelativeColumn(1); // Total Deductions
                    columns.RelativeColumn(1); // Net Pay
                });

                // Labels
                table.Cell().Background(SectionHeaderBg).Padding(6).AlignCenter()
                    .Text("Gross Pay").FontSize(9).Bold();
                table.Cell().Background(SectionHeaderBg).Padding(6).AlignCenter()
                    .Text("Total Deductions").FontSize(9).Bold();
                table.Cell().Background(NetPayBg).Padding(6).AlignCenter()
                    .Text("NET PAY").FontSize(9).Bold().FontColor(NetPayText);

                // Current values
                table.Cell().Padding(8).AlignCenter()
                    .Text($"{_paycheck.GrossPay:C}").FontSize(12).Bold();
                table.Cell().Padding(8).AlignCenter()
                    .Text($"{_paycheck.TotalDeductions:C}").FontSize(12).Bold().FontColor("#DC2626");
                table.Cell().Background(NetPayBg).Padding(8).AlignCenter()
                    .Text($"{_paycheck.NetPay:C}").FontSize(16).Bold().FontColor(NetPayText);

                // YTD labels
                table.Cell().PaddingBottom(4).AlignCenter()
                    .Text($"YTD: {_paycheck.YtdGrossPay:C}").FontSize(7).FontColor("#64748B");
                table.Cell().PaddingBottom(4).AlignCenter()
                    .Text($"YTD: {TotalYtdDeductions():C}").FontSize(7).FontColor("#64748B");
                table.Cell().Background(NetPayBg).PaddingBottom(4).AlignCenter()
                    .Text($"YTD: {_paycheck.YtdNetPay:C}").FontSize(7).FontColor("#94A3B8");
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(BorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Generated on {DateTime.Now:MM/dd/yyyy h:mm tt}")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignRight().Text("This is not a negotiable instrument")
                    .FontSize(7).FontColor("#94A3B8").Italic();
            });
        });
    }

    private decimal TotalYtdDeductions()
    {
        return _paycheck.YtdFederalWithholding
            + _paycheck.YtdOhioStateWithholding
            + _paycheck.YtdSchoolDistrictTax
            + _paycheck.YtdLocalTax
            + _paycheck.YtdSocialSecurity
            + _paycheck.YtdMedicare;
    }

    private static string FormatEin(string ein)
    {
        if (ein.Length == 9)
            return $"{ein[..2]}-{ein[2..]}";
        return ein;
    }
}
