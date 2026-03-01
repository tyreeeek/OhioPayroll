using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OhioPayroll.App.Documents.IrsFormHelpers;

namespace OhioPayroll.App.Documents;

public class W2Data
{
    // Employer
    public string EmployerEin { get; set; } = string.Empty;
    public string StateWithholdingId { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string EmployerAddress { get; set; } = string.Empty;
    public string EmployerCity { get; set; } = string.Empty;
    public string EmployerState { get; set; } = string.Empty;
    public string EmployerZip { get; set; } = string.Empty;

    // Employee
    public string EmployeeSsn { get; set; } = string.Empty;
    public string EmployeeFirstName { get; set; } = string.Empty;
    public string EmployeeLastName { get; set; } = string.Empty;
    public string EmployeeAddress { get; set; } = string.Empty;
    public string EmployeeCity { get; set; } = string.Empty;
    public string EmployeeState { get; set; } = string.Empty;
    public string EmployeeZip { get; set; } = string.Empty;

    // W-2 Boxes
    public decimal Box1WagesTips { get; set; }
    public decimal Box2FederalTaxWithheld { get; set; }
    public decimal Box3SocialSecurityWages { get; set; }
    public decimal Box4SocialSecurityTax { get; set; }
    public decimal Box5MedicareWages { get; set; }
    public decimal Box6MedicareTax { get; set; }
    public decimal Box16StateWages { get; set; }
    public decimal Box17StateTax { get; set; }
    public decimal Box18LocalWages { get; set; }
    public decimal Box19LocalTax { get; set; }
    public string Box20LocalityName { get; set; } = string.Empty;

    public int TaxYear { get; set; }
}

public class W2Document : IDocument
{
    private readonly W2Data _data;

    private const float B = BorderWidth;

    public W2Document(W2Data data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        // Copy B — Employee Federal
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(0.5f, Unit.Inch);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Content().Column(col =>
            {
                col.Item().Element(c => ComposeW2Grid(c,
                    "Copy B \u2014 To Be Filed With Employee's FEDERAL Tax Return"));
            });

            page.Footer().Element(c => PageFooter(c, "Form W-2", _data.TaxYear));
        });

        // Copy C — Employee Records
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(0.5f, Unit.Inch);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Content().Column(col =>
            {
                col.Item().Element(c => ComposeW2Grid(c,
                    "Copy C \u2014 For EMPLOYEE'S RECORDS"));
            });

