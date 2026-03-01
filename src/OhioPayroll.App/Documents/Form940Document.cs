using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OhioPayroll.App.Documents.IrsFormHelpers;

namespace OhioPayroll.App.Documents;

public class Form940Data
{
    public int TaxYear { get; set; }
    public string EmployerEin { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string EmployerAddress { get; set; } = string.Empty;
    public string EmployerCity { get; set; } = string.Empty;
    public string EmployerState { get; set; } = "OH";
    public string EmployerZip { get; set; } = string.Empty;

    public int EmployeeCount { get; set; }
    public string Line1a_State { get; set; } = "OH";
    public decimal Line3_TotalPayments { get; set; }
    public decimal Line4_ExemptPayments { get; set; }
    public decimal Line5_TaxableFutaWages { get; set; }
    public decimal Line6_FutaTaxBeforeAdjustments { get; set; }
    public decimal Line7_Adjustments { get; set; }
    public decimal Line8_TotalFutaTax { get; set; }
    public decimal Line12_TotalDeposits { get; set; }
    public decimal Line14_BalanceDue { get; set; }

    public decimal Q1Liability { get; set; }
    public decimal Q2Liability { get; set; }
    public decimal Q3Liability { get; set; }
    public decimal Q4Liability { get; set; }
}

public class Form940Document : IDocument
{
    private readonly Form940Data _data;

    private const float B = BorderWidth;

