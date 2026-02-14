using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents;

public class Form1096Data
{
    public string FilerName { get; set; } = string.Empty;
    public string FilerAddress { get; set; } = string.Empty;
    public string FilerCity { get; set; } = string.Empty;
    public string FilerState { get; set; } = string.Empty;
    public string FilerZip { get; set; } = string.Empty;
    public string FilerTin { get; set; } = string.Empty; // EIN raw digits
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    public int Box3_TotalForms { get; set; }
    public decimal Box4_FederalTaxWithheld { get; set; }
    public decimal Box5_TotalAmount { get; set; }

    public int TaxYear { get; set; }
}

public class Form1096Document : IDocument
{
    private readonly Form1096Data _data;

    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string LabelColor = "#64748B";
    private static readonly string BorderColor = "#000000";
    private static readonly string LightBorderColor = "#CBD5E1";
    private static readonly string SectionHeaderBg = "#E8EDF2";

    public Form1096Document(Form1096Data data)
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
                    col.Item().Text($"Form 1096  {_data.TaxYear}")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text("Annual Summary and Transmittal of U.S. Information Returns")
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
            // Row 1: Filer's name
            column.Item().Border(1).BorderColor(BorderColor).Column(col =>
            {
                col.Item().Padding(3).Text("Filer's name")
                    .FontSize(6).FontColor(LabelColor);
                col.Item().PaddingLeft(5).PaddingBottom(4)
                    .Text(_data.FilerName)
                    .FontSize(10).Bold();
            });

            column.Item().Height(4);

            // Row 2: Filer's address
            column.Item().Border(1).BorderColor(BorderColor).Column(col =>
            {
                col.Item().Padding(3).Text("Street address (including room or suite no.)")
                    .FontSize(6).FontColor(LabelColor);
                col.Item().PaddingLeft(5).PaddingBottom(4)
                    .Text(_data.FilerAddress)
                    .FontSize(9);
            });

            column.Item().Height(4);

            // Row 3: City, state, ZIP
            column.Item().Border(1).BorderColor(BorderColor).Column(col =>
            {
                col.Item().Padding(3).Text("City or town, state or province, country, and ZIP or foreign postal code")
                    .FontSize(6).FontColor(LabelColor);
                col.Item().PaddingLeft(5).PaddingBottom(4)
                    .Text($"{_data.FilerCity}, {_data.FilerState} {_data.FilerZip}")
                    .FontSize(9);
            });

            column.Item().Height(4);

