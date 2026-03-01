using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OhioPayroll.Core.Models;

namespace OhioPayroll.App.Documents;

public class PaystubDocument : IDocument
{
    private readonly CompanyInfo _company;
    private readonly Employee _employee;
    private readonly Paycheck _paycheck;
    private readonly PayrollRun _run;

    public PaystubDocument(CompanyInfo company, Employee employee, Paycheck paycheck, PayrollRun run)
    {
        _company = company;
        _employee = employee;
        _paycheck = paycheck;
        _run = run;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(40);

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            // Modern header with gradient effect simulation
            column.Item().Background("#1F2937").Padding(24).Row(row =>
            {
                // Company info
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_company.CompanyName ?? "Company Name")
                        .FontSize(20).Bold().FontColor("#FFFFFF");
                    col.Item().PaddingTop(4).Text(_company.Address ?? "Address")
                        .FontSize(10).FontColor("#D1D5DB");
                    col.Item().Text($"{_company.City ?? "City"}, {_company.State ?? "ST"} {_company.ZipCode ?? "00000"}")
                        .FontSize(10).FontColor("#D1D5DB");
                    if (!string.IsNullOrEmpty(_company.Phone))
                        col.Item().PaddingTop(2).Text($"Phone: {_company.Phone}")
                            .FontSize(10).FontColor("#D1D5DB");
                });

