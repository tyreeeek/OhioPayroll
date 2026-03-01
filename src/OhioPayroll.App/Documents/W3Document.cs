using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OhioPayroll.App.Documents.IrsFormHelpers;

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

    private const float B = BorderWidth;

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

            page.Content().Column(col =>
            {
                col.Item().Element(ComposeW3Grid);
            });

            page.Footer().Element(c => PageFooter(c, "Form W-3", _data.TaxYear));
        });
    }

    private void ComposeW3Grid(IContainer container)
    {
        container.Column(grid =>
        {
            // ── Row 1: Control number (left) | Form Title (right)
            grid.Item().Row(row =>
            {
                row.RelativeItem(7)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("a  Control number").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("33333")
                            .FontFamily(DataFont).FontSize(ValueFontSize).Bold();
                    });
                row.RelativeItem(8)
                    .Border(B).BorderColor("#000000").Padding(3)
                    .Column(titleCol =>
                    {
                        titleCol.Item().Row(titleRow =>
                        {
                            titleRow.RelativeItem().Text(text =>
                            {
                                text.Span("Form ").FontSize(7);
                                text.Span("W-3").FontSize(14).Bold();
                            });
                            titleRow.ConstantItem(35).AlignRight()
                                .Text(_data.TaxYear.ToString()).FontSize(11).Bold();
                        });
                        titleCol.Item().Text("Transmittal of Wage and Tax Statements").FontSize(6);
                    });
            });

            // ── Row 2: Kind of Payer/Employer (left, triple) | Box 1-6
            grid.Item().Row(row =>
            {
                row.RelativeItem(7)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("b  Kind of Payer").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Row(chkRow =>
                        {
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "941", true));
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "Military", false));
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "943", false));
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "944", false));
                        });
                        col.Item().PaddingTop(1).Row(chkRow =>
                        {
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "CT-1", false));
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "Hshld emp.", false));
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "Medicare govt.", false));
                        });
                        col.Item().PaddingTop(4).Text("Kind of Employer").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Row(chkRow =>
                        {
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "None apply", true));
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "State/local", false));
                        });
                        col.Item().PaddingTop(1).Row(chkRow =>
                        {
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "Tax-exempt", false));
                            chkRow.RelativeItem().Element(c => IrsCheckbox(c, "Federal govt.", false));
                        });
                    });

                row.RelativeItem(8).Column(numCol =>
                {
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "1  Wages, tips, other compensation",
                                FormatMoney(_data.TotalWages)));
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "2  Federal income tax withheld",
                                FormatMoney(_data.TotalFederalTax)));
                    });
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "3  Social security wages",
                                FormatMoney(_data.TotalSsWages)));
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "4  Social security tax withheld",
                                FormatMoney(_data.TotalSsTax)));
                    });
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "5  Medicare wages and tips",
                                FormatMoney(_data.TotalMedicareWages)));
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "6  Medicare tax withheld",
                                FormatMoney(_data.TotalMedicareTax)));
                    });
                });
            });

            // ── Row 3: c/d + Box 7/8
            grid.Item().Row(row =>
            {
                row.RelativeItem(7).Row(innerRow =>
                {
                    innerRow.RelativeItem()
                        .Border(B).BorderColor("#000000").Padding(2)
                        .Column(col =>
                        {
                            col.Item().Text("c  Total number of Forms W-2").FontSize(LabelFontSize);
                            col.Item().PaddingTop(1).Text(_data.NumberOfW2s.ToString())
                                .FontFamily(DataFont).FontSize(11).Bold();
                        });
                    innerRow.RelativeItem()
                        .Border(B).BorderColor("#000000").Padding(2)
                        .Column(col =>
                        {
                            col.Item().Text("d  Establishment number").FontSize(LabelFontSize);
                            col.Item().PaddingTop(1).Text("").FontSize(ValueFontSize);
                        });
                });
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "7  Social security tips", ""));
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "8  Allocated tips", ""));
            });

            // ── Row 4: e EIN (left) | Box 9/10
            grid.Item().Row(row =>
            {
                row.RelativeItem(7).Element(c =>
                    IrsTinBox(c, "e  Employer identification number (EIN)",
                        FormatTin(_data.EmployerEin)));
                row.RelativeItem(4).Element(c =>
                    IrsBox(c, "9", ""));
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "10  Dependent care benefits", ""));
            });

            // ── Row 5: f Employer name (left) | Box 11/12a
            grid.Item().Row(row =>
            {
                row.RelativeItem(7)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("f  Employer's name").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.EmployerName)
                            .FontFamily(DataFont).FontSize(ValueFontSize).Bold().ClampLines(1);
                    });
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "11  Nonqualified plans", ""));
                row.RelativeItem(4).Element(c =>
                    IrsAmountBox(c, "12a  Deferred compensation", ""));
            });

            // ── Row 6: g Address (left, tall) | Box 12b-13, 14
            grid.Item().Row(row =>
            {
                row.RelativeItem(7)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("g  Employer's address and ZIP code").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text(_data.EmployerAddress)
                            .FontFamily(DataFont).FontSize(7).ClampLines(1);
                        col.Item().Text($"{_data.EmployerCity}, {_data.EmployerState} {_data.EmployerZip}")
                            .FontFamily(DataFont).FontSize(7).ClampLines(1);
                        col.Item().PaddingTop(4).Text("h  Other EIN used this year").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(ValueFontSize);
                    });

                row.RelativeItem(8).Column(numCol =>
                {
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsBox(c, "12b", ""));
                        numRow.RelativeItem().Element(c =>
                            IrsBox(c, "13  For third-party sick pay use only", ""));
                    });
                    numCol.Item().Row(numRow =>
                    {
                        numRow.RelativeItem().Element(c =>
                            IrsAmountBox(c, "14  Income tax withheld by payer of third-party sick pay", ""));
                        numRow.RelativeItem()
                            .Border(B).BorderColor("#000000");
                    });
                });
            });

            // ── Row 7: State/Local (Boxes 15-19)
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
                row.ConstantItem(90)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("16  State wages, tips, etc.").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.TotalStateWages)).FontFamily(DataFont).FontSize(7);
                    });
                row.ConstantItem(90)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("17  State income tax").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.TotalStateTax)).FontFamily(DataFont).FontSize(7);
                    });
                row.ConstantItem(80)
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("18  Local wages, tips, etc.").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.TotalLocalWages)).FontFamily(DataFont).FontSize(7);
                    });
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("19  Local income tax").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(FormatMoney(_data.TotalLocalTax)).FontFamily(DataFont).FontSize(7);
                    });
            });

            // ── Contact info row
            grid.Item().Row(row =>
            {
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Employer's contact person").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(7);
                    });
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Employer's telephone number").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(7);
                    });
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Employer's email address").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(7);
                    });
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Employer's fax number").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(7);
                    });
            });

            // ── Transmittal label
            grid.Item().PaddingTop(3).PaddingBottom(2).PaddingLeft(4)
                .Text("Transmittal of Wage and Tax Statements").FontSize(6).Bold();
        });
    }
}
