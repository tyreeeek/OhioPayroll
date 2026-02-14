using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents;

public class W3Data
{
    // Employer info
    public string EmployerEin { get; set; } = string.Empty;
    public string StateWithholdingId { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string EmployerAddress { get; set; } = string.Empty;
    public string EmployerCity { get; set; } = string.Empty;
    public string EmployerState { get; set; } = string.Empty;
    public string EmployerZip { get; set; } = string.Empty;

    // Totals across all W-2s
    public int NumberOfW2s { get; set; }
    public decimal TotalWages { get; set; }
    public decimal TotalFederalTax { get; set; }
    public decimal TotalSsWages { get; set; }
    public decimal TotalSsTax { get; set; }
    public decimal TotalMedicareWages { get; set; }
    public decimal TotalMedicareTax { get; set; }
    public decimal TotalStateWages { get; set; }
    public decimal TotalStateTax { get; set; }
    public decimal TotalLocalWages { get; set; }
    public decimal TotalLocalTax { get; set; }

    public int TaxYear { get; set; }
}

public class W3Document : IDocument
{
    private readonly W3Data _data;

    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string LabelColor = "#64748B";
    private static readonly string BorderColor = "#000000";
    private static readonly string LightBorderColor = "#CBD5E1";
    private static readonly string SectionHeaderBg = "#E8EDF2";