            // Row 4: Filer's TIN + Contact info
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("Filer's TIN (Employer identification number)")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(FormatEin(_data.FilerTin))
                        .FontSize(11).Bold();
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("Name of person to contact")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(_data.ContactName ?? "")
                        .FontSize(9);
                });
            });

            column.Item().Height(4);

            // Row 5: Contact telephone, email
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("Telephone number")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(_data.ContactPhone ?? "")
                        .FontSize(9);
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("Email address")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(_data.ContactEmail ?? "")
                        .FontSize(9);
                });
            });

            column.Item().Height(8);

            // Totals Section
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

                // Row: Box 3 + Box 4
                ComposeTableBoxLabel(table, "3  Total number of forms");
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(5).AlignRight()
                    .Text(_data.Box3_TotalForms.ToString())
                    .FontSize(9).Bold();
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "4  Federal income tax withheld");
                ComposeTableBoxValue(table, _data.Box4_FederalTaxWithheld);

                // Row: Box 5 + Box 6
                ComposeTableBoxLabel(table, "5  Total amount reported with this Form 1096");
                ComposeTableBoxValue(table, _data.Box5_TotalAmount);
                table.Cell().Text("");
                ComposeTableBoxLabel(table, "6  Check type of return filed");
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(5)
                    .Text("").FontSize(9);
            });

            column.Item().Height(8);

            // Type of Return section
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("TYPE OF RETURN").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Border(1).BorderColor(BorderColor).Padding(8).Column(typeCol =>
            {
                typeCol.Item().Text("Check only one box below to indicate the type of form being filed.")
                    .FontSize(8).FontColor(LabelColor);

                typeCol.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    // Row 1 of form types
                    ComposeFormTypeCell(table, "1097-BTC", false);
                    ComposeFormTypeCell(table, "1098", false);
                    ComposeFormTypeCell(table, "1098-C", false);
                    ComposeFormTypeCell(table, "1098-E", false);

                    // Row 2
                    ComposeFormTypeCell(table, "1098-Q", false);
                    ComposeFormTypeCell(table, "1098-T", false);
                    ComposeFormTypeCell(table, "1099-A", false);
                    ComposeFormTypeCell(table, "1099-B", false);

                    // Row 3
                    ComposeFormTypeCell(table, "1099-C", false);
                    ComposeFormTypeCell(table, "1099-CAP", false);
                    ComposeFormTypeCell(table, "1099-DIV", false);
                    ComposeFormTypeCell(table, "1099-G", false);

                    // Row 4
                    ComposeFormTypeCell(table, "1099-INT", false);
                    ComposeFormTypeCell(table, "1099-K", false);
                    ComposeFormTypeCell(table, "1099-LS", false);
                    ComposeFormTypeCell(table, "1099-LTC", false);

                    // Row 5
                    ComposeFormTypeCell(table, "1099-MISC", false);
                    ComposeFormTypeCell(table, "1099-NEC", true); // This is the one we use
                    ComposeFormTypeCell(table, "1099-OID", false);
                    ComposeFormTypeCell(table, "1099-PATR", false);

                    // Row 6
                    ComposeFormTypeCell(table, "1099-Q", false);
                    ComposeFormTypeCell(table, "1099-R", false);
                    ComposeFormTypeCell(table, "1099-S", false);
                    ComposeFormTypeCell(table, "1099-SA", false);

                    // Row 7
                    ComposeFormTypeCell(table, "1099-SB", false);
                    ComposeFormTypeCell(table, "W-2G", false);
                    ComposeFormTypeCell(table, "3921", false);
                    ComposeFormTypeCell(table, "3922", false);
                });
            });

            column.Item().Height(12);

            // Instructions note
            column.Item().Border(1).BorderColor(BorderColor).Padding(8).Column(noteCol =>
            {
                noteCol.Item().Text("Instructions")
                    .FontSize(9).Bold().FontColor("#1B3A5C");
                noteCol.Item().PaddingTop(4)
                    .Text("This form must be filed with the IRS when submitting paper copies of Form 1099-NEC. " +
                          "If filing electronically, this form is not required. " +
                          "Mail Form 1096 with Copy A of Form(s) 1099 to the appropriate IRS service center.")
                    .FontSize(8).FontColor(LabelColor);
            });
        });
    }

    private static void ComposeFormTypeCell(TableDescriptor table, string formName, bool isChecked)
    {
        if (isChecked)
        {
            table.Cell().Border(1).BorderColor(LightBorderColor).Background("#D4EDDA").Padding(4)
                .Text($"[X] {formName}").FontSize(8).Bold();
        }
        else
        {
            table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                .Text($"[ ] {formName}").FontSize(8);
        }
    }

    private static void ComposeTableBoxLabel(TableDescriptor table, string label)
    {
        table.Cell().Border(1).BorderColor(LightBorderColor).Padding(5)
            .Text(label).FontSize(8);
    }

    private static void ComposeTableBoxValue(TableDescriptor table, decimal value, bool showZero = true)
    {
        table.Cell().Border(1).BorderColor(LightBorderColor).Padding(5).AlignRight()
            .Text(showZero || value != 0 ? FormatAmount(value) : "")
            .FontSize(9).Bold();
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(LightBorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Form 1096  Tax Year {_data.TaxYear}")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignCenter()
                    .Text("Annual Summary and Transmittal of U.S. Information Returns")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignRight()
                    .Text($"Generated {DateTime.Now:MM/dd/yyyy}")
                    .FontSize(7).FontColor("#94A3B8");
            });
        });
    }

    private static string FormatAmount(decimal value)
    {
        // IRS 1096 instructions prohibit "$" symbols and comma separators in money boxes.
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
