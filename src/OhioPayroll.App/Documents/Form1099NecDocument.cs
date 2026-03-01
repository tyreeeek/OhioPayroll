using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OhioPayroll.App.Documents.IrsFormHelpers;

namespace OhioPayroll.App.Documents;

public class Form1099NecData
{
    // Payer (company)
    public string PayerName { get; set; } = string.Empty;
    public string PayerAddress { get; set; } = string.Empty;
    public string PayerCity { get; set; } = string.Empty;
    public string PayerState { get; set; } = string.Empty;
    public string PayerZip { get; set; } = string.Empty;
    public string PayerTin { get; set; } = string.Empty;
    public string? PayerPhone { get; set; }

    // Recipient (contractor)
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string RecipientCity { get; set; } = string.Empty;
    public string RecipientState { get; set; } = string.Empty;
    public string RecipientZip { get; set; } = string.Empty;
    public string RecipientTin { get; set; } = string.Empty;
    public bool RecipientTinIsEin { get; set; }

    // Amounts
    public decimal Box1_NonemployeeCompensation { get; set; }
    public bool Box2_DirectSales { get; set; }
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

    private const float LeftCol = 270f;
    private const float RightCol = 270f;
    private const float RowH = 27f;
    private const float TallRowH = 55f;
    private const float B = BorderWidth;

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

            page.Content().Column(col =>
            {
                col.Item().Element(Compose1099Grid);
            });

            page.Footer().Element(c => PageFooter(c, "Form 1099-NEC", _data.TaxYear));
        });
    }

    private void Compose1099Grid(IContainer container)
    {
        container.Border(B).BorderColor("#000000").Column(grid =>
        {
            // ── Row 1: Payer info (left) | Form title + CORRECTED (right)
            grid.Item().Height(TallRowH).Row(row =>
            {
                row.ConstantItem(LeftCol)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("PAYER'S name, street address, city or town, state or province, country, ZIP or foreign postal code, and telephone no.")
                            .FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.PayerName)
                            .FontFamily(DataFont).FontSize(9).Bold().ClampLines(1);
                        col.Item().Text(_data.PayerAddress).FontFamily(DataFont).FontSize(7).ClampLines(1);
                        col.Item().Text($"{_data.PayerCity}, {_data.PayerState} {_data.PayerZip}")
                            .FontFamily(DataFont).FontSize(7).ClampLines(1);
                        if (!string.IsNullOrWhiteSpace(_data.PayerPhone))
                            col.Item().Text($"Tel: {_data.PayerPhone}").FontFamily(DataFont).FontSize(7);
                    });

                row.ConstantItem(RightCol)
                    .Border(B).BorderColor("#000000").Padding(3)
                    .Column(col =>
                    {
                        col.Item().Row(chkRow =>
                        {
                            chkRow.RelativeItem();
                            chkRow.ConstantItem(110).Element(c =>
                                IrsCheckbox(c, "CORRECTED (if checked)", false));
                        });
                        col.Item().PaddingTop(4).Row(titleRow =>
                        {
                            titleRow.RelativeItem().Text(text =>
                            {
                                text.Span("Form ").FontSize(7);
                                text.Span("1099-NEC").FontSize(14).Bold();
                            });
                            titleRow.ConstantItem(35).AlignRight()
                                .Text(_data.TaxYear.ToString()).FontSize(11).Bold();
                        });
                        col.Item().PaddingTop(1).Text("Nonemployee Compensation").FontSize(7);
                    });
            });

            // ── Row 2: TINs (left split) | Box 1 (right)
            grid.Item().Height(RowH).Row(row =>
            {
                row.ConstantItem(LeftCol / 2)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("PAYER'S TIN").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1)
                            .Text(FormatTin(_data.PayerTin))
                            .FontFamily(DataFont).FontSize(ValueFontSize).Bold();
                    });
                row.ConstantItem(LeftCol / 2)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("RECIPIENT'S TIN").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1)
                            .Text(FormatTin(_data.RecipientTin, !_data.RecipientTinIsEin))
                            .FontFamily(DataFont).FontSize(ValueFontSize).Bold();
                    });
                row.ConstantItem(RightCol).Element(c =>
                    IrsAmountBox(c, "1  Nonemployee compensation",
                        FormatMoney(_data.Box1_NonemployeeCompensation)));
            });

            // ── Row 3: Recipient name (left) | Box 2 (right)
            grid.Item().Height(RowH).Row(row =>
            {
                row.ConstantItem(LeftCol)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("RECIPIENT'S name").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.RecipientName)
                            .FontFamily(DataFont).FontSize(ValueFontSize).Bold().ClampLines(1);
                    });
                row.ConstantItem(RightCol)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("2").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Element(c =>
                            IrsCheckbox(c,
                                "Payer made direct sales totaling $5,000 or more of consumer products to recipient for resale",
                                _data.Box2_DirectSales));
                    });
            });

            // ── Row 4: Street address (left) | Box 4 (right)
            grid.Item().Height(RowH).Row(row =>
            {
                row.ConstantItem(LeftCol)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Street address (including apt. no.)").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.RecipientAddress)
                            .FontFamily(DataFont).FontSize(8).ClampLines(1);
                    });
                row.ConstantItem(RightCol).Element(c =>
                    IrsAmountBox(c, "4  Federal income tax withheld",
                        FormatMoney(_data.Box4_FederalTaxWithheld)));
            });

            // ── Row 5: City/State/ZIP (left) | Boxes 5-6 (right)
            grid.Item().Height(RowH).Row(row =>
            {
                row.ConstantItem(LeftCol)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("City or town, state or province, country, and ZIP or foreign postal code")
                            .FontSize(LabelFontSize);
                        col.Item().PaddingTop(1)
                            .Text($"{_data.RecipientCity}, {_data.RecipientState} {_data.RecipientZip}")
                            .FontFamily(DataFont).FontSize(8).ClampLines(1);
                    });
                row.ConstantItem(RightCol / 2)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("5  State tax withheld").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.StateTaxWithheld)).FontFamily(DataFont).FontSize(8);
                    });
                row.ConstantItem(RightCol / 2)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("6  State income").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.StateIncome)).FontFamily(DataFont).FontSize(8);
                    });
            });

            // ── Row 6: Account number (left split) | Box 7 (right)
            grid.Item().Height(RowH).Row(row =>
            {
                row.ConstantItem(LeftCol / 2)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Account number (see instructions)").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.AccountNumber)
                            .FontFamily(DataFont).FontSize(8);
                    });
                row.ConstantItem(LeftCol / 2)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Element(c =>
                            IrsCheckbox(c, "FATCA filing requirement", false));
                    });
                row.ConstantItem(RightCol)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("7  State/Payer's state no.").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1)
                            .Text($"{_data.StateCode} / {_data.StatePayerNo}")
                            .FontFamily(DataFont).FontSize(8);
                    });
            });

            // ── Copy designation
            grid.Item().PaddingTop(3).PaddingBottom(2).PaddingLeft(4)
                .Text("Copy B \u2014 For Recipient").FontSize(6).Bold();
        });
    }
}
