using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OhioPayroll.App.Documents.IrsFormHelpers;

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

    private const float B = BorderWidth;

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
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Content().Column(col =>
            {
                col.Item().Element(ComposeForm941);
            });

            page.Footer().Element(c =>
                PageFooter(c, $"Form 941 (Q{_data.Quarter})", _data.TaxYear));
        });
    }

    private void ComposeForm941(IContainer container)
    {
        container.Column(main =>
        {
            // ── Form header with quarter checkboxes ──────────────────────
            main.Item().Column(hdr =>
            {
                hdr.Item().Row(row =>
                {
                    row.RelativeItem().Column(titleCol =>
                    {
                        titleCol.Item().Text(text =>
                        {
                            text.Span("Form ").FontSize(10);
                            text.Span("941").FontSize(14).Bold();
                            text.Span(" for ").FontSize(10);
                            text.Span(_data.TaxYear.ToString()).FontSize(14).Bold();
                            text.Span(": ").FontSize(10);
                            text.Span("Employer's QUARTERLY Federal Tax Return").FontSize(8);
                        });
                        titleCol.Item().PaddingTop(1)
                            .Text("Department of the Treasury \u2014 Internal Revenue Service")
                            .FontSize(6.5f).Italic();
                    });
                });

                // Quarter selection checkboxes
                hdr.Item().PaddingTop(3).Row(row =>
                {
                    row.ConstantItem(100).Text("Report for this Quarter of " + _data.TaxYear)
                        .FontSize(7).Bold();
                    row.RelativeItem().Row(qRow =>
                    {
                        qRow.RelativeItem().Element(c =>
                            IrsCheckbox(c, "1: Jan, Feb, Mar", _data.Quarter == 1));
                        qRow.RelativeItem().Element(c =>
                            IrsCheckbox(c, "2: Apr, May, Jun", _data.Quarter == 2));
                        qRow.RelativeItem().Element(c =>
                            IrsCheckbox(c, "3: Jul, Aug, Sep", _data.Quarter == 3));
                        qRow.RelativeItem().Element(c =>
                            IrsCheckbox(c, "4: Oct, Nov, Dec", _data.Quarter == 4));
                    });
                });

                hdr.Item().PaddingTop(3).LineHorizontal(1).LineColor("#000000");
            });

            // ── Employer info ────────────────────────────────────────────
            main.Item().Element(c =>
                EmployerInfoBlock(c, _data.EmployerEin, _data.EmployerName));

            // ── Part 1: Answer these questions for this quarter ──────────
            main.Item().Element(c =>
                PartHeader(c, "Part 1: Answer these questions for this quarter."));

            // Line 1
            main.Item().Element(c =>
                LineItemRow(c, "1",
                    "Number of employees who received wages, tips, or other compensation for the pay period including: " +
                    GetQuarterMonth12(),
                    _data.NumberOfEmployees.ToString()));

            // Line 2
            main.Item().Element(c =>
                LineItemRow(c, "2",
                    "Wages, tips, and other compensation",
                    FormatMoney(_data.Line2_WagesTipsCompensation)));

            // Line 3
            main.Item().Element(c =>
                LineItemRow(c, "3",
                    "Federal income tax withheld from wages, tips, and other compensation",
                    FormatMoney(_data.Line3_FederalTaxWithheld)));

            // Line 4
            main.Item().Element(c =>
                LineItemRow(c, "4",
                    "If no wages, tips, and other compensation are subject to social security or Medicare tax, check and go to line 6",
                    "", false));

            // Line 5a (with rate column)
            main.Item().Element(c =>
                LineItemRowWithRate(c, "5a",
                    "Taxable social security wages",
                    FormatMoney(_data.Line5a_TaxableSsWages),
                    "0.124",
                    FormatMoney(_data.Line5a_SsTaxDue)));

            // Line 5b (with rate column)
            main.Item().Element(c =>
                LineItemRowWithRate(c, "5b",
                    "Taxable social security tips",
                    FormatMoney(0m),
                    "0.124",
                    FormatMoney(0m)));

            // Line 5c (with rate column)
            main.Item().Element(c =>
                LineItemRowWithRate(c, "5c",
                    "Taxable Medicare wages & tips",
                    FormatMoney(_data.Line5c_TaxableMedicareWages),
                    "0.029",
                    FormatMoney(_data.Line5c_MedicareTaxDue)));

            // Line 5d (with rate column)
            main.Item().Element(c =>
                LineItemRowWithRate(c, "5d",
                    "Taxable wages & tips subject to Additional Medicare Tax withholding",
                    FormatMoney(0m),
                    "0.009",
                    FormatMoney(0m)));

            // Line 5e
            main.Item().Element(c =>
                LineItemRow(c, "5e",
                    "Total social security and Medicare taxes. Add Column 2 from lines 5a, 5a(i), 5b, 5c, and 5d",
                    FormatMoney(_data.Line5a_SsTaxDue + _data.Line5c_MedicareTaxDue)));

            // Line 6
            main.Item().Element(c =>
                LineItemRow(c, "6",
                    "Total taxes before adjustments. Add lines 3 and 5e",
                    FormatMoney(_data.Line6_TotalTaxesBeforeAdjustments)));

            // Lines 7-9 (adjustments)
            main.Item().Element(c =>
                LineItemRow(c, "7",
                    "Current quarter's adjustment for fractions of cents",
                    FormatMoney(0m)));
            main.Item().Element(c =>
                LineItemRow(c, "8",
                    "Current quarter's adjustment for sick pay",
                    FormatMoney(0m)));
            main.Item().Element(c =>
                LineItemRow(c, "9",
                    "Current quarter's adjustments for tips and group-term life insurance",
                    FormatMoney(0m)));

            // Line 10
            main.Item().Element(c =>
                LineItemRow(c, "10",
                    "Total taxes after adjustments. Combine lines 6 through 9",
                    FormatMoney(_data.Line10_TotalTaxes)));

            // Line 11
            main.Item().Element(c =>
                LineItemRow(c, "11",
                    "Qualified small business payroll tax credit for increasing research activities. Attach Form 8974",
                    FormatMoney(0m)));

            // Line 12
            main.Item().Element(c =>
                LineItemRow(c, "12",
                    "Total taxes after adjustments and credits. Subtract line 11 from line 10",
                    FormatMoney(_data.Line10_TotalTaxes)));

            // Line 13
            main.Item().Element(c =>
                LineItemRow(c, "13",
                    "Total deposits for this quarter, including overpayment applied from a prior quarter",
                    FormatMoney(_data.Line11_TotalDeposits)));

            // Line 14
            var line14 = _data.Line14_BalanceDueOrOverpayment;
            var line14Label = line14 >= 0
                ? "Balance due. If line 12 is more than line 13, enter the difference and see instructions"
                : "Overpayment. If line 13 is more than line 12, enter the difference";
            main.Item().Element(c =>
                LineItemRow(c, "14", line14Label,
                    FormatMoney(Math.Abs(line14))));

            // Line 15
            main.Item().Element(c =>
                LineItemRow(c, "15",
                    "Overpayment: Check one  \u25A1 Apply to next return  \u25A1 Send a refund",
                    line14 < 0 ? FormatMoney(Math.Abs(line14)) : "", false));

            // ── Part 2: Tell us about your deposit schedule ─────────────
            main.Item().Element(c =>
                PartHeader(c, "Part 2: Tell us about your deposit schedule and tax liability for this quarter."));
            main.Item().MinHeight(16).PaddingLeft(4).Row(row =>
            {
                row.RelativeItem().AlignMiddle()
                    .Text("If line 12 is less than $2,500, don\u2019t complete Part 2 and go to Part 3.")
                    .FontSize(7.5f);
            });
            main.Item().MinHeight(16).PaddingLeft(4).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Row(chkRow =>
                {
                    chkRow.RelativeItem().Element(c =>
                        IrsCheckbox(c, "Line 12 on this return is less than $2,500. Go to Part 3.", false));
                });
            });
            main.Item().MinHeight(16).PaddingLeft(4).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Row(chkRow =>
                {
                    chkRow.RelativeItem().Element(c =>
                        IrsCheckbox(c, "You were a monthly schedule depositor for the entire quarter. Enter your tax liability for each month and total.", false));
                });
            });

            // ── Part 3: Tell us about your business ─────────────────────
            main.Item().Element(c =>
                PartHeader(c, "Part 3: Tell us about your business. If a question does NOT apply to your business, leave it blank."));
            main.Item().MinHeight(16).PaddingLeft(4).Row(row =>
            {
                row.RelativeItem().AlignMiddle()
                    .Text("If your business has closed or you stopped paying wages . . . . . . . . . . . . . . . . . Check here, and enter the final date you paid wages")
                    .FontSize(7.5f);
                row.ConstantItem(AmountBoxWidth)
                    .Border(B).BorderColor("#000000")
                    .Padding(2).AlignMiddle()
                    .Text("__ / __ / ____").FontFamily(DataFont).FontSize(8);
            });

            // ── Part 4: May we speak with your third-party designee? ────
            main.Item().Element(c =>
                PartHeader(c, "Part 4: May we speak with your third-party designee?"));
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

            // ── Part 5: Sign here ────────────────────────────────────────
            main.Item().Element(c =>
                PartHeader(c, "Part 5: Sign here. You MUST complete both pages of Form 941 and SIGN it."));
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

    private string GetQuarterMonth12()
    {
        return _data.Quarter switch
        {
            1 => "March 12",
            2 => "June 12",
            3 => "September 12",
            4 => "December 12",
            _ => ""
        };
    }
}
