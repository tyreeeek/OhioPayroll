using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents.Reports;

public class CheckRegisterReportData
{
    public string CompanyName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<CheckRegisterLine> Entries { get; set; } = new();
}

public class CheckRegisterLine
{
    public int CheckNumber { get; set; }
    public DateTime Date { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;  // Issued, Cleared, Voided
    public decimal RunningTotal { get; set; }
}

public class CheckRegisterReportDocument : IDocument
{
    private readonly CheckRegisterReportData _data;

    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string SectionHeaderBg = "#E8EDF2";
    private static readonly string TableHeaderBg = "#2C5F8A";
    private static readonly string TableHeaderText = "#FFFFFF";
    private static readonly string AltRowBg = "#F5F7FA";
    private static readonly string BorderColor = "#CBD5E1";

    public CheckRegisterReportDocument(CheckRegisterReportData data)
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
                    col.Item().Text("Check Register Report")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text(_data.CompanyName)
                        .FontSize(10).FontColor(HeaderText);
                });
                row.ConstantItem(220).AlignRight().Column(col =>
                {
                    col.Item().AlignRight()
                        .Text($"Period: {_data.StartDate:MM/dd/yyyy} - {_data.EndDate:MM/dd/yyyy}")
                        .FontSize(9).FontColor(HeaderText);
                    col.Item().AlignRight()
                        .Text($"Total Checks: {_data.Entries.Count}")
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
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("CHECK REGISTER").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);   // Check #
                    columns.ConstantColumn(85);   // Date
                    columns.RelativeColumn(3);    // Payee
                    columns.RelativeColumn(1.5f); // Amount
                    columns.ConstantColumn(70);   // Status
                    columns.RelativeColumn(1.5f); // Running Total
                });

                table.Header(header =>
                {
                    header.Cell().Background(TableHeaderBg).Padding(5)
                        .Text("Check #").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5)
                        .Text("Date").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5)
                        .Text("Payee").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Amount").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignCenter()
                        .Text("Status").FontSize(8).Bold().FontColor(TableHeaderText);
                    header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                        .Text("Running Total").FontSize(8).Bold().FontColor(TableHeaderText);
                });

                for (int i = 0; i < _data.Entries.Count; i++)
                {
                    var entry = _data.Entries[i];
                    bool alt = i % 2 == 1;
                    var bg = alt ? AltRowBg : "#FFFFFF";

                    // Check number
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(entry.CheckNumber.ToString()).FontSize(9);

                    // Date
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(entry.Date.ToString("MM/dd/yyyy")).FontSize(9);

                    // Payee
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(entry.PayeeName).FontSize(9);

                    // Amount - show strikethrough styling for voided
                    var amountColor = entry.Status == "Voided" ? "#94A3B8" : "#000000";
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text(FormatCurrency(entry.Amount)).FontSize(9).FontColor(amountColor);

                    // Status with color coding
                    var statusColor = entry.Status switch
                    {
                        "Cleared" => "#16A34A",
                        "Issued" => "#2563EB",
                        "Voided" => "#DC2626",
                        _ => "#000000"
                    };
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignCenter()
                        .Text(entry.Status).FontSize(8).Bold().FontColor(statusColor);

                    // Running total
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text(FormatCurrency(entry.RunningTotal)).FontSize(9);
                }
            });

            column.Item().PaddingTop(16);

            // Summary section
            column.Item().Border(2).BorderColor("#1B3A5C").Column(summaryCol =>
            {
                summaryCol.Item().Background("#1B3A5C").Padding(8)
                    .Text("REGISTER SUMMARY").FontSize(11).Bold().FontColor("#FFFFFF");

                summaryCol.Item().Padding(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    int issuedCount = _data.Entries.Count(e => e.Status == "Issued");
                    int clearedCount = _data.Entries.Count(e => e.Status == "Cleared");
                    int voidedCount = _data.Entries.Count(e => e.Status == "Voided");
                    decimal totalIssued = _data.Entries.Where(e => e.Status != "Voided").Sum(e => e.Amount);

                    SummaryBox(table, "Total Checks", _data.Entries.Count.ToString(), "#1B3A5C");
                    SummaryBox(table, "Issued / Cleared",
                        $"{issuedCount} / {clearedCount}", "#2563EB");
                    SummaryBox(table, "Voided", voidedCount.ToString(), "#DC2626");
                    SummaryBox(table, "Total Amount (excl. voided)",
                        FormatCurrency(totalIssued), "#16A34A");
                });
            });
        });
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
                row.RelativeItem().Text($"Check Register - {_data.CompanyName}")
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