            page.Footer().Element(c => PageFooter(c, "Form W-2", _data.TaxYear));
        });
    }

    private void ComposeW2Grid(IContainer container, string copyDesignation)
    {
        container.Column(grid =>
        {
            // ── Row 1: SSN (left) | Form Title (right)
            grid.Item().Row(row =>
            {
                row.RelativeItem(7).Element(c =>
                    IrsTinBox(c, "a  Employee's social security number",
                        FormatSsnMasked(_data.EmployeeSsn)));
                row.RelativeItem(8)
                    .Border(B).BorderColor("#000000")
                    .Padding(3)
                    .Column(titleCol =>
                    {
                        titleCol.Item().Row(titleRow =>
                        {
                            titleRow.RelativeItem().Text(text =>
                            {
                                text.Span("Form ").FontSize(7);
                                text.Span("W-2").FontSize(14).Bold();
                            });
                            titleRow.ConstantItem(35).AlignRight()
                                .Text(_data.TaxYear.ToString()).FontSize(11).Bold();
                        });
                        titleCol.Item().Text("Wage and Tax Statement").FontSize(6);
                    });
            });

            // ── Row 2: EIN (left) | Box 1 (mid) | Box 2 (right)
            grid.Item().Row(row =>
            {
                row.RelativeItem(7).Element(c =>
                    IrsTinBox(c, "b  Employer identification number (EIN)",
                        FormatTin(_data.EmployerEin)));
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "1  Wages, tips, other compensation",
                        FormatMoney(_data.Box1WagesTips)));
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "2  Federal income tax withheld",
                        FormatMoney(_data.Box2FederalTaxWithheld)));
            });

            // ── Row 3: Employer name/addr (left, tall) | Box 3/4, 5/6
            grid.Item().Row(row =>
            {
                row.RelativeItem(7)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("c  Employer's name, address, and ZIP code")
                            .FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.EmployerName)
                            .FontFamily(DataFont).FontSize(9).Bold().ClampLines(1);
                        col.Item().Text(_data.EmployerAddress)
                            .FontFamily(DataFont).FontSize(7).ClampLines(1);
                        col.Item().Text($"{_data.EmployerCity}, {_data.EmployerState} {_data.EmployerZip}")
                            .FontFamily(DataFont).FontSize(7).ClampLines(1);
                    });

                row.RelativeItem(8).Column(numCol =>
                {
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "3  Social security wages",
                                FormatMoney(_data.Box3SocialSecurityWages)));
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "4  Social security tax withheld",
                                FormatMoney(_data.Box4SocialSecurityTax)));
                    });
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "5  Medicare wages and tips",
                                FormatMoney(_data.Box5MedicareWages)));
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "6  Medicare tax withheld",
                                FormatMoney(_data.Box6MedicareTax)));
                    });
                });
            });

            // ── Row 4: Control number (left) | Box 7 | Box 8
            grid.Item().Row(row =>
            {
                row.RelativeItem(7).Element(c =>
                    IrsBox(c, "d  Control number", ""));
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "7  Social security tips", ""));
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "8  Allocated tips", ""));
            });

            // ── Row 5: Employee name (left, tall) | Box 9/11 | Box 10/12a
            grid.Item().Row(row =>
            {
                row.RelativeItem(7)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("e  Employee's first name and initial    Last name    Suff.")
                            .FontSize(LabelFontSize);
                        col.Item().PaddingTop(2)
                            .Text($"{_data.EmployeeFirstName} {_data.EmployeeLastName}")
                            .FontFamily(DataFont).FontSize(ValueFontSize).Bold().ClampLines(1);
                    });

                row.RelativeItem(8).Column(numCol =>
                {
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsBox(c, "9", ""));
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "10  Dependent care benefits", ""));
                    });
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "11  Nonqualified plans", ""));
                        numRow.RelativeItem().Element(c =>
                            IrsBox(c, "12a  See instructions for box 12", ""));
                    });
                });
            });

            // ── Row 6: Employee addr (left, tall) | Box 12b-c | Box 13-14
            grid.Item().Row(row =>
            {
                row.RelativeItem(7)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("f  Employee's address and ZIP code")
                            .FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.EmployeeAddress)
                            .FontFamily(DataFont).FontSize(7).ClampLines(1);
                        col.Item().Text($"{_data.EmployeeCity}, {_data.EmployeeState} {_data.EmployeeZip}")
                            .FontFamily(DataFont).FontSize(7).ClampLines(1);
                    });

                row.RelativeItem(8).Column(numCol =>
                {
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsBox(c, "12b", ""));
                        numRow.RelativeItem()
                            .Border(B).BorderColor("#000000").Padding(2)
                            .Column(chkCol =>
                            {
                                chkCol.Item().Text("13").FontSize(LabelFontSize);
                                chkCol.Item().PaddingTop(1).Row(chkRow =>
                                {
                                    chkRow.RelativeItem().Element(c =>
                                        IrsCheckbox(c, "Statutory employee", false));
                                });
                                chkCol.Item().Row(chkRow =>
                                {
                                    chkRow.RelativeItem().Element(c =>
                                        IrsCheckbox(c, "Retirement plan", false));
                                });
                                chkCol.Item().Row(chkRow =>
                                {
                                    chkRow.RelativeItem().Element(c =>
                                        IrsCheckbox(c, "Third-party sick pay", false));
                                });
                            });
                    });
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsBox(c, "12c", ""));
                        numRow.RelativeItem().Element(c =>
                            IrsBox(c, "14  Other", ""));
                    });
                });
            });

            // ── Row 7: State/Local (Boxes 15-20)
            grid.Item().Row(row =>
            {
                row.ConstantItem(36)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("15  State").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.EmployerState)
                            .FontFamily(DataFont).FontSize(9).Bold();
                    });
                row.ConstantItem(90)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Employer's state ID number").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.StateWithholdingId)
                            .FontFamily(DataFont).FontSize(7);
                    });
                row.ConstantItem(80)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("16  State wages, tips, etc.").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.Box16StateWages)).FontFamily(DataFont).FontSize(7);
                    });
                row.ConstantItem(80)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("17  State income tax").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.Box17StateTax)).FontFamily(DataFont).FontSize(7);
                    });
                row.ConstantItem(75)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("18  Local wages, tips, etc.").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.Box18LocalWages)).FontFamily(DataFont).FontSize(7);
                    });
                row.ConstantItem(65)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("19  Local income tax").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.Box19LocalTax)).FontFamily(DataFont).FontSize(7);
                    });
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("20  Locality name").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.Box20LocalityName)
                            .FontFamily(DataFont).FontSize(7);
                    });
            });

            // ── Copy designation
            grid.Item().PaddingTop(3).PaddingBottom(2).PaddingLeft(4)
                .Text(copyDesignation).FontSize(6).Bold();
        });
    }
}
