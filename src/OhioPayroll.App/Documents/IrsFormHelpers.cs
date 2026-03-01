using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents;

/// <summary>
/// Shared constants and helpers for IRS-style form rendering.
/// Pure formatting functions + deterministic layout primitives.
/// No data access, no conditional business logic.
/// </summary>
public static class IrsFormHelpers
{
    // ── Constants ──────────────────────────────────────────────────────
    public const float LabelFontSize = 5.5f;
    public const float ValueFontSize = 10f;
    public const float FormNumberFontSize = 16f;
    public const float BorderWidth = 0.75f;
    public const string DataFont = "Courier New";

    // W-2 / W-3 shared column widths (points; 72pt = 1 inch)
    public const float LeftColumnWidth = 252f;   // ~3.5"
    public const float MidColumnWidth = 144f;    // ~2.0"
    public const float RightColumnWidth = 144f;  // ~2.0"
    public const float BoxRowHeight = 27f;       // 3/8" — IRS standard box height
    public const float TallBoxRowHeight = 50f;   // double-height box (employer addr, etc.)

    // 940/941 line-item widths
    public const float LineNumWidth = 28f;
    public const float AmountBoxWidth = 110f;

    // Section header background
    public const string SectionBg = "#E8E8E8";

    // ── Pure Formatting Functions ──────────────────────────────────────

    /// <summary>Format decimal as IRS amount: commas, 2 decimals, no $ symbol.</summary>
    public static string FormatMoney(decimal value)
    {
        return value.ToString("#,##0.00");
    }