                // Paystub title and date
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("EMPLOYEE PAYSTUB")
                        .FontSize(22).Bold().FontColor("#10B981");
                    col.Item().PaddingTop(6).Text($"Payment Date: {_run.PayDate:MM/dd/yyyy}")
                        .FontSize(11).FontColor("#E5E7EB");
                    if (_paycheck.CheckNumber.HasValue)
                        col.Item().PaddingTop(2).Text($"Check #: {_paycheck.CheckNumber}")
                            .FontSize(11).FontColor("#E5E7EB");
                });
            });

            // Subtle accent bar
            column.Item().Height(3).Background("#10B981");
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(15);

            // Employee info section
            column.Item().Element(ComposeEmployeeInfo);

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Payment period
            column.Item().Element(ComposePaymentPeriod);

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Earnings section
            column.Item().Element(ComposeEarnings);

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Deductions section
            column.Item().Element(ComposeDeductions);

            column.Item().LineHorizontal(1).LineColor("#E5E7EB");

            // Payment summary
            column.Item().Element(ComposePaymentSummary);
        });
    }

    private void ComposeEmployeeInfo(IContainer container)
    {
        container.Background("#F9FAFB").Padding(16).Column(column =>
        {
            column.Item().Text("Employee Information").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(60).Text("Name:").FontSize(10).FontColor("#6B7280");
                        r.RelativeItem().Text(_employee.FullName).FontSize(10).Bold().FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.ConstantItem(60).Text("Address:").FontSize(10).FontColor("#6B7280");
                        r.RelativeItem().Text($"{_employee.Address ?? "N/A"}").FontSize(10).FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.ConstantItem(60).Text("City:").FontSize(10).FontColor("#6B7280");
                        r.RelativeItem().Text($"{_employee.City ?? "N/A"}, {_employee.State ?? "N/A"} {_employee.ZipCode ?? "N/A"}").FontSize(10).FontColor("#111827");
                    });
                });

                row.ConstantItem(20);

                row.RelativeItem().Column(col =>
                {
                    if (!string.IsNullOrEmpty(_employee.SsnLast4))
                    {
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(40).Text("SSN:").FontSize(10).FontColor("#6B7280");
                            r.RelativeItem().Text($"XXX-XX-{_employee.SsnLast4}").FontSize(10).Bold().FontColor("#111827");
                        });
                    }
                });
            });
        });
    }

    private void ComposePaymentPeriod(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Payment Period").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Period").FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text($"{_run.PeriodStart:MM/dd/yyyy} - {_run.PeriodEnd:MM/dd/yyyy}").FontSize(10).Bold().FontColor("#111827");
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Frequency").FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text($"{_run.PayFrequency}").FontSize(10).Bold().FontColor("#111827");
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Pay Date").FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text($"{_run.PayDate:MM/dd/yyyy}").FontSize(10).Bold().FontColor("#111827");
                });
            });
        });
    }

    private void ComposeEarnings(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Earnings").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Description
                    columns.RelativeColumn(1); // Hours
                    columns.RelativeColumn(1); // Rate
                    columns.RelativeColumn(1); // Current
                    columns.RelativeColumn(1); // YTD
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Description").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Hours").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Rate").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Current").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("YTD").FontSize(9).Bold().FontColor("#FFFFFF");
                });

                // Regular Pay
                if (_paycheck.RegularHours > 0)
                {
                    var regularRate = _paycheck.RegularPay / _paycheck.RegularHours;
                    table.Cell().Element(CellStyle).Text("Regular Pay");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.RegularHours:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{regularRate:C2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.RegularPay:C2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdGrossPay:C2}");
                }

                // Overtime Pay
                if (_paycheck.OvertimeHours > 0)
                {
                    var overtimeRate = _paycheck.OvertimePay / _paycheck.OvertimeHours;
                    table.Cell().Element(CellStyle).Text("Overtime Pay");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.OvertimeHours:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{overtimeRate:C2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.OvertimePay:C2}");
                    table.Cell().Element(CellStyle).AlignRight().Text("");
                }

                // Total Gross
                table.Cell().Element(CellStyle).Text("Gross Pay").Bold();
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.RegularHours + _paycheck.OvertimeHours:N2}").Bold();
                table.Cell().Element(CellStyle).Text("");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.GrossPay:C2}").Bold();
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdGrossPay:C2}").Bold();
            });
        });
    }

    private void ComposeDeductions(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Deductions").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Description
                    columns.RelativeColumn(1); // Current
                    columns.RelativeColumn(1); // YTD
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Description").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Current").FontSize(9).Bold().FontColor("#FFFFFF");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("YTD").FontSize(9).Bold().FontColor("#FFFFFF");
                });

                // Federal Withholding
                table.Cell().Element(CellStyle).Text("Federal Withholding");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.FederalWithholding:C2}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdFederalWithholding:C2}");

                // State Withholding
                table.Cell().Element(CellStyle).Text("Ohio State Withholding");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.OhioStateWithholding:C2}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdOhioStateWithholding:C2}");

                // School District Tax
                if (_paycheck.SchoolDistrictTax > 0)
                {
                    table.Cell().Element(CellStyle).Text("School District Tax");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.SchoolDistrictTax:C2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdSchoolDistrictTax:C2}");
                }

                // Local Tax
                if (_paycheck.LocalMunicipalityTax > 0)
                {
                    table.Cell().Element(CellStyle).Text("Local/Municipality Tax");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.LocalMunicipalityTax:C2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdLocalTax:C2}");
                }

                // Social Security
                table.Cell().Element(CellStyle).Text("Social Security (Employee)");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.SocialSecurityTax:C2}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdSocialSecurity:C2}");

                // Medicare
                table.Cell().Element(CellStyle).Text("Medicare (Employee)");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.MedicareTax:C2}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.YtdMedicare:C2}");

                // Total Deductions
                table.Cell().Element(CellStyle).Text("Total Deductions").Bold();
                table.Cell().Element(CellStyle).AlignRight().Text($"{_paycheck.TotalDeductions:C2}").Bold();
                table.Cell().Element(CellStyle).AlignRight().Text($"{TotalYtdDeductions():C2}").Bold();
            });
        });
    }

    private void ComposePaymentSummary(IContainer container)
    {
        container.Background("#F9FAFB").Padding(16).Column(column =>
        {
            column.Item().Text("Payment Summary").FontSize(13).Bold().FontColor("#374151");
            column.Item().PaddingTop(10).Row(row =>
            {
                // Current period - highlighted
                row.RelativeItem().Background("#FFFFFF").Border(2).BorderColor("#10B981").Padding(12).Column(col =>
                {
                    col.Item().Text("CURRENT PERIOD").FontSize(9).Bold().FontColor("#6B7280");
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("Gross Pay").FontSize(10).FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text($"{_paycheck.GrossPay:C2}").FontSize(10).FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Deductions").FontSize(10).FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text($"{_paycheck.TotalDeductions:C2}").FontSize(10).FontColor("#EF4444");
                    });
                    col.Item().PaddingTop(8).LineHorizontal(2).LineColor("#10B981");
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("NET PAY").FontSize(12).Bold().FontColor("#10B981");
                        r.ConstantItem(100).AlignRight().Text($"{_paycheck.NetPay:C2}").FontSize(14).Bold().FontColor("#10B981");
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
                        r.ConstantItem(100).AlignRight().Text($"{_paycheck.YtdGrossPay:C2}").FontSize(10).FontColor("#111827");
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("YTD Deductions").FontSize(10).FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text($"{TotalYtdDeductions():C2}").FontSize(10).FontColor("#EF4444");
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E5E7EB");
                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("YTD Net").FontSize(10).Bold().FontColor("#374151");
                        r.ConstantItem(100).AlignRight().Text($"{_paycheck.YtdNetPay:C2}").FontSize(11).Bold().FontColor("#111827");
                    });
                });
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

    private decimal TotalYtdDeductions()
    {
        return _paycheck.YtdFederalWithholding
            + _paycheck.YtdOhioStateWithholding
            + _paycheck.YtdSchoolDistrictTax
            + _paycheck.YtdLocalTax
            + _paycheck.YtdSocialSecurity
            + _paycheck.YtdMedicare;
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor("#E5E7EB").PaddingVertical(6).PaddingHorizontal(4);
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.Background("#374151").PaddingVertical(8).PaddingHorizontal(4);
    }

    private static string FormatEin(string ein)
    {
        if (ein.Length == 9)
            return $"{ein[..2]}-{ein[2..]}";
        return ein;
    }
}

