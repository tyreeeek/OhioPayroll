using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Data;
using OhioPayroll.App.Services;

namespace OhioPayroll.App.Documents;

/// <summary>
/// Generates contractor checks with MICR encoding for bank processing.
/// Follows standard business check format with proper MICR line placement.
/// </summary>
public class ContractorCheckDocument
{
    private readonly PayrollDbContext _db;
    private readonly IEncryptionService _encryption;

    public ContractorCheckDocument(PayrollDbContext db, IEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    /// <summary>
    /// Generates a contractor check PDF.
    /// </summary>
    public byte[] Generate(ContractorPayment payment, CheckRegisterEntry checkEntry)
    {
        // Load related data
        var contractor = _db.Contractors
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == payment.ContractorId);

        var company = _db.CompanyInfo
            .AsNoTracking()
            .FirstOrDefault() ?? new CompanyInfo();

        var bankAccount = _db.CompanyBankAccounts
            .AsNoTracking()
            .FirstOrDefault(b => b.IsDefaultForChecks) ?? new CompanyBankAccount();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0); // No margins for precise MICR placement

                page.Content().Column(column =>
                {
                    // Standard check is positioned at 3.5 inches from top
                    column.Item().Height(3.5f, Unit.Inch).Background(Colors.White);

                    // Check portion
                    column.Item().Height(3.5f, Unit.Inch).Element(c => ComposeCheck(c, payment, checkEntry, contractor, company, bankAccount));

                    // Stub portion (payment details)
                    column.Item().Height(3.5f, Unit.Inch).Element(c => ComposeStub(c, payment, contractor, company));
                });
            });
        }).GeneratePdf();
    }

    private void ComposeCheck(IContainer container, ContractorPayment payment, CheckRegisterEntry checkEntry,
        Contractor? contractor, CompanyInfo company, CompanyBankAccount bankAccount)
    {
        container.Border(1).BorderColor("#374151").Padding(16).Column(column =>
        {
            column.Spacing(12);

            // Check header - modern layout
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company.CompanyName ?? "Company Name").FontSize(16).Bold().FontColor("#111827");
                    col.Item().PaddingTop(2).Text(company.Address ?? "Address").FontSize(9).FontColor("#6B7280");
                    col.Item().Text($"{company.City ?? "City"}, {company.State ?? "ST"} {company.ZipCode ?? "00000"}").FontSize(9).FontColor("#6B7280");
                });

                row.ConstantItem(160).AlignRight().Column(col =>
                {
                    col.Item().Background("#1F2937").Padding(8).AlignCenter().Column(innerCol =>
                    {
                        innerCol.Item().Text($"#{checkEntry.CheckNumber}").FontSize(16).Bold().FontColor("#FFFFFF");
                        innerCol.Item().PaddingTop(2).Text($"{payment.PaymentDate:MM/dd/yyyy}").FontSize(9).FontColor("#D1D5DB");
                    });
                });
            });

            column.Item().PaddingTop(16);

            // Pay to the order of - modern design
            column.Item().Column(col =>
            {
                col.Item().Text("PAY TO THE ORDER OF").FontSize(8).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(4).Row(row =>
                {
                    var payeeName = payment.ContractorNameSnapshot ?? contractor?.Name ?? "Unknown";
                    row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(4)
                        .Text(payeeName).FontSize(13).Bold().FontColor("#111827");
                });
            });

            column.Item().PaddingTop(12);

            // Amount row
            column.Item().Row(row =>
            {
                row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(4)
                    .Text($"{AmountToWords(payment.Amount)} DOLLARS").FontSize(10).FontColor("#374151");
                row.ConstantItem(12);
                row.ConstantItem(140).Border(2).BorderColor("#111827").Padding(6).AlignCenter()
                    .Text($"${payment.Amount:N2}").FontSize(14).Bold().FontColor("#111827");
            });

            column.Item().PaddingTop(16);

            // Memo line
            column.Item().Column(col =>
            {
                col.Item().Text("MEMO").FontSize(7).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(2).BorderBottom(1).BorderColor("#D1D5DB").PaddingBottom(4)
                    .Text(payment.Description ?? "Contractor Payment").FontSize(9).FontColor("#374151");
            });

            // Signature line (push to bottom)
            column.Item().PaddingTop(20).AlignRight().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(220).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor("#111827");
                    col.Item().PaddingTop(3).AlignCenter()
                        .Text("Authorized Signature").FontSize(8).FontColor("#6B7280");
                });
            });

            column.Item().PaddingTop(15);

            // MICR line (positioned at bottom of check)
            // MICR format: ⑆CheckNumber⑆ ⑈RoutingNumber⑈ AccountNumber⑆
            // Decrypt banking data for MICR encoding
            string routingNumber = "000000000";
            string accountNumber = "000000000";

            try
            {
                if (!string.IsNullOrEmpty(bankAccount.EncryptedRoutingNumber))
                    routingNumber = _encryption.Decrypt(bankAccount.EncryptedRoutingNumber);

                if (!string.IsNullOrEmpty(bankAccount.EncryptedAccountNumber))
                    accountNumber = _encryption.Decrypt(bankAccount.EncryptedAccountNumber);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to decrypt banking data for check {checkEntry.CheckNumber}", ex);
                throw new InvalidOperationException($"Cannot generate check: Banking data unavailable. {ex.Message}", ex);
            }

            column.Item().PaddingTop(10).Text(text =>
            {
                text.Span("⑆").FontFamily("MICR").FontSize(12);
                text.Span($"{checkEntry.CheckNumber:D4}").FontFamily("MICR").FontSize(12);
                text.Span("⑆  ⑈").FontFamily("MICR").FontSize(12);
                text.Span(routingNumber).FontFamily("MICR").FontSize(12);
                text.Span("⑈  ").FontFamily("MICR").FontSize(12);
                text.Span(accountNumber).FontFamily("MICR").FontSize(12);
                text.Span("⑆").FontFamily("MICR").FontSize(12);
            });
        });
    }

    private void ComposeStub(IContainer container, ContractorPayment payment,
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

    /// <summary>
    /// Converts a decimal amount to words (e.g., 1234.56 -> "One thousand two hundred thirty-four and 56/100")
    /// </summary>
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
