using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents;

public class Form1099NecData
{
    // Payer (company)
    public string PayerName { get; set; } = string.Empty;
    public string PayerAddress { get; set; } = string.Empty;
    public string PayerCity { get; set; } = string.Empty;
    public string PayerState { get; set; } = string.Empty;
    public string PayerZip { get; set; } = string.Empty;
    public string PayerTin { get; set; } = string.Empty; // EIN raw digits
    public string? PayerPhone { get; set; }

    // Recipient (contractor)
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string RecipientCity { get; set; } = string.Empty;
    public string RecipientState { get; set; } = string.Empty;
    public string RecipientZip { get; set; } = string.Empty;
    public string RecipientTin { get; set; } = string.Empty; // SSN or EIN raw digits
    public bool RecipientTinIsEin { get; set; }

    // Amounts
    public decimal Box1_NonemployeeCompensation { get; set; }
    public decimal Box4_FederalTaxWithheld { get; set; }

    // State
    public string StateName { get; set; } = "Ohio";
    public string StateCode { get; set; } = "OH";
    public string StatePayerNo { get; set; } = string.Empty;
    public decimal StateIncome { get; set; }
    public decimal StateTaxWithheld { get; set; }

    public int TaxYear { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
}

public class Form1099NecDocument : IDocument
{
    private readonly Form1099NecData _data;

    // Color palette matching project style
    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string LabelColor = "#64748B";
    private static readonly string BorderColor = "#000000";
    private static readonly string LightBorderColor = "#CBD5E1";
    private static readonly string BoxBg = "#FFFFFF";
    private static readonly string SectionHeaderBg = "#E8EDF2";

