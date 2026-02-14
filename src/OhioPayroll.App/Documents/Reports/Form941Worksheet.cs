using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents.Reports;

public class Form941Data
{
    public int TaxYear { get; set; }
    public int Quarter { get; set; }
    public string EmployerEin { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;

    public int NumberOfEmployees { get; set; }
    public decimal Line2_WagesTipsCompensation { get; set; }
    public decimal Line3_FederalTaxWithheld { get; set; }
    public decimal Line5a_TaxableSsWages { get; set; }
    public decimal Line5a_SsTaxDue { get; set; }  // 5a * 12.4%
    public decimal Line5c_TaxableMedicareWages { get; set; }
    public decimal Line5c_MedicareTaxDue { get; set; }  // 5c * 2.9%
    public decimal Line6_TotalTaxesBeforeAdjustments { get; set; }
    public decimal Line10_TotalTaxes { get; set; }
    public decimal Line11_TotalDeposits { get; set; }
    public decimal Line14_BalanceDueOrOverpayment { get; set; }
}

public class Form941WorksheetDocument : IDocument
{
    private readonly Form941Data _data;

    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string SectionHeaderBg = "#E8EDF2";
    private static readonly string LabelColor = "#64748B";
    private static readonly string BorderColor = "#CBD5E1";
    private static readonly string AltRowBg = "#F5F7FA";

