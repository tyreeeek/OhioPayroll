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
                    // Top voucher stub (company copy)
                    column.Item().Height(3.5f, Unit.Inch).Element(c => ComposeStub(c, payment, contractor, company));

                    // Perforation line
                    column.Item().PaddingVertical(1).LineHorizontal(0.5f).LineColor("#AAAAAA");

                    // Check portion — compact layout to fit 3.5"
                    column.Item().Height(3.5f, Unit.Inch).Element(c => ComposeCheck(c, payment, checkEntry, contractor, company, bankAccount));

                    // Perforation line
                    column.Item().PaddingVertical(1).LineHorizontal(0.5f).LineColor("#AAAAAA");

                    // Bottom voucher stub (contractor copy)
                    column.Item().Element(c => ComposeStub(c, payment, contractor, company));
                });
            });
        }).GeneratePdf();
    }

    private void ComposeCheck(IContainer container, ContractorPayment payment, CheckRegisterEntry checkEntry,
        Contractor? contractor, CompanyInfo company, CompanyBankAccount bankAccount)
    {
        container.Border(1).BorderColor("#374151").Padding(10).Column(column =>
        {
            column.Spacing(4);

            // Check header
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company.CompanyName ?? "Company Name").FontSize(14).Bold().FontColor("#111827");
                    col.Item().PaddingTop(1).Text(company.Address ?? "Address").FontSize(8).FontColor("#6B7280");
                    col.Item().Text($"{company.City ?? "City"}, {company.State ?? "ST"} {company.ZipCode ?? "00000"}").FontSize(8).FontColor("#6B7280");
                });

                row.ConstantItem(140).AlignRight().Background("#1F2937").Padding(6).AlignCenter().Column(innerCol =>
                {
                    innerCol.Item().Text($"#{checkEntry.CheckNumber}").FontSize(14).Bold().FontColor("#FFFFFF");
                    innerCol.Item().PaddingTop(1).Text($"{payment.PaymentDate:MM/dd/yyyy}").FontSize(8).FontColor("#D1D5DB");
                });
            });

            column.Item().PaddingTop(8);

            // Pay to the order of
            column.Item().Column(col =>
            {
                col.Item().Text("PAY TO THE ORDER OF").FontSize(7).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(2).Row(row =>
                {
                    var payeeName = payment.ContractorNameSnapshot ?? contractor?.Name ?? "Unknown";
                    row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(2)
                        .Text(payeeName).FontSize(12).Bold().FontColor("#111827");
                });
            });

            column.Item().PaddingTop(6);

            // Amount row
            column.Item().Row(row =>
            {
                row.RelativeItem().BorderBottom(1).BorderColor("#111827").PaddingBottom(2)
                    .Text($"{AmountToWords(payment.Amount)} DOLLARS").FontSize(9).FontColor("#374151");
                row.ConstantItem(8);
                row.ConstantItem(130).Border(2).BorderColor("#111827").Padding(4).AlignCenter()
                    .Text($"${payment.Amount:N2}").FontSize(13).Bold().FontColor("#111827");
            });

            column.Item().PaddingTop(4);

            // Memo line
            column.Item().Column(col =>
            {
                col.Item().Text("MEMO").FontSize(7).Bold().FontColor("#6B7280");
                col.Item().PaddingTop(1).BorderBottom(1).BorderColor("#D1D5DB").PaddingBottom(2)
                    .Text(payment.Description ?? "Contractor Payment").FontSize(8).FontColor("#374151");
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

            // MICR line — decrypt banking data
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
                // Fall back to placeholder instead of crashing
                routingNumber = "000000000";
                accountNumber = "000000000";
            }

            column.Item().PaddingTop(6).Text(text =>
            {
                // Use Courier as fallback — MICR E-13B font may not be installed
                text.Span($"C{routingNumber}C  A{accountNumber}A  {checkEntry.CheckNumber:D6}")
                    .FontFamily("Courier").FontSize(10);
            });
        });
    }

    private void ComposeStub(IContainer container, ContractorPayment payment,
        Contractor? contractor, CompanyInfo company)
    {
        container.Background("#F9FAFB").Padding(12).Column(column =>
        {
            column.Spacing(6);

            // Stub header
            column.Item().Text("PAYMENT STUB - CONTRACTOR").FontSize(9).Bold().FontColor("#374151");

            // Company and contractor info
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("FROM").FontSize(7).Bold().FontColor("#6B7280");
                    col.Item().PaddingTop(1).Text(company.CompanyName ?? "Company Name").FontSize(8).Bold().FontColor("#111827");
                    col.Item().Text(company.Address ?? "").FontSize(7).FontColor("#374151");
                });

                row.ConstantItem(16);

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("TO").FontSize(7).Bold().FontColor("#6B7280");
                    var contractorName = payment.ContractorNameSnapshot ?? contractor?.Name ?? "Unknown";
                    col.Item().PaddingTop(1).Text(contractorName).FontSize(8).Bold().FontColor("#111827");
                    col.Item().Text(contractor?.Address ?? "").FontSize(7).FontColor("#374151");
                });
            });

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Payment details
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(80).Text("Payment Date:").FontSize(7).FontColor("#6B7280");
                        r.RelativeItem().Text($"{payment.PaymentDate:MM/dd/yyyy}").FontSize(8).Bold().FontColor("#111827");
                    });
                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.ConstantItem(80).Text("Description:").FontSize(7).FontColor("#6B7280");
                        r.RelativeItem().Text($"{payment.Description ?? "Contractor Payment"}").FontSize(8).FontColor("#111827");
                    });
                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.ConstantItem(80).Text("Method:").FontSize(7).FontColor("#6B7280");
                        r.RelativeItem().Text($"{payment.PaymentMethod}").FontSize(8).FontColor("#111827");
                    });
                });

                row.ConstantItem(16);

                row.ConstantItem(120).Border(2).BorderColor("#F59E0B").Background("#FFFFFF").Padding(8).Column(col =>
                {
                    col.Item().AlignCenter().Text("PAYMENT AMOUNT").FontSize(7).Bold().FontColor("#92400E");
                    col.Item().PaddingTop(2).AlignCenter().Text($"{payment.Amount:C2}").FontSize(14).Bold().FontColor("#F59E0B");
                    col.Item().PaddingTop(1).AlignCenter().Text("(No taxes withheld)").FontSize(6).Italic().FontColor("#78350F");
                });
            });

            // Contractor notice
            column.Item().Border(1).BorderColor("#F59E0B").Background("#FFFBEB").Padding(6).Text(
                "1099 CONTRACTOR: No taxes withheld. You are responsible for all applicable taxes."
            ).FontSize(7).FontColor("#78350F");
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
