using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.Documents;

/// <summary>
/// Generates contractor paystubs using snapshot data for historical accuracy.
/// No tax withholding - contractors receive gross payment amount.
/// Template matches employee paystub format exactly.
/// </summary>
public class ContractorPaystubDocument
{
    private readonly PayrollDbContext _db;

    public ContractorPaystubDocument(PayrollDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generates a contractor paystub PDF using immutable snapshot data.
    /// Includes both detailed paystub (page 1) and printable check (page 2 if payment method is Check).
    /// </summary>
    public byte[] Generate(ContractorPayment payment)
    {
        // Load related data
        var contractor = _db.Contractors
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == payment.ContractorId);

        var company = _db.CompanyInfo
            .AsNoTracking()
            .FirstOrDefault() ?? new CompanyInfo();

        var run = payment.ContractorPayrollRunId.HasValue
            ? _db.ContractorPayrollRuns
                .AsNoTracking()
                .FirstOrDefault(r => r.Id == payment.ContractorPayrollRunId.Value)
            : null;

        // Load check register entry if exists
        var checkEntry = !string.IsNullOrEmpty(payment.CheckNumber) && int.TryParse(payment.CheckNumber, out var checkNum)
            ? _db.CheckRegister
                .AsNoTracking()
                .FirstOrDefault(c => c.CheckNumber == checkNum)
            : null;

        var bankAccount = _db.CompanyBankAccounts
            .AsNoTracking()
            .FirstOrDefault(b => b.IsDefaultForChecks) ?? new CompanyBankAccount();

        // Calculate YTD total for current tax year (EXCLUDING current payment)
        var taxYearStart = new DateTime(payment.PaymentDate.Year, 1, 1);
        var ytdTotal = _db.ContractorPayments
            .AsNoTracking()
            .Where(p => p.ContractorId == payment.ContractorId &&
                       p.PaymentDate >= taxYearStart &&
                       p.PaymentDate < payment.PaymentDate &&  // EXCLUDE current payment
                       p.IsLocked)
            .Sum(p => (decimal?)p.Amount) ?? 0m;

        // Add current payment to show cumulative YTD including this payment
        ytdTotal += payment.Amount;

        return Document.Create(container =>
        {
            // Page 1: Detailed paystub (matching employee template)
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);

                page.Header().Element(c => ComposeHeader(c, company, payment));
                page.Content().Element(c => ComposeContent(c, payment, contractor, run, ytdTotal));
                page.Footer().Element(ComposeFooter);
            });

            // Page 2: Printable check (if payment method is check)
            if (payment.PaymentMethod == Core.Models.Enums.ContractorPaymentMethod.Check && checkEntry != null)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(0);