    public Form941WorksheetDocument(Form941Data data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(0.5f, Unit.Inch);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Background(HeaderBg).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Form 941 Worksheet")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text("Employer's QUARTERLY Federal Tax Return")
                        .FontSize(9).FontColor(HeaderText);
                });
                row.ConstantItem(200).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text($"Tax Year {_data.TaxYear}  Q{_data.Quarter}")
                        .FontSize(12).Bold().FontColor(HeaderText);
                    col.Item().AlignRight().Text(GetQuarterDateRange())
                        .FontSize(8).FontColor(HeaderText);
                });
            });

            // Employer info bar
            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(8).Row(r =>
                {
                    r.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Employer Name").FontSize(7).FontColor(LabelColor);
                        col.Item().Text(_data.EmployerName).FontSize(10).Bold();
                    });
                    r.ConstantItem(200).Column(col =>
                    {
                        col.Item().Text("Employer EIN").FontSize(7).FontColor(LabelColor);
                        col.Item().Text(FormatEin(_data.EmployerEin)).FontSize(10).Bold();
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
            // Part 1: Answer these questions
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("Part 1: Lines 1-15 Calculation").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50);  // Line #
                    columns.RelativeColumn(4);   // Description
                    columns.RelativeColumn(2);   // Amount
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background("#2C5F8A").Padding(5)
                        .Text("Line").FontSize(8).Bold().FontColor("#FFFFFF");
                    header.Cell().Background("#2C5F8A").Padding(5)
                        .Text("Description").FontSize(8).Bold().FontColor("#FFFFFF");
                    header.Cell().Background("#2C5F8A").Padding(5).AlignRight()
                        .Text("Amount").FontSize(8).Bold().FontColor("#FFFFFF");
                });

                // Line 1
                ComposeLineRow(table, "1", "Number of employees who received wages, tips, or other compensation",
                    _data.NumberOfEmployees.ToString(), false);

                // Line 2
                ComposeLineRow(table, "2", "Wages, tips, and other compensation",
                    FormatCurrency(_data.Line2_WagesTipsCompensation), true);

                // Line 3
                ComposeLineRow(table, "3", "Federal income tax withheld from wages, tips, and other compensation",
                    FormatCurrency(_data.Line3_FederalTaxWithheld), false);

                // Line 4
                ComposeLineRow(table, "4", "If no wages, tips, and other compensation are subject to social security or Medicare tax",
                    "", true);

                // Line 5a
                ComposeDualLineRow(table, "5a", "Taxable social security wages",
                    FormatCurrency(_data.Line5a_TaxableSsWages), "x 0.124 =",
                    FormatCurrency(_data.Line5a_SsTaxDue), false);

                // Line 5b
                ComposeDualLineRow(table, "5b", "Taxable social security tips",
                    FormatCurrency(0m), "x 0.124 =",
                    FormatCurrency(0m), true);

                // Line 5c
                ComposeDualLineRow(table, "5c", "Taxable Medicare wages & tips",
                    FormatCurrency(_data.Line5c_TaxableMedicareWages), "x 0.029 =",
                    FormatCurrency(_data.Line5c_MedicareTaxDue), false);

                // Line 5d
                ComposeLineRow(table, "5d", "Taxable wages & tips subject to Additional Medicare Tax withholding",
                    FormatCurrency(0m), true);

                // Line 5e: Sum of 5a through 5d. Lines 5b (social security tips) and
                // 5d (Additional Medicare Tax) are zero because this application does
                // not track tip income or Additional Medicare Tax withholding.
                ComposeLineRow(table, "5e", "Total social security and Medicare taxes (add 5a through 5d)",
                    FormatCurrency(_data.Line5a_SsTaxDue + _data.Line5c_MedicareTaxDue), false);

                // Line 6
                ComposeLineRow(table, "6", "Total taxes before adjustments (add lines 3 and 5e)",
                    FormatCurrency(_data.Line6_TotalTaxesBeforeAdjustments), true);

                // Lines 7-9 adjustments
                ComposeLineRow(table, "7", "Current quarter's adjustment for fractions of cents",
                    FormatCurrency(0m), false);
                ComposeLineRow(table, "8", "Current quarter's adjustment for sick pay",
                    FormatCurrency(0m), true);
                ComposeLineRow(table, "9", "Current quarter's adjustments for tips and group-term life insurance",
                    FormatCurrency(0m), false);

                // Line 10
                ComposeLineRow(table, "10", "Total taxes after adjustments (combine lines 6 through 9)",
                    FormatCurrency(_data.Line10_TotalTaxes), true);

                // Line 11
                ComposeLineRow(table, "11", "Qualified small business payroll tax credit for increasing research activities",
                    FormatCurrency(0m), false);

                // Line 12
                ComposeLineRow(table, "12", "Total taxes after adjustments and credits (line 10 - line 11)",
                    FormatCurrency(_data.Line10_TotalTaxes), true);

                // Line 13
                ComposeLineRow(table, "13", "Total deposits for this quarter, including overpayment applied from prior quarter",
                    FormatCurrency(_data.Line11_TotalDeposits), false);

                // Line 14
                var line14 = _data.Line14_BalanceDueOrOverpayment;
                var line14Label = line14 >= 0
                    ? "Balance due (line 12 - line 13)"
                    : "Overpayment (line 13 - line 12)";
                ComposeLineRow(table, "14", line14Label,
                    FormatCurrency(Math.Abs(line14)), true);

                // Line 15
                ComposeLineRow(table, "15", "Overpayment: Apply to next return / Send a refund",
                    line14 < 0 ? FormatCurrency(Math.Abs(line14)) : FormatCurrency(0m), false);
            });

            column.Item().PaddingTop(16);

            // Summary box
            column.Item().Border(2).BorderColor("#1B3A5C").Column(summaryCol =>
            {
                summaryCol.Item().Background(SectionHeaderBg).Padding(8)
                    .Text("SUMMARY").FontSize(11).Bold().FontColor("#1B3A5C");

                summaryCol.Item().Padding(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });

                    SummaryRow(table, "Total Taxes for Quarter:", FormatCurrency(_data.Line10_TotalTaxes));
                    SummaryRow(table, "Total Deposits Made:", FormatCurrency(_data.Line11_TotalDeposits));

                    var balance = _data.Line14_BalanceDueOrOverpayment;
                    var balanceLabel = balance >= 0 ? "Balance Due:" : "Overpayment:";
                    SummaryRow(table, balanceLabel, FormatCurrency(Math.Abs(balance)));
                });
            });
        });
    }

    private static void ComposeLineRow(TableDescriptor table, string lineNum, string description,
        string amount, bool altRow)
    {
        var bg = altRow ? AltRowBg : "#FFFFFF";

        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
            .Text(lineNum).FontSize(9).Bold();
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
            .Text(description).FontSize(8);
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
            .Text(amount).FontSize(9).Bold();
    }

    private static void ComposeDualLineRow(TableDescriptor table, string lineNum, string description,
        string wageAmount, string multiplier, string taxAmount, bool altRow)
    {
        var bg = altRow ? AltRowBg : "#FFFFFF";

        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
            .Text(lineNum).FontSize(9).Bold();
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
            .Row(row =>
            {
                row.RelativeItem().Text(description).FontSize(8);
                row.ConstantItem(80).AlignRight().Text(wageAmount).FontSize(8);
                row.ConstantItem(60).AlignCenter().Text(multiplier).FontSize(7).FontColor(LabelColor);
            });
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
            .Text(taxAmount).FontSize(9).Bold();
    }

    private static void SummaryRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(4).Text(label).FontSize(10).Bold();
        table.Cell().Padding(4).AlignRight().Text(value).FontSize(11).Bold();
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(BorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Form 941 Worksheet  Q{_data.Quarter} {_data.TaxYear}")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignCenter()
                    .Text("For internal use only - not for filing")
                    .FontSize(7).FontColor("#94A3B8").Italic();
                row.RelativeItem().AlignRight()
                    .Text($"Generated {DateTime.Now:MM/dd/yyyy}")
                    .FontSize(7).FontColor("#94A3B8");
            });
        });
    }

    private string GetQuarterDateRange()
    {
        return _data.Quarter switch
        {
            1 => $"January 1 - March 31, {_data.TaxYear}",
            2 => $"April 1 - June 30, {_data.TaxYear}",
            3 => $"July 1 - September 30, {_data.TaxYear}",
            4 => $"October 1 - December 31, {_data.TaxYear}",
            _ => ""
        };
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("$#,##0.00");
    }

    private static string FormatEin(string ein)
    {
        if (ein.Length == 9)
            return $"{ein[..2]}-{ein[2..]}";
        return ein;
    }
}