    /// <summary>Format a raw digit string as EIN (XX-XXXXXXX) or SSN (XXX-XX-XXXX).</summary>
    public static string FormatTin(string raw, bool isSsn = false)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (isSsn && digits.Length == 9)
            return $"{digits[..3]}-{digits[3..5]}-{digits[5..]}";
        if (!isSsn && digits.Length == 9)
            return $"{digits[..2]}-{digits[2..]}";
        return raw; // return as-is if not 9 digits
    }

    /// <summary>Mask SSN for employee copies: ***-**-1234.</summary>
    public static string FormatSsnMasked(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length >= 4)
            return $"***-**-{digits[^4..]}";
        return raw;
    }

    // ── Layout Primitives ──────────────────────────────────────────────

    /// <summary>
    /// Standard IRS box: thin black border, tiny label top-left, value below in Courier.
    /// </summary>
    public static void IrsBox(IContainer container, string label, string value)
    {
        container
            .Border(BorderWidth)
            .BorderColor("#000000")
            .Padding(2)
            .Column(col =>
            {
                col.Item().Text(label).FontSize(LabelFontSize);
                col.Item().PaddingTop(1).Text(value)
                    .FontFamily(DataFont).FontSize(ValueFontSize);
            });
    }

    /// <summary>
    /// IRS amount box: label top-left, value right-aligned below in Courier.
    /// </summary>
    public static void IrsAmountBox(IContainer container, string label, string value)
    {
        container
            .Border(BorderWidth)
            .BorderColor("#000000")
            .Padding(2)
            .Column(col =>
            {
                col.Item().Text(label).FontSize(LabelFontSize);
                col.Item().PaddingTop(1).AlignRight().Text(value)
                    .FontFamily(DataFont).FontSize(ValueFontSize);
            });
    }

    /// <summary>
    /// IRS box with only a label and a large bold Courier value (for TINs, SSNs).
    /// </summary>
    public static void IrsTinBox(IContainer container, string label, string value)
    {
        container
            .Border(BorderWidth)
            .BorderColor("#000000")
            .Padding(2)
            .Column(col =>
            {
                col.Item().Text(label).FontSize(LabelFontSize);
                col.Item().PaddingTop(1).Text(value)
                    .FontFamily(DataFont).FontSize(ValueFontSize).Bold();
            });
    }

    /// <summary>
    /// Checkbox rendering: filled square or empty square + label.
    /// </summary>
    public static void IrsCheckbox(IContainer container, string label, bool isChecked)
    {
        container.Row(row =>
        {
            row.ConstantItem(9).AlignMiddle().Text(isChecked ? "\u25A0" : "\u25A1").FontSize(8);
            row.RelativeItem().PaddingLeft(1).AlignMiddle().Text(label).FontSize(LabelFontSize);
        });
    }

    /// <summary>
    /// Form title block: large form number + subtitle + year.
    /// </summary>
    public static void FormTitle(IContainer container, string formNumber, int year, string subtitle)
    {
        container
            .Border(BorderWidth)
            .BorderColor("#000000")
            .Padding(3)
            .Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(titleCol =>
                    {
                        titleCol.Item().Text($"Form {formNumber}").FontSize(FormNumberFontSize).Bold();
                        titleCol.Item().Text(subtitle).FontSize(6);
                    });
                    row.ConstantItem(40).AlignRight().AlignMiddle()
                        .Text(year.ToString()).FontSize(14).Bold();
                });
            });
    }

    /// <summary>
    /// Copy designation label.
    /// </summary>
    public static void CopyLabel(IContainer container, string copyText)
    {
        container.PaddingTop(2).Text(copyText).FontSize(6).Bold();
    }

    /// <summary>
    /// Page footer: form name, year, and generation date.
    /// </summary>
    public static void PageFooter(IContainer container, string formName, int year)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(BorderWidth).LineColor("#000000");
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"{formName}  {year}")
                    .FontSize(7).FontColor("#666666");
                row.RelativeItem().AlignRight()
                    .Text($"Generated {DateTime.Now:MM/dd/yyyy}")
                    .FontSize(7).FontColor("#666666");
            });
        });
    }

    // ── Shared Layout Fragments ────────────────────────────────────────

    /// <summary>
    /// A single numeric box row for the W-2/W-3 right-side grid.
    /// Two equal-width amount boxes side by side.
    /// </summary>
    public static void NumericBoxRow(IContainer container,
        string leftLabel, string leftValue,
        string rightLabel, string rightValue)
    {
        container.Height(BoxRowHeight).Row(row =>
        {
            row.RelativeItem().Element(c => IrsAmountBox(c, leftLabel, leftValue));
            row.RelativeItem().Element(c => IrsAmountBox(c, rightLabel, rightValue));
        });
    }

    /// <summary>
    /// Line-item row for Form 940 / Form 941.
    /// Line number (28pt) + description (relative) + bordered amount box (110pt).
    /// </summary>
    public static void LineItemRow(IContainer container,
        string lineNum, string description, string amount, bool hasBorder = true)
    {
        container.MinHeight(16).Row(row =>
        {
            row.ConstantItem(LineNumWidth).AlignMiddle().PaddingLeft(4)
                .Text(lineNum).FontSize(8).Bold();
            row.RelativeItem().AlignMiddle().PaddingLeft(2)
                .Text(description).FontSize(7.5f);
            if (hasBorder)
            {
                row.ConstantItem(AmountBoxWidth)
                    .Border(BorderWidth).BorderColor("#000000")
                    .Padding(2).AlignRight().AlignMiddle()
                    .Text(amount).FontFamily(DataFont).FontSize(8);
            }
            else
            {
                row.ConstantItem(AmountBoxWidth)
                    .Padding(2).AlignRight().AlignMiddle()
                    .Text(amount).FontFamily(DataFont).FontSize(8);
            }
        });
    }

    /// <summary>
    /// Line-item row with rate column for Form 941 lines 5a-5d.
    /// Line number + description + wages box + "× rate =" + tax box.
    /// </summary>
    public static void LineItemRowWithRate(IContainer container,
        string lineNum, string description, string wages, string rate, string tax)
    {
        container.MinHeight(16).Row(row =>
        {
            row.ConstantItem(LineNumWidth).AlignMiddle().PaddingLeft(4)
                .Text(lineNum).FontSize(8).Bold();
            row.RelativeItem().AlignMiddle().PaddingLeft(2)
                .Text(description).FontSize(7.5f);
            row.ConstantItem(80)
                .Border(BorderWidth).BorderColor("#000000")
                .Padding(2).AlignRight().AlignMiddle()
                .Text(wages).FontFamily(DataFont).FontSize(8);
            row.ConstantItem(45).AlignCenter().AlignMiddle()
                .Text($"\u00D7 {rate} =").FontSize(6.5f);
            row.ConstantItem(80)
                .Border(BorderWidth).BorderColor("#000000")
                .Padding(2).AlignRight().AlignMiddle()
                .Text(tax).FontFamily(DataFont).FontSize(8);
        });
    }

    /// <summary>
    /// Bold part header with gray background shading for Form 940/941.
    /// </summary>
    public static void PartHeader(IContainer container, string partTitle)
    {
        container
            .PaddingTop(4)
            .Background(SectionBg)
            .BorderBottom(1).BorderColor("#000000")
            .PaddingLeft(4).PaddingVertical(2)
            .Text(partTitle).FontSize(8).Bold();
    }

    /// <summary>
    /// IRS-style form header block for 940/941 forms.
    /// "Form XXX for YYYY: Title" + "Department of the Treasury..." line.
    /// </summary>
    public static void IrsFormHeader(IContainer container,
        string formNumber, int year, string title)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(titleCol =>
                {
                    titleCol.Item().Text(text =>
                    {
                        text.Span("Form ").FontSize(10);
                        text.Span(formNumber).FontSize(14).Bold();
                        text.Span($" for ").FontSize(10);
                        text.Span(year.ToString()).FontSize(14).Bold();
                        text.Span(": ").FontSize(10);
                        text.Span(title).FontSize(8);
                    });
                    titleCol.Item().PaddingTop(1)
                        .Text("Department of the Treasury \u2014 Internal Revenue Service")
                        .FontSize(6.5f).Italic();
                });
            });
            col.Item().PaddingTop(3).LineHorizontal(1).LineColor("#000000");
        });
    }

    /// <summary>
    /// Employer info block with bordered fields for 940/941 forms.
    /// </summary>
    public static void EmployerInfoBlock(IContainer container,
        string ein, string name, string? address = null,
        string? city = null, string? state = null, string? zip = null)
    {
        container.PaddingTop(2).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c =>
                    IrsTinBox(c, "Employer identification number (EIN)", FormatTin(ein)));
            });
            col.Item().PaddingTop(1).Element(c =>
                IrsBox(c, "Name (not your trade name)", name));
            if (address != null)
            {
                col.Item().PaddingTop(1).Element(c =>
                    IrsBox(c, "Address", address));
            }
            if (city != null || state != null || zip != null)
            {
                col.Item().PaddingTop(1).Row(row =>
                {
                    row.RelativeItem(3).Element(c =>
                        IrsBox(c, "City", city ?? ""));
                    row.RelativeItem(1).Element(c =>
                        IrsBox(c, "State", state ?? ""));
                    row.RelativeItem(2).Element(c =>
                        IrsBox(c, "ZIP code", zip ?? ""));
                });
            }
        });
    }
}
