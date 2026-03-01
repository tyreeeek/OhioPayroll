using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OhioPayroll.Core.Models;

namespace OhioPayroll.App.Documents;

public class CheckDocument : IDocument
{
    private readonly CompanyInfo _company;
    private readonly Employee _employee;
    private readonly Paycheck _paycheck;
    private readonly PayrollRun _run;
    private readonly string _bankRoutingNumber;
    private readonly string _bankAccountNumber;
    private readonly decimal _offsetX;
    private readonly decimal _offsetY;

    // Layout constants — letter paper divided into thirds
    private const float PageWidth = 8.5f;    // inches
    private const float PageHeight = 11f;     // inches
    private const float SectionHeight = 3.5f; // inches (approximately 11/3)
    private const float Margin = 0.25f;       // inches

    // Modern color palette
    private static readonly string HeaderBg = "#1F2937";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string LightBg = "#F9FAFB";
    private static readonly string BorderColor = "#E5E7EB";
    private static readonly string TableHeaderBg = "#374151";
    private static readonly string TableHeaderText = "#FFFFFF";
    private static readonly string AccentGreen = "#10B981";

    public CheckDocument(
        CompanyInfo company,
        Employee employee,
        Paycheck paycheck,
        PayrollRun run,
        string bankRoutingNumber,
        string bankAccountNumber,
        decimal offsetX = 0,
        decimal offsetY = 0)
    {
        _company = company;
        _employee = employee;
        _paycheck = paycheck;
        _run = run;
        _bankRoutingNumber = bankRoutingNumber;
        _bankAccountNumber = bankAccountNumber;
        _offsetX = offsetX;
        _offsetY = offsetY;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.MarginLeft((Margin + (float)_offsetX), Unit.Inch);
            page.MarginRight(Margin, Unit.Inch);
            page.MarginTop((Margin + (float)_offsetY), Unit.Inch);
            page.MarginBottom(Margin, Unit.Inch);
            page.DefaultTextStyle(x => x.FontSize(8));

            page.Content().Column(column =>
            {
                // Top third: Employee voucher stub — auto-sized
                column.Item().Element(c => ComposeVoucherStub(c, "EMPLOYEE COPY"));

                // Perforation line
                column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor("#AAAAAA");

                // Middle third: The actual check — auto-sized
                column.Item().Element(ComposeCheckSection);

                // Perforation line
                column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor("#AAAAAA");

                // Bottom third: Company voucher stub
                column.Item().Element(c => ComposeVoucherStub(c, "COMPANY COPY"));
            });
        });
    }

    private void ComposeVoucherStub(IContainer container, string copyLabel)
    {
        container.Background(LightBg).Border(1).BorderColor(BorderColor).Padding(6).Column(column =>
        {
            column.Spacing(3);

            // Voucher header
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_company.CompanyName).FontSize(9).Bold().FontColor("#111827");
                    col.Item().Text($"EIN: {FormatEin(_company.Ein)}").FontSize(7).FontColor("#6B7280");
                });
                row.ConstantItem(120).AlignRight().Background("#FFFFFF").Border(1).BorderColor(AccentGreen).Padding(3).Column(col =>
                {
                    col.Item().AlignRight().Text(copyLabel).FontSize(6).Bold().FontColor("#6B7280");
                    if (_paycheck.CheckNumber.HasValue)
                        col.Item().AlignRight().Text($"Check #: {_paycheck.CheckNumber}").FontSize(7).Bold().FontColor("#111827");
                    col.Item().AlignRight().Text($"Pay Date: {_run.PayDate:MM/dd/yyyy}").FontSize(7).FontColor("#374151");
                });
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("EMPLOYEE").FontSize(6).FontColor("#6B7280");
                    col.Item().Text(_employee.FullName).FontSize(8).Bold().FontColor("#111827");
                });
                row.ConstantItem(180).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text("PERIOD").FontSize(6).FontColor("#6B7280");
                    col.Item().AlignRight().Text($"{_run.PeriodStart:MM/dd/yyyy} - {_run.PeriodEnd:MM/dd/yyyy}").FontSize(7).Bold().FontColor("#111827");
                });
            });

            // Earnings / Deductions side by side
            column.Item().Row(row =>
            {
                // Earnings mini-table
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(TableHeaderBg).Padding(2)
                            .Text("Earnings").FontSize(6).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(2).AlignRight()
                            .Text("Current").FontSize(6).Bold().FontColor(TableHeaderText);
                    });

                    VoucherRow(table, "Regular Pay", _paycheck.RegularPay, false);
                    VoucherRow(table, "Overtime Pay", _paycheck.OvertimePay, true);
                    VoucherTotalRow(table, "Gross Pay", _paycheck.GrossPay);
                });

                row.ConstantItem(6);

                // Deductions mini-table
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(TableHeaderBg).Padding(2)
                            .Text("Deductions").FontSize(6).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(2).AlignRight()
                            .Text("Current").FontSize(6).Bold().FontColor(TableHeaderText);
                    });

                    VoucherRow(table, "Federal W/H", _paycheck.FederalWithholding, false);
                    VoucherRow(table, "Ohio State", _paycheck.OhioStateWithholding, true);
                    VoucherRow(table, "School Dist.", _paycheck.SchoolDistrictTax, false);
                    VoucherRow(table, "Local/Muni", _paycheck.LocalMunicipalityTax, true);
                    VoucherRow(table, "Soc. Security", _paycheck.SocialSecurityTax, false);
                    VoucherRow(table, "Medicare", _paycheck.MedicareTax, true);
                    VoucherTotalRow(table, "Total Deduct.", _paycheck.TotalDeductions);
                });

                row.ConstantItem(6);

                // Net Pay box
                row.ConstantItem(100).AlignMiddle().Border(2).BorderColor(AccentGreen).Background("#FFFFFF").Column(col =>
                {
                    col.Item().Background(AccentGreen).Padding(4).AlignCenter()
                        .Text("NET PAY").FontSize(7).Bold().FontColor(HeaderText);
                    col.Item().Padding(6).AlignCenter()
                        .Text($"{_paycheck.NetPay:C}").FontSize(12).Bold().FontColor(AccentGreen);
                });
            });
        });
    }

    private static void VoucherRow(TableDescriptor table, string description, decimal amount, bool alt)
    {
        var bg = alt ? LightBg : "#FFFFFF";
        table.Cell().Background(bg).Padding(1).Text(description).FontSize(6);
        table.Cell().Background(bg).Padding(1).AlignRight().Text($"{amount:C}").FontSize(6);
    }

    private static void VoucherTotalRow(TableDescriptor table, string description, decimal amount)
    {
        table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(1)
            .Text(description).FontSize(6).Bold();
        table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(1).AlignRight()
            .Text($"{amount:C}").FontSize(6).Bold();
    }

    private void ComposeCheckSection(IContainer container)
    {
        container.Border(1).BorderColor("#374151").Padding(10).Column(column =>
        {
            column.Spacing(4);

            // Check header
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_company.CompanyName).FontSize(12).Bold().FontColor("#111827");
                    col.Item().PaddingTop(1).Text(_company.Address).FontSize(8).FontColor("#6B7280");
                    col.Item().Text($"{_company.City}, {_company.State} {_company.ZipCode}").FontSize(8).FontColor("#6B7280");
                });

                row.ConstantItem(140).AlignRight().Background(HeaderBg).Padding(6).Column(col =>
                {
                    if (_paycheck.CheckNumber.HasValue)
                        col.Item().AlignCenter().Text($"#{_paycheck.CheckNumber}")
                            .FontSize(14).Bold().FontColor("#FFFFFF");
                    col.Item().PaddingTop(1).AlignCenter()
                        .Text($"{_run.PayDate:MM/dd/yyyy}").FontSize(8).FontColor("#D1D5DB");
                });
            });

            column.Item().PaddingTop(8);

            // Pay To The Order Of
            column.Item().Column(col =>
            {
                col.Item().Text("PAY TO THE ORDER OF").FontSize(7).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(2)
                        .Text(_employee.FullName).FontSize(12).Bold().FontColor("#111827");
                });
            });

            column.Item().PaddingTop(6);

            // Amount row
            column.Item().Row(row =>
            {
                row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(2)
                    .Text($"{AmountToWords(_paycheck.NetPay)} DOLLARS")
                    .FontSize(9).FontColor("#374151");
                row.ConstantItem(8);
                row.ConstantItem(130).Border(2).BorderColor("#111827").Padding(4).AlignCenter()
                    .Text($"${_paycheck.NetPay:C}").FontSize(13).Bold().FontColor("#111827");
            });

            column.Item().PaddingTop(4);

            // Memo line
            column.Item().Column(col =>
            {
                col.Item().Text("MEMO").FontSize(7).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(1).BorderBottom(1).BorderColor("#D1D5DB").PaddingBottom(2)
                    .Text($"Payroll {_run.PeriodStart:MM/dd/yyyy} - {_run.PeriodEnd:MM/dd/yyyy}")
                    .FontSize(8).FontColor("#374151");
            });

            // Signature line
            column.Item().PaddingTop(10).AlignRight().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(200).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor("#111827");
                    col.Item().PaddingTop(2).AlignCenter()
                        .Text("Authorized Signature").FontSize(7).FontColor("#6B7280");
                });
            });

            // MICR line (Courier placeholder — install E-13B font for production)
            column.Item().PaddingTop(6).Text(ComposeMicrLine())
                .FontSize(10).FontFamily("Courier");
        });
    }

    private string ComposeMicrLine()
    {
        string checkNum = _paycheck.CheckNumber?.ToString("D6") ?? "000000";
        // MICR format: C{routing}C {account}A {check_number}
        // Using standard MICR symbols: C = transit symbol, A = on-us symbol
        return $"C{_bankRoutingNumber}C  A{_bankAccountNumber}A  {checkNum}";
    }

    private void ComposeFooter(IContainer container)
    {
        // Footer is not used since content is self-contained in the three sections
    }

    /// <summary>
    /// Converts a decimal amount to words for check writing.
    /// Example: 1234.56 becomes "One Thousand Two Hundred Thirty-Four and 56/100"
    /// </summary>
    public static string AmountToWords(decimal amount)
    {
        if (amount < 0)
            return "Negative " + AmountToWords(Math.Abs(amount));

        long dollars = (long)Math.Truncate(amount);
        int cents = (int)((amount - dollars) * 100);

        string dollarWords = dollars == 0 ? "Zero" : NumberToWords(dollars);
        return $"{dollarWords} and {cents:D2}/100";
    }

    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Negative " + NumberToWords(Math.Abs(number));

        string[] ones =
        {
            "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen",
            "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        };

        string[] tens =
        {
            "", "", "Twenty", "Thirty", "Forty", "Fifty",
            "Sixty", "Seventy", "Eighty", "Ninety"
        };

        var parts = new List<string>();

        if (number >= 1_000_000_000)
        {
            parts.Add(NumberToWords(number / 1_000_000_000) + " Billion");
            number %= 1_000_000_000;
        }

        if (number >= 1_000_000)
        {
            parts.Add(NumberToWords(number / 1_000_000) + " Million");
            number %= 1_000_000;
        }

        if (number >= 1_000)
        {
            parts.Add(NumberToWords(number / 1_000) + " Thousand");
            number %= 1_000;
        }

        if (number >= 100)
        {
            parts.Add(ones[number / 100] + " Hundred");
            number %= 100;
        }

        if (number >= 20)
        {
            string tenPart = tens[number / 10];
            long remainder = number % 10;
            if (remainder > 0)
                parts.Add(tenPart + "-" + ones[remainder]);
            else
                parts.Add(tenPart);
        }
        else if (number > 0)
        {
            parts.Add(ones[number]);
        }

        return string.Join(" ", parts);
    }

    private static string FormatEin(string ein)
    {
        if (ein.Length == 9)
            return $"{ein[..2]}-{ein[2..]}";
        return ein;
    }
}

