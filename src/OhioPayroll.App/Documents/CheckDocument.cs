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

    // Color palette
    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string LightBg = "#F5F7FA";
    private static readonly string BorderColor = "#CBD5E1";
    private static readonly string TableHeaderBg = "#2C5F8A";
    private static readonly string TableHeaderText = "#FFFFFF";

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
                // Top third: Employee voucher stub
                column.Item().Height(SectionHeight, Unit.Inch).Element(c => ComposeVoucherStub(c, "EMPLOYEE COPY"));

                // Perforation line
                column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor("#AAAAAA");

                // Middle third: The actual check
                column.Item().Height(SectionHeight, Unit.Inch).Element(ComposeCheckSection);

                // Perforation line
                column.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor("#AAAAAA");

                // Bottom third: Company voucher stub
                column.Item().Height(SectionHeight, Unit.Inch).Element(c => ComposeVoucherStub(c, "COMPANY COPY"));
            });
        });
    }

    private void ComposeVoucherStub(IContainer container, string copyLabel)
    {
        container.Border(0.5f).BorderColor(BorderColor).Padding(6).Column(column =>
        {
            // Voucher header
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_company.CompanyName).FontSize(9).Bold();
                    col.Item().Text($"EIN: {FormatEin(_company.Ein)}").FontSize(7).FontColor("#64748B");
                });
                row.ConstantItem(120).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text(copyLabel).FontSize(7).Bold().FontColor("#64748B");
                    if (_paycheck.CheckNumber.HasValue)
                        col.Item().AlignRight().Text($"Check #: {_paycheck.CheckNumber}").FontSize(7);
                    col.Item().AlignRight().Text($"Pay Date: {_run.PayDate:MM/dd/yyyy}").FontSize(7);
                });
            });

            column.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text($"Employee: {_employee.FullName}").FontSize(8).Bold();
                row.ConstantItem(180).AlignRight()
                    .Text($"Period: {_run.PeriodStart:MM/dd/yyyy} - {_run.PeriodEnd:MM/dd/yyyy}").FontSize(7);
            });

            column.Item().PaddingTop(4);

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
                        header.Cell().Background(TableHeaderBg).Padding(3)
                            .Text("Earnings").FontSize(7).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(3).AlignRight()
                            .Text("Current").FontSize(7).Bold().FontColor(TableHeaderText);
                    });

                    VoucherRow(table, "Regular Pay", _paycheck.RegularPay, false);
                    VoucherRow(table, "Overtime Pay", _paycheck.OvertimePay, true);
                    VoucherTotalRow(table, "Gross Pay", _paycheck.GrossPay);
                });

                row.ConstantItem(8);

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
                        header.Cell().Background(TableHeaderBg).Padding(3)
                            .Text("Deductions").FontSize(7).Bold().FontColor(TableHeaderText);
                        header.Cell().Background(TableHeaderBg).Padding(3).AlignRight()
                            .Text("Current").FontSize(7).Bold().FontColor(TableHeaderText);
                    });

                    VoucherRow(table, "Federal W/H", _paycheck.FederalWithholding, false);
                    VoucherRow(table, "Ohio State", _paycheck.OhioStateWithholding, true);
                    VoucherRow(table, "School Dist.", _paycheck.SchoolDistrictTax, false);
                    VoucherRow(table, "Local/Muni", _paycheck.LocalMunicipalityTax, true);
                    VoucherRow(table, "Soc. Security", _paycheck.SocialSecurityTax, false);
                    VoucherRow(table, "Medicare", _paycheck.MedicareTax, true);
                    VoucherTotalRow(table, "Total Deduct.", _paycheck.TotalDeductions);
                });

                row.ConstantItem(8);

                // Net Pay box
                row.ConstantItem(100).AlignMiddle().Border(1).BorderColor(HeaderBg).Column(col =>
                {
                    col.Item().Background(HeaderBg).Padding(4).AlignCenter()
                        .Text("NET PAY").FontSize(8).Bold().FontColor(HeaderText);
                    col.Item().Padding(6).AlignCenter()
                        .Text($"{_paycheck.NetPay:C}").FontSize(12).Bold();
                });
            });
        });
    }

    private static void VoucherRow(TableDescriptor table, string description, decimal amount, bool alt)
    {
        var bg = alt ? LightBg : "#FFFFFF";
        table.Cell().Background(bg).Padding(2).Text(description).FontSize(7);
        table.Cell().Background(bg).Padding(2).AlignRight().Text($"{amount:C}").FontSize(7);
    }

    private static void VoucherTotalRow(TableDescriptor table, string description, decimal amount)
    {
        table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(2)
            .Text(description).FontSize(7).Bold();
        table.Cell().BorderTop(1).BorderColor(BorderColor).Padding(2).AlignRight()
            .Text($"{amount:C}").FontSize(7).Bold();
    }

    private void ComposeCheckSection(IContainer container)
    {
        container.Border(0.5f).BorderColor(BorderColor).Padding(8).Column(column =>
        {
            // Check header: Company info + check number/date
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_company.CompanyName).FontSize(11).Bold();
                    col.Item().Text(_company.Address).FontSize(8);
                    col.Item().Text($"{_company.City}, {_company.State} {_company.ZipCode}").FontSize(8);
                });

                row.ConstantItem(160).AlignRight().Column(col =>
                {
                    if (_paycheck.CheckNumber.HasValue)
                        col.Item().AlignRight().Text($"{_paycheck.CheckNumber}")
                            .FontSize(11).Bold();
                    col.Item().PaddingTop(4).AlignRight()
                        .Text($"Date: {_run.PayDate:MM/dd/yyyy}").FontSize(9);
                });
            });

            column.Item().PaddingTop(12);

            // Pay To The Order Of
            column.Item().Row(row =>
            {
                row.ConstantItem(120).Text("PAY TO THE\nORDER OF").FontSize(8).Bold();
                row.RelativeItem().BorderBottom(1).BorderColor("#000000").PaddingBottom(2)
                    .Text(_employee.FullName).FontSize(11).Bold();
                row.ConstantItem(10);
                row.ConstantItem(120).Border(1).BorderColor("#000000").Padding(4).AlignCenter()
                    .Text($"**{_paycheck.NetPay:C}**").FontSize(10).Bold();
            });

            column.Item().PaddingTop(8);

            // Amount in words
            column.Item().Row(row =>
            {
                row.RelativeItem().BorderBottom(1).BorderColor("#000000").PaddingBottom(2)
                    .Text($"{AmountToWords(_paycheck.NetPay)} DOLLARS")
                    .FontSize(9);
                row.ConstantItem(10);
                row.ConstantItem(120);
            });

            column.Item().PaddingTop(8);

            // Memo line
            column.Item().Row(row =>
            {
                row.ConstantItem(40).Text("Memo:").FontSize(8);
                row.RelativeItem().BorderBottom(1).BorderColor(BorderColor).PaddingBottom(2)
                    .Text($"Payroll {_run.PeriodStart:MM/dd/yyyy} - {_run.PeriodEnd:MM/dd/yyyy}")
                    .FontSize(8).FontColor("#64748B");
            });

            // Signature line (push to bottom)
            column.Item().PaddingTop(16).AlignRight().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(200).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor("#000000");
                    col.Item().PaddingTop(2).AlignCenter()
                        .Text("Authorized Signature").FontSize(7).FontColor("#64748B");
                });
            });

            // MICR line
            // NOTE: The current implementation uses Courier as a placeholder font for the
            // MICR encoding line. True E-13B MICR compliance requires a special E-13B font
            // (e.g., "MICR E13B" or similar). For production check printing, an E-13B MICR
            // font must be installed on the system and referenced here instead of Courier.
            // Banks may reject checks without proper E-13B encoding on the MICR line.
            column.Item().PaddingTop(8).Text(ComposeMicrLine())
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