    public W3Document(W3Data data)
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
            column.Item().Background(HeaderBg).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"W-3 Transmittal of Wage and Tax Statements  {_data.TaxYear}")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text("Send this entire page with the entire Copy A page of Form(s) W-2 to the SSA")
                        .FontSize(8).FontColor(HeaderText);
                });
                row.ConstantItem(150).AlignRight().AlignMiddle()
                    .Text("Department of the Treasury - IRS")
                    .FontSize(8).FontColor(HeaderText);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(8).Column(column =>
        {
            // Row 1: Kind of Payer / Kind of Employer
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("a  Control number")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text("").FontSize(8);
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("b  Kind of Payer")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text("941  Regular").FontSize(8).Bold();
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("b  Kind of Employer")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text("None apply").FontSize(8);
                });
            });

            column.Item().Height(4);

            // Row 2: Number of W-2s + Employer EIN
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("c  Total number of Forms W-2")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(_data.NumberOfW2s.ToString())
                        .FontSize(12).Bold();
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("d  Establishment number")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text("").FontSize(8);
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("e  Employer identification number (EIN)")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(FormatEin(_data.EmployerEin))
                        .FontSize(11).Bold();
                });
            });

            column.Item().Height(4);

            // Row 3: Employer name and address
            column.Item().Border(1).BorderColor(BorderColor).Column(col =>
            {
                col.Item().Padding(3).Text("f  Employer's name")
                    .FontSize(6).FontColor(LabelColor);
                col.Item().PaddingLeft(5).Text(_data.EmployerName)
                    .FontSize(10).Bold();
                col.Item().PaddingLeft(5).Text(_data.EmployerAddress)
                    .FontSize(8);
                col.Item().PaddingLeft(5).PaddingBottom(4)
                    .Text($"{_data.EmployerCity}, {_data.EmployerState} {_data.EmployerZip}")
                    .FontSize(8);
            });

            column.Item().Height(8);

            // Wage and Tax Totals Section
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("TRANSMITTAL TOTALS").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Box label
                    columns.RelativeColumn(2); // Amount
                    columns.ConstantColumn(20); // Spacer
                    columns.RelativeColumn(3); // Box label
                    columns.RelativeColumn(2); // Amount
                });

                // Row: Box 1 + Box 2
                ComposeTableBoxLabel(table, "1  Wages, tips, other compensation");
                ComposeTableBoxValue(table, _data.TotalWages);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "2  Federal income tax withheld");
                ComposeTableBoxValue(table, _data.TotalFederalTax);

                // Row: Box 3 + Box 4
                ComposeTableBoxLabel(table, "3  Social security wages");
                ComposeTableBoxValue(table, _data.TotalSsWages);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "4  Social security tax withheld");
                ComposeTableBoxValue(table, _data.TotalSsTax);

                // Row: Box 5 + Box 6
                ComposeTableBoxLabel(table, "5  Medicare wages and tips");
                ComposeTableBoxValue(table, _data.TotalMedicareWages);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "6  Medicare tax withheld");
                ComposeTableBoxValue(table, _data.TotalMedicareTax);

                // Boxes 7-14 are intentionally zero. This application does not track
                // social security tips, allocated tips, dependent care benefits,
                // nonqualified plans, deferred compensation, third-party sick pay,
                // or payer-withheld income tax. These boxes are reported as zero
                // because the corresponding benefit/compensation types are not
                // supported by this payroll system.

                // Row: Box 7 + Box 8
                ComposeTableBoxLabel(table, "7  Social security tips");
                ComposeTableBoxValue(table, 0m);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "8  Allocated tips");
                ComposeTableBoxValue(table, 0m);

                // Row: Box 9 + Box 10
                ComposeTableBoxLabel(table, "9  ");
                ComposeTableBoxValue(table, 0m, showZero: false);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "10  Dependent care benefits");
                ComposeTableBoxValue(table, 0m);

                // Row: Box 11 + Box 12a
                ComposeTableBoxLabel(table, "11  Nonqualified plans");
                ComposeTableBoxValue(table, 0m);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "12a  Deferred compensation");
                ComposeTableBoxValue(table, 0m);

                // Row: Box 13 + Box 14
                ComposeTableBoxLabel(table, "13  For third-party sick pay use only");
                ComposeTableBoxValue(table, 0m, showZero: false);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "14  Income tax withheld by payer");
                ComposeTableBoxValue(table, 0m);
            });

            column.Item().Height(8);

            // State and Local section
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("STATE AND LOCAL TAX INFORMATION").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);   // 15 State
                    columns.RelativeColumn(1.5f); // 15 Employer's state ID
                    columns.RelativeColumn(1.5f); // 16 State wages
                    columns.RelativeColumn(1.5f); // 17 State tax
                    columns.RelativeColumn(1.5f); // 18 Local wages
                    columns.RelativeColumn(1.5f); // 19 Local tax
                });

                // Header labels
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("15  State").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("Employer's state ID").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("16  State wages, tips, etc.").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("17  State income tax").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("18  Local wages, tips, etc.").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("19  Local income tax").FontSize(6).FontColor(LabelColor);

                // Data row
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text("OH").FontSize(9).Bold();
                // Box 15: Employer's state withholding account number (NOT the federal EIN)
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(_data.StateWithholdingId).FontSize(7);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(FormatCurrency(_data.TotalStateWages)).FontSize(8).Bold();
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(FormatCurrency(_data.TotalStateTax)).FontSize(8).Bold();
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(FormatCurrency(_data.TotalLocalWages)).FontSize(8).Bold();
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(FormatCurrency(_data.TotalLocalTax)).FontSize(8).Bold();
            });

            column.Item().Height(12);

            // Contact info section
            column.Item().Border(1).BorderColor(BorderColor).Padding(8).Column(contactCol =>
            {
                contactCol.Item().Text("Contact Information")
                    .FontSize(9).Bold().FontColor("#1B3A5C");
                contactCol.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Employer's contact person:").FontSize(7).FontColor(LabelColor);
                        col.Item().Text("").FontSize(8);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Telephone number:").FontSize(7).FontColor(LabelColor);
                        col.Item().Text("").FontSize(8);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Email address:").FontSize(7).FontColor(LabelColor);
                        col.Item().Text("").FontSize(8);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Fax number:").FontSize(7).FontColor(LabelColor);
                        col.Item().Text("").FontSize(8);
                    });
                });
            });
        });
    }

    private static void ComposeTableBoxLabel(TableDescriptor table, string label)
    {
        table.Cell().Border(1).BorderColor(LightBorderColor).Padding(5)
            .Text(label).FontSize(8);
    }

    private static void ComposeTableBoxValue(TableDescriptor table, decimal value, bool showZero = true)
    {
        table.Cell().Border(1).BorderColor(LightBorderColor).Padding(5).AlignRight()
            .Text(showZero || value != 0 ? FormatCurrency(value) : "")
            .FontSize(9).Bold();
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(LightBorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Form W-3  Tax Year {_data.TaxYear}")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignCenter()
                    .Text("Transmittal of Wage and Tax Statements")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignRight()
                    .Text($"Generated {DateTime.Now:MM/dd/yyyy}")
                    .FontSize(7).FontColor("#94A3B8");
            });
        });
    }

    private static string FormatCurrency(decimal value)
    {
        // IRS W-3 instructions prohibit "$" symbols and comma separators in money boxes.
        // Use plain numeric format: digits and decimal point only.
        return value.ToString("0.00");
    }

    private static string FormatEin(string ein)
    {
        if (ein.Length == 9)
            return $"{ein[..2]}-{ein[2..]}";
        return ein;
    }
}

