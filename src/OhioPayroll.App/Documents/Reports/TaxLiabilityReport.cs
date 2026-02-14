using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents.Reports;

public class TaxLiabilityReportData
{
    public string CompanyName { get; set; } = string.Empty;
    public int TaxYear { get; set; }
    public List<TaxLiabilityLine> Lines { get; set; } = new();
}

public class TaxLiabilityLine
{
    public string TaxTypeName { get; set; } = string.Empty;
    public int Quarter { get; set; }
    public decimal AmountOwed { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance => AmountOwed - AmountPaid;
    public string Status { get; set; } = string.Empty;
}

public class TaxLiabilityReportDocument : IDocument
{
    private readonly TaxLiabilityReportData _data;

    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string SectionHeaderBg = "#E8EDF2";
    private static readonly string TableHeaderBg = "#2C5F8A";
    private static readonly string TableHeaderText = "#FFFFFF";
    private static readonly string AltRowBg = "#F5F7FA";
    private static readonly string BorderColor = "#CBD5E1";

    public TaxLiabilityReportDocument(TaxLiabilityReportData data)
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
                    col.Item().Text("Tax Liability Report")
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
            // Group lines by tax type
            var groupedByTax = _data.Lines
                .GroupBy(l => l.TaxTypeName)
                .OrderBy(g => g.Key);

            foreach (var group in groupedByTax)
            {
                column.Item().PaddingTop(8).Background(SectionHeaderBg).Padding(6)
                    .Text(group.Key).FontSize(10).Bold().FontColor("#1B3A5C");

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);   // Quarter
                        columns.RelativeColumn(2);   // Amount Owed
                        columns.RelativeColumn(2);   // Amount Paid
                        columns.RelativeColumn(2);   // Balance
                        columns.RelativeColumn(1.5f); // Status
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(TableHeaderBg).Padding(5)
                            .Text("Quarter").FontSize(8).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                            .Text("Amount Owed").FontSize(8).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                            .Text("Amount Paid").FontSize(8).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(5).AlignRight()
                            .Text("Balance").FontSize(8).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(5).AlignCenter()
                            .Text("Status").FontSize(8).Bold().FontColor(TableHeaderText);
                    });

                    var lines = group.OrderBy(l => l.Quarter).ToList();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        bool alt = i % 2 == 1;
                        var bg = alt ? AltRowBg : "#FFFFFF";

                        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5)
                            .Text($"Q{line.Quarter}").FontSize(9);
                        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                            .Text(FormatCurrency(line.AmountOwed)).FontSize(9);
                        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                            .Text(FormatCurrency(line.AmountPaid)).FontSize(9);

                        var balanceColor = line.Balance > 0 ? "#DC2626" : line.Balance < 0 ? "#16A34A" : "#000000";
                        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                            .Text(FormatCurrency(line.Balance)).FontSize(9).Bold().FontColor(balanceColor);

                        var statusColor = line.Status switch
                        {
                            "Paid" => "#16A34A",
                            "Unpaid" => "#DC2626",
                            "Scheduled" => "#D97706",
                            _ => "#000000"
                        };
                        table.Cell().Background(bg).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignCenter()
                            .Text(line.Status).FontSize(8).Bold().FontColor(statusColor);
                    }

                    // Subtotals for this tax type
                    decimal totalOwed = lines.Sum(l => l.AmountOwed);
                    decimal totalPaid = lines.Sum(l => l.AmountPaid);
                    decimal totalBalance = totalOwed - totalPaid;

                    table.Cell().Background(SectionHeaderBg).Padding(5)
                        .Text("Subtotal").FontSize(9).Bold();
                    table.Cell().Background(SectionHeaderBg).Padding(5).AlignRight()
                        .Text(FormatCurrency(totalOwed)).FontSize(9).Bold();
                    table.Cell().Background(SectionHeaderBg).Padding(5).AlignRight()
                        .Text(FormatCurrency(totalPaid)).FontSize(9).Bold();
                    table.Cell().Background(SectionHeaderBg).Padding(5).AlignRight()
                        .Text(FormatCurrency(totalBalance)).FontSize(9).Bold();
                    table.Cell().Background(SectionHeaderBg).Padding(5)
                        .Text("").FontSize(8);
                });
            }

            column.Item().PaddingTop(16);

            // Grand Totals
            column.Item().Border(2).BorderColor("#1B3A5C").Column(grandCol =>
            {
                grandCol.Item().Background("#1B3A5C").Padding(8)
                    .Text("GRAND TOTALS").FontSize(11).Bold().FontColor("#FFFFFF");

                grandCol.Item().Padding(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    decimal grandOwed = _data.Lines.Sum(l => l.AmountOwed);
                    decimal grandPaid = _data.Lines.Sum(l => l.AmountPaid);
                    decimal grandBalance = grandOwed - grandPaid;

                    table.Cell().Padding(6).AlignCenter().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Total Owed").FontSize(8).FontColor("#64748B");
                        col.Item().AlignCenter().Text(FormatCurrency(grandOwed))
                            .FontSize(14).Bold();
                    });
                    table.Cell().Padding(6).AlignCenter().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Total Paid").FontSize(8).FontColor("#64748B");
                        col.Item().AlignCenter().Text(FormatCurrency(grandPaid))
                            .FontSize(14).Bold().FontColor("#16A34A");
                    });
                    table.Cell().Padding(6).AlignCenter().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Outstanding Balance").FontSize(8).FontColor("#64748B");
                        var color = grandBalance > 0 ? "#DC2626" : "#16A34A";
                        col.Item().AlignCenter().Text(FormatCurrency(grandBalance))
                            .FontSize(14).Bold().FontColor(color);
                    });
                });
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
                row.RelativeItem().Text($"Tax Liability Report - {_data.CompanyName}")
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
