using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OhioPayroll.App.Documents.IrsFormHelpers;

namespace OhioPayroll.App.Documents;

public class Form1096Data
{
    public string FilerName { get; set; } = string.Empty;
    public string FilerAddress { get; set; } = string.Empty;
    public string FilerCity { get; set; } = string.Empty;
    public string FilerState { get; set; } = string.Empty;
    public string FilerZip { get; set; } = string.Empty;
    public string FilerTin { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    public int Box3_TotalForms { get; set; }
    public decimal Box4_FederalTaxWithheld { get; set; }
    public decimal Box5_TotalAmount { get; set; }

    public int TaxYear { get; set; }

    /// <summary>
    /// The form type to check in the "Type of Return" section (e.g., "1099-NEC", "1099-MISC", "1099-INT").
    /// </summary>
    public string FormType { get; set; } = "1099-NEC";
}

public class Form1096Document : IDocument
{
    private readonly Form1096Data _data;

    private const float RowH = 24f;
    private const float B = BorderWidth;

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

            page.Content().Column(col =>
            {
                col.Item().Element(Compose1096Grid);
            });

            page.Footer().Element(c => PageFooter(c, "Form 1096", _data.TaxYear));
        });
    }

    private void Compose1096Grid(IContainer container)
    {
        container.Border(B).BorderColor("#000000").Column(grid =>
        {
            // ── Header: Form title
            grid.Item().Height(46)
                .Border(B).BorderColor("#000000").Padding(4)
                .Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Form ").FontSize(9);
                            text.Span("1096").FontSize(16).Bold();
                        });
                        row.ConstantItem(40).AlignRight()
                            .Text(_data.TaxYear.ToString()).FontSize(13).Bold();
                    });
                    col.Item().Text("Annual Summary and Transmittal of U.S. Information Returns")
                        .FontSize(7);
                    col.Item().Text("Department of the Treasury \u2014 Internal Revenue Service")
                        .FontSize(6).Italic();
                });

            // ── Filer's name
            grid.Item().Height(RowH).Element(c =>
                IrsBox(c, "FILER'S name", _data.FilerName));

            // ── Street address
            grid.Item().Height(RowH).Element(c =>
                IrsBox(c, "Street address (including room or suite number)", _data.FilerAddress));

            // ── City, state, ZIP
            grid.Item().Height(RowH).Element(c =>
                IrsBox(c, "City or town, state or province, country, and ZIP or foreign postal code",
                    $"{_data.FilerCity}, {_data.FilerState} {_data.FilerZip}"));

            // ── Contact name + Telephone
            grid.Item().Height(RowH).Row(row =>
            {
                row.RelativeItem().Element(c =>
                    IrsBox(c, "Name of person to contact", _data.ContactName ?? ""));
                row.RelativeItem().Element(c =>
                    IrsBox(c, "Telephone number", _data.ContactPhone ?? ""));
            });

            // ── Email + Fax
            grid.Item().Height(RowH).Row(row =>
            {
                row.RelativeItem().Element(c =>
                    IrsBox(c, "Email address", _data.ContactEmail ?? ""));
                row.RelativeItem().Element(c =>
                    IrsBox(c, "Fax number", ""));
            });

            // ── EIN
            grid.Item().Height(RowH).Row(row =>
            {
                row.RelativeItem().Element(c =>
                    IrsTinBox(c, "Employer identification number (EIN)", FormatTin(_data.FilerTin)));
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("Social security number").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).Text("").FontSize(ValueFontSize);
                    });
            });

            // ── Boxes 3, 4, 5
            grid.Item().Height(RowH + 4).Row(row =>
            {
                row.RelativeItem()
                    .Border(B).BorderColor("#000000").Padding(2)
                    .Column(col =>
                    {
                        col.Item().Text("3  Total number of forms").FontSize(LabelFontSize);
                        col.Item().PaddingTop(1).AlignRight()
                            .Text(_data.Box3_TotalForms.ToString())
                            .FontFamily(DataFont).FontSize(11).Bold();
                    });
                row.RelativeItem().Element(c =>
                    IrsAmountBox(c, "4  Federal income tax withheld",
                        FormatMoney(_data.Box4_FederalTaxWithheld)));
                row.RelativeItem().Element(c =>
                    IrsAmountBox(c, "5  Total amount reported with this Form 1096",
                        FormatMoney(_data.Box5_TotalAmount)));
            });

            // ── TYPE OF RETURN section
            grid.Item()
                .Border(B).BorderColor("#000000").Padding(4)
                .Column(typeCol =>
                {
                    typeCol.Item().Text("Check only one box below to indicate the type of form being filed.")
                        .FontSize(7).Bold();

                    typeCol.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        var formTypes = new[]
                        {
                            "1097-BTC", "1098", "1098-C", "1098-E", "1098-F",
                            "1098-Q", "1098-T", "1099-A", "1099-B", "1099-C",
                            "1099-CAP", "1099-DA", "1099-DIV", "1099-G", "1099-INT",
                            "1099-K", "1099-LS", "1099-LTC", "1099-MISC", "1099-NEC",
                            "1099-OID", "1099-PATR", "1099-Q", "1099-QA", "1099-R",
                            "1099-S", "1099-SA", "1099-SB", "5498", "5498-ESA",
                            "5498-QA", "5498-SA", "W-2G", "3921", "3922"
                        };

                        foreach (var formType in formTypes)
                        {
                            var isChecked = string.Equals(_data.FormType, formType,
                                StringComparison.OrdinalIgnoreCase);
                            table.Cell().Padding(1).Element(c =>
                                IrsCheckbox(c, formType, isChecked));
                        }
                    });
                });

            // ── For Official Use Only
            grid.Item().Height(RowH)
                .Border(B).BorderColor("#000000").Padding(3)
                .Column(col =>
                {
                    col.Item().Text("For Official Use Only").FontSize(LabelFontSize);
                });

            grid.Item().PaddingTop(3).PaddingBottom(2);
        });
    }
}
