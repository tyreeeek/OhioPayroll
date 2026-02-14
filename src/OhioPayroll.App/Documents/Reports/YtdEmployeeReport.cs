using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents.Reports;

public class YtdEmployeeReportData
{
    public string CompanyName { get; set; } = string.Empty;
    public int TaxYear { get; set; }
    public List<YtdEmployeeLine> Employees { get; set; } = new();
}

public class YtdEmployeeLine
{
    public string EmployeeName { get; set; } = string.Empty;
    public string SsnLast4 { get; set; } = string.Empty;
    public decimal GrossPay { get; set; }
    public decimal FederalTax { get; set; }
    public decimal StateTax { get; set; }
    public decimal LocalTax { get; set; }
    public decimal SocialSecurity { get; set; }
    public decimal Medicare { get; set; }
    public decimal NetPay { get; set; }
}

public class YtdEmployeeReportDocument : IDocument
{
    private readonly YtdEmployeeReportData _data;

    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string SectionHeaderBg = "#E8EDF2";
    private static readonly string TableHeaderBg = "#2C5F8A";
    private static readonly string TableHeaderText = "#FFFFFF";
    private static readonly string AltRowBg = "#F5F7FA";
    private static readonly string BorderColor = "#CBD5E1";

    public YtdEmployeeReportDocument(YtdEmployeeReportData data)
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
                    col.Item().Text("Year-to-Date Employee Report")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text(_data.CompanyName)
                        .FontSize(10).FontColor(HeaderText);
                });
                row.ConstantItem(150).AlignRight().AlignMiddle()
                    .Text($"Tax Year {_data.TaxYear}")
                    .FontSize(12).Bold().FontColor(HeaderText);
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(BorderColor);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(8).Column(column =>
        {
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text($"EMPLOYEE EARNINGS AND WITHHOLDINGS - {_data.TaxYear}")
                .FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.5f); // Name
                    columns.ConstantColumn(50);   // SSN Last 4
                    columns.RelativeColumn(1.2f); // Gross
                    columns.RelativeColumn(1.2f); // Federal
                    columns.RelativeColumn(1);    // State
                    columns.RelativeColumn(1);    // Local
                    columns.RelativeColumn(1);    // SS
                    columns.RelativeColumn(1);    // Medicare
                    columns.RelativeColumn(1.2f); // Net
                });

                table.Header(header =>
                {
                    TableHeader(header, "Employee Name");
                    TableHeader(header, "SSN");
                    TableHeader(header, "Gross Pay");
                    TableHeader(header, "Federal");
                    TableHeader(header, "State");
                    TableHeader(header, "Local");
                    TableHeader(header, "SS");
                    TableHeader(header, "Medicare");
                    TableHeader(header, "Net Pay");
                });

                for (int i = 0; i < _data.Employees.Count; i++)
                {
                    var emp = _data.Employees[i];
                    bool alt = i % 2 == 1;
                    var bg = alt ? AltRowBg : "#FFFFFF";

                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(4)
                        .Text(emp.EmployeeName).FontSize(8);
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(4)
                        .Text($"***-{emp.SsnLast4}").FontSize(8);
                    CurrencyCell(table, emp.GrossPay, bg);
                    CurrencyCell(table, emp.FederalTax, bg);
                    CurrencyCell(table, emp.StateTax, bg);
                    CurrencyCell(table, emp.LocalTax, bg);
                    CurrencyCell(table, emp.SocialSecurity, bg);
                    CurrencyCell(table, emp.Medicare, bg);
                    CurrencyCell(table, emp.NetPay, bg);
                }

                // Totals row
                table.Cell().Background(SectionHeaderBg).BorderTop(2).BorderColor("#1B3A5C").Padding(4)
                    .Text("TOTALS").FontSize(8).Bold();
                table.Cell().Background(SectionHeaderBg).BorderTop(2).BorderColor("#1B3A5C").Padding(4)
                    .Text($"{_data.Employees.Count} emps").FontSize(7).Bold();
                TotalCurrencyCell(table, _data.Employees.Sum(e => e.GrossPay));
                TotalCurrencyCell(table, _data.Employees.Sum(e => e.FederalTax));
                TotalCurrencyCell(table, _data.Employees.Sum(e => e.StateTax));
                TotalCurrencyCell(table, _data.Employees.Sum(e => e.LocalTax));
                TotalCurrencyCell(table, _data.Employees.Sum(e => e.SocialSecurity));
                TotalCurrencyCell(table, _data.Employees.Sum(e => e.Medicare));
                TotalCurrencyCell(table, _data.Employees.Sum(e => e.NetPay));
            });

            column.Item().PaddingTop(16);

            // Summary boxes
            column.Item().Border(2).BorderColor("#1B3A5C").Column(summaryCol =>
            {
                summaryCol.Item().Background("#1B3A5C").Padding(8)
                    .Text("ANNUAL TOTALS").FontSize(11).Bold().FontColor("#FFFFFF");

                summaryCol.Item().Padding(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    SummaryBox(table, "Total Gross Pay",
                        FormatCurrency(_data.Employees.Sum(e => e.GrossPay)), "#000000");
                    SummaryBox(table, "Total Taxes Withheld",
                        FormatCurrency(_data.Employees.Sum(e => e.FederalTax + e.StateTax + e.LocalTax
                            + e.SocialSecurity + e.Medicare)), "#DC2626");
                    SummaryBox(table, "Total Net Pay",
                        FormatCurrency(_data.Employees.Sum(e => e.NetPay)), "#16A34A");
                    SummaryBox(table, "Total Employees",
                        _data.Employees.Count.ToString(), "#1B3A5C");
                });
            });
        });
    }

    private static void TableHeader(TableCellDescriptor header, string text)
    {
        header.Cell().Background(TableHeaderBg).Padding(4)
            .Text(text).FontSize(7).Bold().FontColor(TableHeaderText);
    }

    private static void CurrencyCell(TableDescriptor table, decimal value, string bg)
    {
        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
            .Text(FormatCurrency(value)).FontSize(7);
    }

    private static void TotalCurrencyCell(TableDescriptor table, decimal value)
    {
        table.Cell().Background(SectionHeaderBg).BorderTop(2).BorderColor("#1B3A5C").Padding(4).AlignRight()
            .Text(FormatCurrency(value)).FontSize(8).Bold();
    }

    private static void SummaryBox(TableDescriptor table, string label, string value, string valueColor)
    {
        table.Cell().Padding(6).AlignCenter().Column(col =>
        {
            col.Item().AlignCenter().Text(label).FontSize(8).FontColor("#64748B");
            col.Item().AlignCenter().Text(value).FontSize(14).Bold().FontColor(valueColor);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(BorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"YTD Employee Report - {_data.CompanyName}")
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