    public Form1099NecDocument(Form1099NecData data)
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
                    col.Item().Text($"Form 1099-NEC  {_data.TaxYear}")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text("Nonemployee Compensation")
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
            // Row 1: Payer's TIN + Recipient's TIN
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("PAYER'S TIN")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(FormatEin(_data.PayerTin))
                        .FontSize(11).Bold();
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("RECIPIENT'S TIN")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(FormatTin(_data.RecipientTin, _data.RecipientTinIsEin))
                        .FontSize(11).Bold();
                });
            });

            column.Item().Height(4);

            // Row 2: Payer info + Box 1 (Nonemployee compensation)
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("PAYER'S name, street address, city or town, state or province, country, ZIP or foreign postal code, and telephone no.")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).Text(_data.PayerName)
                        .FontSize(9).Bold();
                    col.Item().PaddingLeft(5).Text(_data.PayerAddress)
                        .FontSize(8);
                    col.Item().PaddingLeft(5).Text($"{_data.PayerCity}, {_data.PayerState} {_data.PayerZip}")
                        .FontSize(8);
                    if (!string.IsNullOrWhiteSpace(_data.PayerPhone))
                    {
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text($"Tel: {_data.PayerPhone}")
                            .FontSize(8);
                    }
                    else
                    {
                        col.Item().PaddingBottom(4).Text("").FontSize(4);
                    }
                });
                row.ConstantItem(4);
                row.ConstantItem(240).Column(rightCol =>
                {
                    ComposeBox(rightCol.Item(), "1  Nonemployee compensation",
                        _data.Box1_NonemployeeCompensation);
                });
            });

            column.Item().Height(4);

            // Row 3: Recipient info + Boxes 2 and 4
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(recipCol =>
                {
                    recipCol.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Padding(3).Text("RECIPIENT'S name")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text(_data.RecipientName)
                            .FontSize(10).Bold();
                    });

                    recipCol.Item().Height(4);

                    recipCol.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Padding(3).Text("Street address (including apt. no.)")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text(_data.RecipientAddress)
                            .FontSize(8);
                    });

                    recipCol.Item().Height(4);

                    recipCol.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Padding(3).Text("City or town, state or province, country, and ZIP or foreign postal code")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text($"{_data.RecipientCity}, {_data.RecipientState} {_data.RecipientZip}")
                            .FontSize(8);
                    });
                });

                row.ConstantItem(4);

                row.ConstantItem(240).Column(rightCol =>
                {
                    // Box 2: Payer made direct sales totaling $5,000 or more (checkbox)
                    rightCol.Item().Border(1).BorderColor(BorderColor).Background(BoxBg).Column(col =>
                    {
                        col.Item().Padding(3).Text("2  Payer made direct sales totaling $5,000 or more of consumer products to recipient for resale")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text("[ ]").FontSize(10);
                    });

                    rightCol.Item().Height(4);

                    // Box 3: (reserved)
                    rightCol.Item().Border(1).BorderColor(BorderColor).Background(BoxBg).Column(col =>
                    {
                        col.Item().Padding(3).Text("3")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text("").FontSize(8);
                    });

                    rightCol.Item().Height(4);

                    // Box 4: Federal income tax withheld
                    ComposeBox(rightCol.Item(), "4  Federal income tax withheld",
                        _data.Box4_FederalTaxWithheld);
                });
            });

            column.Item().Height(4);

            // Row 4: Account number + 2nd TIN notice
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("Account number (see instructions)")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(_data.AccountNumber)
                        .FontSize(8);
                });
                row.ConstantItem(4);
                row.ConstantItem(240).Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("2nd TIN not.")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text("").FontSize(8);
                });
            });

            column.Item().Height(8);

            // State Tax Information Section (Boxes 5-7)
            column.Item().Background(SectionHeaderBg).Padding(6)
                .Text("STATE TAX INFORMATION").FontSize(10).Bold().FontColor("#1B3A5C");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);     // 5 State
                    columns.RelativeColumn(1.5f);   // 6 State payer's no.
                    columns.RelativeColumn(2);      // 7 State income
                    columns.RelativeColumn(2);      // State tax withheld
                });

                // Header labels
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("5  State").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("6  State/Payer's state no.").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("7  State income").FontSize(6).FontColor(LabelColor);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                    .Text("State tax withheld").FontSize(6).FontColor(LabelColor);

                // Data row
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(_data.StateCode).FontSize(9).Bold();
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(_data.StatePayerNo).FontSize(7);
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(FormatAmount(_data.StateIncome)).FontSize(8).Bold();
                table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                    .Text(FormatAmount(_data.StateTaxWithheld)).FontSize(8).Bold();
            });
        });
    }

    private static void ComposeBox(IContainer container, string label, decimal value, bool showZero = true)
    {
        container.Border(1).BorderColor(BorderColor).Background(BoxBg).Column(col =>
        {
            col.Item().Padding(3).Text(label)
                .FontSize(6).FontColor(LabelColor);
            col.Item().PaddingLeft(5).PaddingBottom(4).AlignRight().PaddingRight(5)
                .Text(showZero || value != 0 ? FormatAmount(value) : "")
                .FontSize(10).Bold();
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(LightBorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Form 1099-NEC  Tax Year {_data.TaxYear}")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignCenter()
                    .Text("Copy B - For Recipient")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignRight()
                    .Text($"Generated {DateTime.Now:MM/dd/yyyy}")
                    .FontSize(7).FontColor("#94A3B8");
            });
        });
    }

    private static string FormatAmount(decimal value)
    {
        // IRS 1099 instructions prohibit "$" symbols and comma separators in money boxes.
        // Use plain numeric format: digits and decimal point only.
        return value.ToString("0.00");
    }

    private static string FormatEin(string ein)
    {
        if (ein.Length == 9)
            return $"{ein[..2]}-{ein[2..]}";
        return ein;
    }

    private static string FormatTin(string tin, bool isEin)
    {
        if (isEin && tin.Length == 9)
            return $"{tin[..2]}-{tin[2..]}";
        if (!isEin && tin.Length == 9)
            return $"{tin[..3]}-{tin[3..5]}-{tin[5..]}";
        return tin;
    }
}