                    page.Content().Column(column =>
                    {
                        // Check portion (top) — 5 inches to fit all content
                        column.Item().Height(5f, Unit.Inch).Element(c => ComposeCheck(c, payment, checkEntry, contractor, company, bankAccount));

                        // Stub portion (bottom)
                        column.Item().Element(c => ComposeCheckStub(c, payment, contractor, company));
                    });
                });
            }
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container, CompanyInfo company, ContractorPayment payment)
    {
        container.Column(column =>
        {
            // Modern header with gradient effect simulation
            column.Item().Background("#1F2937").Padding(24).Row(row =>
            {
                // Company info
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company.CompanyName ?? "Company Name")
                        .FontSize(20).Bold().FontColor("#FFFFFF");
                    col.Item().PaddingTop(4).Text(company.Address ?? "Address")
                        .FontSize(10).FontColor("#D1D5DB");
                    col.Item().Text($"{company.City ?? "City"}, {company.State ?? "ST"} {company.ZipCode ?? "00000"}")
                        .FontSize(10).FontColor("#D1D5DB");
                    if (!string.IsNullOrEmpty(company.Phone))
                        col.Item().PaddingTop(2).Text($"Phone: {company.Phone}")
                            .FontSize(10).FontColor("#D1D5DB");
                });

                // Paystub title and date
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("CONTRACTOR PAYSTUB")
                        .FontSize(22).Bold().FontColor("#F59E0B");
                    col.Item().PaddingTop(6).Text($"Payment Date: {payment.PaymentDate:MM/dd/yyyy}")
                        .FontSize(11).FontColor("#E5E7EB");
                    if (!string.IsNullOrEmpty(payment.CheckNumber))
                        col.Item().PaddingTop(2).Text($"Check #: {payment.CheckNumber}")
                            .FontSize(11).FontColor("#E5E7EB");
                });
            });

            // Subtle accent bar (amber for contractor)
            column.Item().Height(3).Background("#F59E0B");
        });
    }

    private void ComposeContent(IContainer container, ContractorPayment payment,
        Contractor? contractor, ContractorPayrollRun? run, decimal ytdTotal)
    {
        container.Column(column =>
        {
            column.Spacing(15);

            // Contractor info section
            column.Item().Element(c => ComposeContractorInfo(c, payment, contractor));

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Payment period (if part of payroll run)
            if (run != null)
            {
                column.Item().Element(c => ComposePaymentPeriod(c, run));
                column.Item().LineHorizontal(1).LineColor("#E5E7EB");
            }

            // Earnings section (matching employee template)
            column.Item().Element(c => ComposeEarnings(c, payment, ytdTotal));

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Payment summary (matching employee template)
            column.Item().Element(c => ComposePaymentSummary(c, payment, ytdTotal));
        });
    }

    private void ComposeContractorInfo(IContainer container, ContractorPayment payment, Contractor? contractor)
    {
        container.Background("#F9FAFB").Padding(16).Column(column =>
        {
            column.Item().Text("Contractor Information").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    var contractorName = payment.ContractorNameSnapshot ?? contractor?.Name ?? "Unknown";
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(60).Text("Name:").FontSize(10).FontColor("#6B7280");
                        r.RelativeItem().Text(contractorName).FontSize(10).Bold().FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.ConstantItem(60).Text("Address:").FontSize(10).FontColor("#6B7280");
                        r.RelativeItem().Text($"{contractor?.Address ?? "N/A"}").FontSize(10).FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.ConstantItem(60).Text("City:").FontSize(10).FontColor("#6B7280");
                        r.RelativeItem().Text($"{contractor?.City ?? "N/A"}, {contractor?.State ?? "N/A"} {contractor?.ZipCode ?? "N/A"}").FontSize(10).FontColor("#111827");
                    });
                });

                row.ConstantItem(20);

                row.RelativeItem().Column(col =>
                {
                    if (contractor != null && !string.IsNullOrEmpty(contractor.TinLast4))
                    {
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(40).Text("TIN:").FontSize(10).FontColor("#6B7280");
                            r.RelativeItem().Text($"XXX-XX-{contractor.TinLast4}").FontSize(10).Bold().FontColor("#111827");
                        });
                    }
                });
            });
        });
    }

    private void ComposePaymentPeriod(IContainer container, ContractorPayrollRun run)
    {
        container.Column(column =>
        {
            column.Item().Text("Payment Period").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Period").FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text($"{run.PeriodStart:MM/dd/yyyy} - {run.PeriodEnd:MM/dd/yyyy}").FontSize(10).Bold().FontColor("#111827");
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Frequency").FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text($"{run.PayFrequency}").FontSize(10).Bold().FontColor("#111827");
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Pay Date").FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text($"{run.PayDate:MM/dd/yyyy}").FontSize(10).Bold().FontColor("#111827");
                });
            });
        });
    }

    private void ComposeEarnings(IContainer container, ContractorPayment payment, decimal ytdTotal)
    {
        container.Column(column =>
        {
            column.Item().Text("Earnings").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(8).Table(table =>
            {
                // Same column structure as employee template
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Description
                    columns.RelativeColumn(1); // Hours/Days
                    columns.RelativeColumn(1); // Rate
                    columns.RelativeColumn(1); // Current
                    columns.RelativeColumn(1); // YTD
                });

                // Header (matching employee template)
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Description").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Hours/Days").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Rate").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Current").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("YTD").FontSize(9).Bold().FontColor("#FFFFFF");
                });

                // Payment row
                var rateType = payment.RateTypeAtPayment ?? ContractorRateType.Flat;
                var rate = payment.RateAtPayment ?? 0;
                var units = rateType switch
                {
                    ContractorRateType.Hourly => payment.HoursWorked ?? 0,
                    ContractorRateType.Daily => payment.DaysWorked ?? 0,
                    _ => 1
                };

                var description = rateType switch
                {
                    ContractorRateType.Hourly => "Contractor Pay (Hourly)",
                    ContractorRateType.Daily => "Contractor Pay (Daily)",
                    ContractorRateType.Flat => "Contractor Pay (Flat Rate)",
                    _ => "Contractor Payment"
                };

                // Main payment row
                table.Cell().Element(CellStyle).Text(description);
                table.Cell().Element(CellStyle).AlignRight().Text(rateType == ContractorRateType.Flat ? "-" : $"{units:N2}");
                table.Cell().Element(CellStyle).AlignRight().Text(rateType == ContractorRateType.Flat ? "-" : $"{rate:C2}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{payment.Amount:C2}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{ytdTotal:C2}");

                // Total row (matching employee template)
                table.Cell().Element(CellStyle).Text("Gross Payment").Bold();
                table.Cell().Element(CellStyle).AlignRight().Text(rateType == ContractorRateType.Flat ? "-" : $"{units:N2}").Bold();
                table.Cell().Element(CellStyle).Text("");
                table.Cell().Element(CellStyle).AlignRight().Text($"{payment.Amount:C2}").Bold();
                table.Cell().Element(CellStyle).AlignRight().Text($"{ytdTotal:C2}").Bold();
            });
        });
    }

    private void ComposePaymentSummary(IContainer container, ContractorPayment payment, decimal ytdTotal)
    {
        container.Background("#F9FAFB").Padding(16).Column(column =>
        {
            column.Item().Text("Payment Summary").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(10).Row(row =>
            {
                // Current period - highlighted with amber border for contractor
                row.RelativeItem().Background("#FFFFFF").Border(2).BorderColor("#F59E0B").Padding(12).Column(col =>
                {
                    col.Item().Text("CURRENT PERIOD").FontSize(9).Bold().FontColor("#6B7280");
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("Gross Payment").FontSize(10).FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text($"{payment.Amount:C2}").FontSize(10).FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Deductions").FontSize(10).FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text("$0.00").FontSize(10).FontColor("#6B7280");
                    });
                    col.Item().PaddingTop(8).LineHorizontal(2).LineColor("#F59E0B");
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("NET PAYMENT").FontSize(12).Bold().FontColor("#F59E0B");
                        r.ConstantItem(100).AlignRight().Text($"{payment.Amount:C2}").FontSize(14).Bold().FontColor("#F59E0B");
                    });
                });

                // YTD
                row.ConstantItem(16);
                row.RelativeItem().Background("#FFFFFF").Border(1).BorderColor("#E5E7EB").Padding(12).Column(col =>
                {
                    col.Item().Text("YEAR-TO-DATE").FontSize(9).Bold().FontColor("#6B7280");
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("YTD Gross").FontSize(10).FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text($"{ytdTotal:C2}").FontSize(10).FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("YTD Deductions").FontSize(10).FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text("$0.00").FontSize(10).FontColor("#6B7280");
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E5E7EB");
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("YTD Net").FontSize(10).Bold().FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text($"{ytdTotal:C2}").FontSize(11).Bold().FontColor("#111827");
                    });
                });
            });

            // Contractor notice - modern design
            column.Item().PaddingTop(16).Border(1).BorderColor("#F59E0B").Background("#FFFBEB").Padding(14).Column(noticeCol =>
            {
                noticeCol.Item().Row(r =>
                {
                    r.ConstantItem(20).Text("⚠").FontSize(14).FontColor("#F59E0B");
                    r.RelativeItem().Text("IMPORTANT NOTICE - INDEPENDENT CONTRACTOR").FontSize(10).Bold().FontColor("#92400E");
                });
                noticeCol.Item().PaddingTop(6).Text(
                    "You are classified as an independent contractor (1099). No taxes have been withheld from this payment. " +
                    "You are responsible for all applicable federal, state, and local taxes including self-employment tax."
                ).FontSize(9).FontColor("#78350F");
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated by Ohio Payroll System - ").FontSize(8).FontColor("#9CA3AF");
            text.Span($"{DateTime.Now:MM/dd/yyyy HH:mm}").FontSize(8).Italic().FontColor("#9CA3AF");
        });
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor("#E5E7EB").PaddingVertical(6).PaddingHorizontal(4);
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.Background("#374151").PaddingVertical(8).PaddingHorizontal(4);
    }

    // Check generation methods
    private void ComposeCheck(IContainer container, ContractorPayment payment, CheckRegisterEntry checkEntry,
        Contractor? contractor, CompanyInfo company, CompanyBankAccount bankAccount)
    {
        // Note: For MICR encoding, this uses placeholder routing/account numbers.
        // Real MICR encoding requires IEncryptionService to decrypt banking data.
        // See ContractorCheckDocument.cs for production implementation.

        container.Border(1).BorderColor("#374151").Padding(12).Column(column =>
        {
            column.Spacing(6);

            // Check header
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company.CompanyName ?? "Company Name").FontSize(14).Bold().FontColor("#111827");
                    col.Item().PaddingTop(2).Text(company.Address ?? "Address").FontSize(9).FontColor("#6B7280");
                    col.Item().Text($"{company.City ?? "City"}, {company.State ?? "ST"} {company.ZipCode ?? "00000"}").FontSize(9).FontColor("#6B7280");
                });

                row.ConstantItem(140).AlignRight().Column(col =>
                {
                    col.Item().Background("#1F2937").Padding(6).AlignCenter().Column(innerCol =>
                    {
                        innerCol.Item().Text($"#{checkEntry.CheckNumber}").FontSize(14).Bold().FontColor("#FFFFFF");
                        innerCol.Item().PaddingTop(2).Text($"{payment.PaymentDate:MM/dd/yyyy}").FontSize(9).FontColor("#D1D5DB");
                    });
                });
            });

            column.Item().PaddingTop(8);

            // Pay to the order of
            column.Item().Column(col =>
            {
                col.Item().Text("PAY TO THE ORDER OF").FontSize(8).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(3).Row(row =>
                {
                    var payeeName = payment.ContractorNameSnapshot ?? contractor?.Name ?? "Unknown";
                    row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(3)
                        .Text(payeeName).FontSize(12).Bold().FontColor("#111827");
                });
            });

            column.Item().PaddingTop(6);

            // Amount row
            column.Item().Row(row =>
            {
                row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(3)
                    .Text($"{AmountToWords(payment.Amount)} DOLLARS").FontSize(10).FontColor("#374151");
                row.ConstantItem(10);
                row.ConstantItem(130).Border(2).BorderColor("#111827").Padding(5).AlignCenter()
                    .Text($"${payment.Amount:N2}").FontSize(13).Bold().FontColor("#111827");
            });

            column.Item().PaddingTop(8);

            // Memo line
            column.Item().Column(col =>
            {
                col.Item().Text("MEMO").FontSize(7).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(2).BorderBottom(1).BorderColor("#D1D5DB").PaddingBottom(3)
                    .Text(payment.Description ?? "Contractor Payment").FontSize(9).FontColor("#374151");
            });

            // Signature line
            column.Item().PaddingTop(12).AlignRight().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(200).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor("#111827");
                    col.Item().PaddingTop(2).AlignCenter()
                        .Text("Authorized Signature").FontSize(8).FontColor("#6B7280");
                });
            });

            // MICR line
            column.Item().PaddingTop(8).Text(text =>
            {
                text.Span("⑆").FontFamily("MICR").FontSize(12);
                text.Span($"{checkEntry.CheckNumber:D4}").FontFamily("MICR").FontSize(12);
                text.Span("⑆  ⑈").FontFamily("MICR").FontSize(12);
                text.Span("000000000").FontFamily("MICR").FontSize(12);
                text.Span("⑈  ").FontFamily("MICR").FontSize(12);
                text.Span("000000000").FontFamily("MICR").FontSize(12);
                text.Span("⑆").FontFamily("MICR").FontSize(12);
            });
        });
    }

    private void ComposeCheckStub(IContainer container, ContractorPayment payment,
        Contractor? contractor, CompanyInfo company)
    {
        container.BorderTop(2).BorderColor("#E5E7EB").Background("#F9FAFB")
            .PaddingHorizontal(40).PaddingVertical(20).Column(column =>
        {
            column.Spacing(10);

            // Stub header - modern
            column.Item().Text("PAYMENT STUB - CONTRACTOR").FontSize(10).Bold().FontColor("#374151");

            // Company and contractor info - better layout
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("FROM").FontSize(7).Bold().FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text(company.CompanyName ?? "Company Name").FontSize(9).Bold().FontColor("#111827");
                    col.Item().Text(company.Address ?? "").FontSize(8).FontColor("#374151");
                });

                row.ConstantItem(20);

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("TO").FontSize(7).Bold().FontColor("#6B7280");
                    var contractorName = payment.ContractorNameSnapshot ?? contractor?.Name ?? "Unknown";
                    col.Item().PaddingTop(2).Text(contractorName).FontSize(9).Bold().FontColor("#111827");
                    col.Item().Text(contractor?.Address ?? "").FontSize(8).FontColor("#374151");
                });
            });

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Payment details - modern grid
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(90).Text("Payment Date:").FontSize(8).FontColor("#6B7280");
                        r.RelativeItem().Text($"{payment.PaymentDate:MM/dd/yyyy}").FontSize(9).Bold().FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.ConstantItem(90).Text("Description:").FontSize(8).FontColor("#6B7280");
                        r.RelativeItem().Text($"{payment.Description ?? "Contractor Payment"}").FontSize(9).FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.ConstantItem(90).Text("Payment Method:").FontSize(8).FontColor("#6B7280");
                        r.RelativeItem().Text($"{payment.PaymentMethod}").FontSize(9).FontColor("#111827");
                    });
                });

                row.ConstantItem(20);

                row.ConstantItem(140).Border(2).BorderColor("#F59E0B").Background("#FFFFFF").Padding(10).Column(col =>
                {
                    col.Item().AlignCenter().Text("PAYMENT AMOUNT").FontSize(8).Bold().FontColor("#92400E");
                    col.Item().PaddingTop(4).AlignCenter().Text($"{payment.Amount:C2}").FontSize(16).Bold().FontColor("#F59E0B");
                    col.Item().PaddingTop(2).AlignCenter().Text("(No taxes withheld)").FontSize(7).Italic().FontColor("#78350F");
                });
            });

            column.Item().PaddingTop(8);

            // Important notice - modern warning box
            column.Item().Border(1).BorderColor("#F59E0B").Background("#FFFBEB").Padding(10).Row(row =>
            {
                row.ConstantItem(16).Text("⚠").FontSize(12).FontColor("#F59E0B");
                row.RelativeItem().Text(
                    "CONTRACTOR PAYMENT: You are classified as an independent contractor (1099). " +
                    "No taxes have been withheld. You are responsible for all applicable taxes."
                ).FontSize(8).FontColor("#78350F");
            });
        });
    }

    private string AmountToWords(decimal amount)
    {
        if (amount == 0) return "Zero";

        var dollars = (int)Math.Floor(amount);
        var cents = (int)Math.Round((amount - dollars) * 100);

        var words = NumberToWords(dollars);
        if (cents > 0)
            words += $" and {cents:D2}/100";
        else
            words += " and 00/100";

        return words;
    }

    private string NumberToWords(int number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        string words = "";

        if (number / 1000000 > 0)
        {
            words += NumberToWords(number / 1000000) + " million ";
            number %= 1000000;
        }

        if (number / 1000 > 0)
        {
            words += NumberToWords(number / 1000) + " thousand ";
            number %= 1000;
        }

        if (number / 100 > 0)
        {
            words += NumberToWords(number / 100) + " hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            if (words != "") words += "and ";

            string[] unitsMap = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
                "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
            string[] tensMap = { "zero", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += "-" + unitsMap[number % 10];
            }
        }

        return words.Trim();
    }
}
