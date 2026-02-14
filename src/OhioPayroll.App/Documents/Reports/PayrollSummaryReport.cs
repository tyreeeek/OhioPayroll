using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents.Reports;

public class PayrollSummaryData
{
    public string CompanyName { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<PayrollRunSummary> Runs { get; set; } = new();
}

public class PayrollRunSummary
{
    public int RunId { get; set; }
    public DateTime PayDate { get; set; }
    public int EmployeeCount { get; set; }
    public decimal GrossPay { get; set; }
    public decimal FederalTax { get; set; }
    public decimal StateTax { get; set; }
    public decimal LocalTax { get; set; }
    public decimal SocialSecurity { get; set; }
    public decimal Medicare { get; set; }
    public decimal EmployerTaxes { get; set; }
    public decimal NetPay { get; set; }
}

public class PayrollSummaryDocument : IDocument
{
    private readonly PayrollSummaryData _data;

    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string SectionHeaderBg = "#E8EDF2";
    private static readonly string TableHeaderBg = "#2C5F8A";
    private static readonly string TableHeaderText = "#FFFFFF";
    private static readonly string AltRowBg = "#F5F7FA";
    private static readonly string BorderColor = "#CBD5E1";

    public PayrollSummaryDocument(PayrollSummaryData data)
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
                    col.Item().Text("Payroll Summary Report")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text(_data.CompanyName)
                        .FontSize(10).FontColor(HeaderText);
                });
                row.ConstantItem(200).AlignRight().Column(col =>
                {
                    col.Item().AlignRight()
                        .Text($"Period: {_data.PeriodStart:MM/dd/yyyy} - {_data.PeriodEnd:MM/dd/yyyy}")
                        .FontSize(9).FontColor(HeaderText);
                    col.Item().AlignRight()
                        .Text($"Report Date: {_data.ReportDate:MM/dd/yyyy}")
                        .FontSize(8).FontColor(HeaderText);
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(BorderColor);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(8).Column(column =>
        {
            // Payroll Runs Detail Table
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("PAYROLL RUN DETAIL").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(40);   // Run ID
                    columns.ConstantColumn(80);   // Pay Date
                    columns.ConstantColumn(35);   // # Emps
                    columns.RelativeColumn(1);    // Gross
                    columns.RelativeColumn(1);    // Federal
                    columns.RelativeColumn(1);    // State
                    columns.RelativeColumn(1);    // Local
                    columns.RelativeColumn(1);    // SS
                    columns.RelativeColumn(1);    // Medicare
                    columns.RelativeColumn(1);    // Employer
                    columns.RelativeColumn(1);    // Net
                });

                table.Header(header =>
                {
                    TableHeader(header, "Run");
                    TableHeader(header, "Pay Date");
                    TableHeader(header, "Emps");
                    TableHeader(header, "Gross Pay");
                    TableHeader(header, "Federal");
                    TableHeader(header, "State");
                    TableHeader(header, "Local");
                    TableHeader(header, "SS");
                    TableHeader(header, "Medicare");
                    TableHeader(header, "Employer");
                    TableHeader(header, "Net Pay");
                });

                for (int i = 0; i < _data.Runs.Count; i++)
                {
                    var run = _data.Runs[i];
                    bool alt = i % 2 == 1;
                    var bg = alt ? AltRowBg : "#FFFFFF";

                    DataCell(table, run.RunId.ToString(), bg);
                    DataCell(table, run.PayDate.ToString("MM/dd/yyyy"), bg);
                    DataCell(table, run.EmployeeCount.ToString(), bg);
                    CurrencyCell(table, run.GrossPay, bg);
                    CurrencyCell(table, run.FederalTax, bg);
                    CurrencyCell(table, run.StateTax, bg);
                    CurrencyCell(table, run.LocalTax, bg);
                    CurrencyCell(table, run.SocialSecurity, bg);
                    CurrencyCell(table, run.Medicare, bg);
                    CurrencyCell(table, run.EmployerTaxes, bg);
                    CurrencyCell(table, run.NetPay, bg);
                }

                // Totals row
                var totalBg = SectionHeaderBg;
                TotalCell(table, "TOTAL", totalBg);
                TotalCell(table, "", totalBg);
                TotalCell(table, _data.Runs.Sum(r => r.EmployeeCount).ToString(), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.GrossPay), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.FederalTax), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.StateTax), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.LocalTax), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.SocialSecurity), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.Medicare), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.EmployerTaxes), totalBg);
                TotalCurrencyCell(table, _data.Runs.Sum(r => r.NetPay), totalBg);
            });

            column.Item().PaddingTop(16);

            // Tax Breakdown Summary
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("TAX BREAKDOWN SUMMARY").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Tax type
                    columns.RelativeColumn(2); // Amount
                });

                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(5)
                        .Text("Tax Type").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Total Amount").FontSize(8).Bold().FontColor(TableHeaderText);
                });

                SummaryRow(table, "Federal Income Tax Withheld", _data.Runs.Sum(r => r.FederalTax), false);
                SummaryRow(table, "Ohio State Income Tax", _data.Runs.Sum(r => r.StateTax), true);
                SummaryRow(table, "Local / Municipal Tax", _data.Runs.Sum(r => r.LocalTax), false);
                SummaryRow(table, "Social Security (Employee)", _data.Runs.Sum(r => r.SocialSecurity), true);
                SummaryRow(table, "Medicare (Employee)", _data.Runs.Sum(r => r.Medicare), false);
                SummaryRow(table, "Employer Taxes (SS, Medicare, FUTA, SUTA)", _data.Runs.Sum(r => r.EmployerTaxes), true);

                decimal totalTaxes = _data.Runs.Sum(r => r.FederalTax + r.StateTax + r.LocalTax
                    + r.SocialSecurity + r.Medicare + r.EmployerTaxes);

                table.Cell().BorderTop(2).BorderColor("#1B3A5C").Padding(5)
                    .Text("Total All Taxes").FontSize(10).Bold();
                table.Cell().BorderTop(2).BorderColor("#1B3A5C").Padding(5).AlignRight()
                    .Text(FormatCurrency(totalTaxes)).FontSize(10).Bold();
            });

            column.Item().PaddingTop(16);

            // Grand totals box
            column.Item().Border(2).BorderColor("#1B3A5C").Column(grandCol =>
            {
                grandCol.Item().Background("#1B3A5C").Padding(8)
                    .Text("PERIOD TOTALS").FontSize(11).Bold().FontColor("#FFFFFF");

                grandCol.Item().Padding(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Cell().Padding(6).AlignCenter().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Total Gross Pay").FontSize(8).FontColor(LabelColor);
                        col.Item().AlignCenter().Text(FormatCurrency(_data.Runs.Sum(r => r.GrossPay)))
                            .FontSize(14).Bold();
                    });
                    table.Cell().Padding(6).AlignCenter().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Total Deductions").FontSize(8).FontColor(LabelColor);
                        decimal totalDed = _data.Runs.Sum(r => r.FederalTax + r.StateTax + r.LocalTax
                            + r.SocialSecurity + r.Medicare);
                        col.Item().AlignCenter().Text(FormatCurrency(totalDed))
                            .FontSize(14).Bold().FontColor("#DC2626");
                    });
                    table.Cell().Padding(6).AlignCenter().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Total Net Pay").FontSize(8).FontColor(LabelColor);
                        col.Item().AlignCenter().Text(FormatCurrency(_data.Runs.Sum(r => r.NetPay)))
                            .FontSize(14).Bold().FontColor("#16A34A");
                    });
                });
            });
        });
    }

    private static readonly string LabelColor = "#64748B";

    private static void TableHeader(TableCellDescriptor header, string text)
    {
        header.Cell().Background(TableHeaderBg).Padding(4)
            .Text(text).FontSize(7).Bold().FontColor(TableHeaderText);
    }

    private static void DataCell(TableDescriptor table, string text, string bg)
    {
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(4)
            .Text(text).FontSize(7);
    }

    private static void CurrencyCell(TableDescriptor table, decimal value, string bg)
    {
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
            .Text(FormatCurrency(value)).FontSize(7);
    }

    private static void TotalCell(TableDescriptor table, string text, string bg)
    {
        table.Cell().Background(bg).BorderTop(2).BorderColor("#1B3A5C").Padding(4)
            .Text(text).FontSize(8).Bold();
    }

    private static void TotalCurrencyCell(TableDescriptor table, decimal value, string bg)
    {
        table.Cell().Background(bg).BorderTop(2).BorderColor("#1B3A5C").Padding(4).AlignRight()
            .Text(FormatCurrency(value)).FontSize(8).Bold();
    }

    private static void SummaryRow(TableDescriptor table, string label, decimal value, bool altRow)
    {
        var bg = altRow ? AltRowBg : "#FFFFFF";
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
            .Text(label).FontSize(9);
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
            .Text(FormatCurrency(value)).FontSize(9).Bold();
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(BorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Payroll Summary Report - {_data.CompanyName}")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignRight()
                    .Text($"Generated {DateTime.Now:MM/dd/yyyy}")
                    .FontSize(7).FontColor("#94A3B8");
            });
        });
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("$#,##0.00");
    }
}