    public Form940Document(Form940Data data)
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
                col.Item().Element(ComposeForm940);
            });

            page.Footer().Element(c => PageFooter(c, "Form 940", _data.TaxYear));
        });
    }

    private void ComposeForm940(IContainer container)
    {
        container.Column(main =>
        {
            // ── Form header ──────────────────────────────────────────────
            main.Item().Element(c =>
                IrsFormHeader(c, "940", _data.TaxYear,
                    "Employer's Annual Federal Unemployment (FUTA) Tax Return"));

            // ── Employer info ────────────────────────────────────────────
            main.Item().Element(c =>
                EmployerInfoBlock(c, _data.EmployerEin, _data.EmployerName,
                    _data.EmployerAddress, _data.EmployerCity,
                    _data.EmployerState, _data.EmployerZip));

            // ── Part 1: Tell us about your return ────────────────────────
            main.Item().Element(c => PartHeader(c, "Part 1: Tell us about your return"));

            main.Item().Element(c =>
                LineItemRow(c, "1a",
                    $"If you had to pay state unemployment tax in one state only, enter the state abbreviation: {_data.Line1a_State}",
                    "", false));
            main.Item().Element(c =>
                LineItemRow(c, "1b",
                    "If you had to pay state unemployment tax in more than one state, you are a multi-state employer",
                    ""));
            main.Item().Element(c =>
                LineItemRow(c, "2",
                    "If you paid wages in a state that is subject to CREDIT REDUCTION, check here",
                    ""));

            // ── Part 2: Determine your FUTA tax before adjustments ───────
            main.Item().Element(c =>
                PartHeader(c, "Part 2: Determine your FUTA tax before adjustments (lines 3\u20138)"));

            main.Item().Element(c =>
                LineItemRow(c, "3",
                    "Total payments to all employees",
                    FormatMoney(_data.Line3_TotalPayments)));
            main.Item().Element(c =>
                LineItemRow(c, "4",
                    "Payments exempt from FUTA tax",
                    FormatMoney(_data.Line4_ExemptPayments)));
            main.Item().Element(c =>
                LineItemRow(c, "5",
                    "Total of payments made to each employee in excess of $7,000",
                    FormatMoney(_data.Line3_TotalPayments - _data.Line5_TaxableFutaWages)));
            main.Item().Element(c =>
                LineItemRow(c, "6",
                    "Subtotal (line 4 + line 5 = line 6)",
                    FormatMoney(_data.Line4_ExemptPayments +
                        (_data.Line3_TotalPayments - _data.Line5_TaxableFutaWages))));
            main.Item().Element(c =>
                LineItemRow(c, "7",
                    "Total taxable FUTA wages (line 3 \u2212 line 6 = line 7)",
                    FormatMoney(_data.Line5_TaxableFutaWages)));
            main.Item().Element(c =>
                LineItemRow(c, "8",
                    "FUTA tax before adjustments (line 7 \u00D7 0.006 = line 8)",
                    FormatMoney(_data.Line6_FutaTaxBeforeAdjustments)));

            // ── Part 3: Determine your adjustments ───────────────────────
            main.Item().Element(c =>
                PartHeader(c, "Part 3: Determine your adjustments (lines 9\u201311)"));

            main.Item().Element(c =>
                LineItemRow(c, "9",
                    "If ALL of the taxable FUTA wages you paid were excluded from state unemployment tax, multiply line 7 by 0.054",
                    FormatMoney(0m)));
            main.Item().Element(c =>
                LineItemRow(c, "10",
                    "If SOME of the taxable FUTA wages you paid were excluded from state unemployment tax",
                    FormatMoney(0m)));
            main.Item().Element(c =>
                LineItemRow(c, "11",
                    "If credit reduction applies, enter the total from Schedule A (Form 940)",
                    FormatMoney(_data.Line7_Adjustments)));

            // ── Part 4: Determine your FUTA tax and balance due ──────────
            main.Item().Element(c =>
                PartHeader(c, "Part 4: Determine your FUTA tax and balance due or overpayment (lines 12\u201315)"));

            main.Item().Element(c =>
                LineItemRow(c, "12",
                    "Total FUTA tax after adjustments (lines 8 + 9 + 10 + 11 = line 12)",
                    FormatMoney(_data.Line8_TotalFutaTax)));
            main.Item().Element(c =>
                LineItemRow(c, "13",
                    "FUTA tax deposited for the year, including any overpayment applied from a prior year",
                    FormatMoney(_data.Line12_TotalDeposits)));

            var balance = _data.Line14_BalanceDue;
            var balanceLabel = balance >= 0
                ? "Balance due (If line 12 is more than line 13, enter the excess on line 14)"
                : "Overpayment (If line 13 is more than line 12, enter the excess on line 14)";
            main.Item().Element(c =>
                LineItemRow(c, "14", balanceLabel,
                    FormatMoney(Math.Abs(balance))));
            main.Item().Element(c =>
                LineItemRow(c, "15",
                    "Overpayment: Check one \u25A1 Apply to next return  \u25A1 Send a refund",
                    balance < 0 ? FormatMoney(Math.Abs(balance)) : "", false));

            // ── Part 5: Report your FUTA tax liability by quarter ────────
            main.Item().Element(c =>
                PartHeader(c, "Part 5: Report your FUTA tax liability by quarter (only if line 12 is more than $500)"));

            main.Item().PaddingTop(4).Border(B).BorderColor("#000000").Column(qCol =>
            {
                qCol.Item().MinHeight(16).Row(row =>
                {
                    row.ConstantItem(LineNumWidth).AlignMiddle().PaddingLeft(4)
                        .Text("16a").FontSize(8).Bold();
                    row.RelativeItem().AlignMiddle().PaddingLeft(2)
                        .Text($"1st quarter (January 1 \u2013 March 31)").FontSize(7.5f);
                    row.ConstantItem(AmountBoxWidth)
                        .Border(B).BorderColor("#000000")
                        .Padding(2).AlignRight().AlignMiddle()
                        .Text(FormatMoney(_data.Q1Liability)).FontFamily(DataFont).FontSize(8);
                });
                qCol.Item().MinHeight(16).Row(row =>
                {
                    row.ConstantItem(LineNumWidth).AlignMiddle().PaddingLeft(4)
                        .Text("16b").FontSize(8).Bold();
                    row.RelativeItem().AlignMiddle().PaddingLeft(2)
                        .Text($"2nd quarter (April 1 \u2013 June 30)").FontSize(7.5f);
                    row.ConstantItem(AmountBoxWidth)
                        .Border(B).BorderColor("#000000")
                        .Padding(2).AlignRight().AlignMiddle()
                        .Text(FormatMoney(_data.Q2Liability)).FontFamily(DataFont).FontSize(8);
                });
                qCol.Item().MinHeight(16).Row(row =>
                {
                    row.ConstantItem(LineNumWidth).AlignMiddle().PaddingLeft(4)
                        .Text("16c").FontSize(8).Bold();
                    row.RelativeItem().AlignMiddle().PaddingLeft(2)
                        .Text($"3rd quarter (July 1 \u2013 September 30)").FontSize(7.5f);
                    row.ConstantItem(AmountBoxWidth)
                        .Border(B).BorderColor("#000000")
                        .Padding(2).AlignRight().AlignMiddle()
                        .Text(FormatMoney(_data.Q3Liability)).FontFamily(DataFont).FontSize(8);
                });
                qCol.Item().MinHeight(16).Row(row =>
                {
                    row.ConstantItem(LineNumWidth).AlignMiddle().PaddingLeft(4)
                        .Text("16d").FontSize(8).Bold();
                    row.RelativeItem().AlignMiddle().PaddingLeft(2)
                        .Text($"4th quarter (October 1 \u2013 December 31)").FontSize(7.5f);
                    row.ConstantItem(AmountBoxWidth)
                        .Border(B).BorderColor("#000000")
                        .Padding(2).AlignRight().AlignMiddle()
                        .Text(FormatMoney(_data.Q4Liability)).FontFamily(DataFont).FontSize(8);
                });

                var totalQuarterly = _data.Q1Liability + _data.Q2Liability
                    + _data.Q3Liability + _data.Q4Liability;
                qCol.Item().MinHeight(16).BorderTop(B).BorderColor("#000000").Row(row =>
                {
                    row.ConstantItem(LineNumWidth).AlignMiddle().PaddingLeft(4)
                        .Text("17").FontSize(8).Bold();
                    row.RelativeItem().AlignMiddle().PaddingLeft(2)
                        .Text("Total tax liability for the year (lines 16a + 16b + 16c + 16d = line 17)  Must equal line 12.")
                        .FontSize(7.5f);
                    row.ConstantItem(AmountBoxWidth)
                        .Border(B).BorderColor("#000000")
                        .Padding(2).AlignRight().AlignMiddle()
                        .Text(FormatMoney(totalQuarterly)).FontFamily(DataFont).FontSize(8).Bold();
                });
            });

            // ── Part 6: May we speak with your third-party designee? ─────
            main.Item().Element(c =>
                PartHeader(c, "Part 6: May we speak with your third-party designee?"));
            main.Item().MinHeight(16).Row(row =>
            {
                row.RelativeItem().PaddingLeft(4).AlignMiddle()
                    .Text("Do you want to allow an employee, a paid tax preparer, or another person to discuss this return with the IRS?")
                    .FontSize(7.5f);
                row.ConstantItem(100).Row(chkRow =>
                {
                    chkRow.RelativeItem().Element(c => IrsCheckbox(c, "Yes", false));
                    chkRow.RelativeItem().Element(c => IrsCheckbox(c, "No", true));
                });
            });

            // ── Part 7: Sign here ────────────────────────────────────────
            main.Item().Element(c =>
                PartHeader(c, "Part 7: Sign here (You MUST complete both pages of this form and SIGN it.)"));
            main.Item().PaddingTop(2).MinHeight(30).Row(row =>
            {
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(3)
                    .Column(col =>
                    {
                        col.Item().Text("Sign your name here").FontSize(LabelFontSize);
                        col.Item().PaddingTop(8).Text("").FontSize(ValueFontSize);
                    });
                row.ConstantItem(80)
                    .Border(B).BorderColor("#000000").Padding(3)
                    .Column(col =>
                    {
                        col.Item().Text("Date").FontSize(LabelFontSize);
                        col.Item().PaddingTop(8).Text("").FontSize(ValueFontSize);
                    });
            });
            main.Item().MinHeight(16).Row(row =>
            {
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(3)
                    .Column(col =>
                    {
                        col.Item().Text("Print your name here").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(ValueFontSize);
                    });
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(3)
                    .Column(col =>
                    {
                        col.Item().Text("Print your title here").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(ValueFontSize);
                    });
            });

            main.Item().PaddingTop(4);
        });
    }
}
