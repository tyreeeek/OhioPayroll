using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents;

public class W2Data
{
    // Employer
    public string EmployerEin { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string EmployerAddress { get; set; } = string.Empty;
    public string EmployerCity { get; set; } = string.Empty;
    public string EmployerState { get; set; } = string.Empty;
    public string EmployerZip { get; set; } = string.Empty;

    // Employee
    public string EmployeeSsn { get; set; } = string.Empty; // Last 4 only for display
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

    // Color palette matching project style
    private static readonly string HeaderBg = "#1B3A5C";
    private static readonly string HeaderText = "#FFFFFF";
    private static readonly string LabelColor = "#64748B";
    private static readonly string BorderColor = "#000000";
    private static readonly string LightBorderColor = "#CBD5E1";
    private static readonly string BoxBg = "#FFFFFF";

    public W2Document(W2Data data)
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

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Background(HeaderBg).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"W-2 Wage and Tax Statement  {_data.TaxYear}")
                        .FontSize(16).Bold().FontColor(HeaderText);
                    col.Item().Text("Copy B - To Be Filed With Employee's FEDERAL Tax Return")
                        .FontSize(8).FontColor(HeaderText);
                });
                row.ConstantItem(150).AlignRight().AlignMiddle()
                    .Text("Department of the Treasury - IRS")
                    .FontSize(8).FontColor(HeaderText);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(8).Column(column =>
        {
            // Row 1: Employer ID (a) + Employee SSN (d)
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("a  Employee's social security number")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text($"XXX-XX-{_data.EmployeeSsn}")
                        .FontSize(11).Bold();
                });
                row.ConstantItem(4);
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("b  Employer identification number (EIN)")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text(FormatEin(_data.EmployerEin))
                        .FontSize(11).Bold();
                });
            });

            column.Item().Height(4);

            // Row 2: Employer info (c) + Boxes 1 & 2
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                {
                    col.Item().Padding(3).Text("c  Employer's name, address, and ZIP code")
                        .FontSize(6).FontColor(LabelColor);
                    col.Item().PaddingLeft(5).Text(_data.EmployerName)
                        .FontSize(9).Bold();
                    col.Item().PaddingLeft(5).Text(_data.EmployerAddress)
                        .FontSize(8);
                    col.Item().PaddingLeft(5).PaddingBottom(4)
                        .Text($"{_data.EmployerCity}, {_data.EmployerState} {_data.EmployerZip}")
                        .FontSize(8);
                });
                row.ConstantItem(4);
                row.ConstantItem(240).Column(rightCol =>
                {
                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeBox(boxRow.RelativeItem(), "1  Wages, tips, other compensation",
                            _data.Box1WagesTips);
                        boxRow.ConstantItem(4);
                        ComposeBox(boxRow.RelativeItem(), "2  Federal income tax withheld",
                            _data.Box2FederalTaxWithheld);
                    });

                    rightCol.Item().Height(4);

                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeBox(boxRow.RelativeItem(), "3  Social security wages",
                            _data.Box3SocialSecurityWages);
                        boxRow.ConstantItem(4);
                        ComposeBox(boxRow.RelativeItem(), "4  Social security tax withheld",
                            _data.Box4SocialSecurityTax);
                    });

                    rightCol.Item().Height(4);

                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeBox(boxRow.RelativeItem(), "5  Medicare wages and tips",
                            _data.Box5MedicareWages);
                        boxRow.ConstantItem(4);
                        ComposeBox(boxRow.RelativeItem(), "6  Medicare tax withheld",
                            _data.Box6MedicareTax);
                    });
                });
            });

            column.Item().Height(4);

            // Row 3: Employee info (e, f) + Boxes 7-10
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(empCol =>
                {
                    empCol.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Padding(3).Text("e  Employee's first name and initial    Last name")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text($"{_data.EmployeeFirstName} {_data.EmployeeLastName}")
                            .FontSize(10).Bold();
                    });

                    empCol.Item().Height(4);

                    empCol.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Padding(3).Text("f  Employee's address and ZIP code")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).Text(_data.EmployeeAddress)
                            .FontSize(8);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text($"{_data.EmployeeCity}, {_data.EmployeeState} {_data.EmployeeZip}")
                            .FontSize(8);
                    });
                });

                row.ConstantItem(4);

                row.ConstantItem(240).Column(rightCol =>
                {
                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeBox(boxRow.RelativeItem(), "7  Social security tips", 0m);
                        boxRow.ConstantItem(4);
                        ComposeBox(boxRow.RelativeItem(), "8  Allocated tips", 0m);
                    });

                    rightCol.Item().Height(4);

                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeBox(boxRow.RelativeItem(), "9  ", 0m, showZero: false);
                        boxRow.ConstantItem(4);
                        ComposeBox(boxRow.RelativeItem(), "10  Dependent care benefits", 0m);
                    });

                    rightCol.Item().Height(4);

                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeBox(boxRow.RelativeItem(), "11  Nonqualified plans", 0m);
                        boxRow.ConstantItem(4);
                        ComposeTextBox(boxRow.RelativeItem(), "12a  See instructions for box 12", "");
                    });
                });
            });

            column.Item().Height(4);

            // Row 4: Boxes 13, 14 + 12b-d
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(leftCol =>
                {
                    leftCol.Item().Row(boxRow =>
                    {
                        boxRow.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
                        {
                            col.Item().Padding(3).Text("13  Statutory employee / Retirement plan / Third-party sick pay")
                                .FontSize(6).FontColor(LabelColor);
                            col.Item().PaddingLeft(5).PaddingBottom(4)
                                .Text("").FontSize(8);
                        });
                    });

                    leftCol.Item().Height(4);

                    leftCol.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Padding(3).Text("14  Other")
                            .FontSize(6).FontColor(LabelColor);
                        col.Item().PaddingLeft(5).PaddingBottom(4)
                            .Text("").FontSize(8);
                    });
                });

                row.ConstantItem(4);

                row.ConstantItem(240).Column(rightCol =>
                {
                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeTextBox(boxRow.RelativeItem(), "12b", "");
                        boxRow.ConstantItem(4);
                        ComposeTextBox(boxRow.RelativeItem(), "12c", "");
                    });

                    rightCol.Item().Height(4);

                    rightCol.Item().Row(boxRow =>
                    {
                        ComposeTextBox(boxRow.RelativeItem(), "12d", "");
                    });
                });
            });

            column.Item().Height(8);

            // State and Local section (Boxes 15-20)
            column.Item().Background("#F5F7FA").Border(1).BorderColor(BorderColor).Padding(2).Column(stateCol =>
            {
                stateCol.Item().Padding(3).Text("State and Local Tax Information")
                    .FontSize(8).Bold().FontColor("#1B3A5C");

                stateCol.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);   // 15 State
                        columns.RelativeColumn(1);   // 15 Employer's state ID
                        columns.RelativeColumn(1.5f); // 16 State wages
                        columns.RelativeColumn(1.5f); // 17 State tax
                        columns.RelativeColumn(1.5f); // 18 Local wages
                        columns.RelativeColumn(1.5f); // 19 Local tax
                        columns.RelativeColumn(1.5f); // 20 Locality name
                    });

                    // Header labels
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                        .Text("15  State").FontSize(6).FontColor(LabelColor);
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                        .Text("Employer's state ID").FontSize(6).FontColor(LabelColor);
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                        .Text("16  State wages, tips, etc.").FontSize(6).FontColor(LabelColor);
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                        .Text("17  State income tax").FontSize(6).FontColor(LabelColor);
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                        .Text("18  Local wages, tips, etc.").FontSize(6).FontColor(LabelColor);
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                        .Text("19  Local income tax").FontSize(6).FontColor(LabelColor);
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(3)
                        .Text("20  Locality name").FontSize(6).FontColor(LabelColor);

                    // Data row
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                        .Text("OH").FontSize(9).Bold();
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                        .Text(FormatEin(_data.EmployerEin)).FontSize(7);
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                        .Text(FormatCurrency(_data.Box16StateWages)).FontSize(8).Bold();
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                        .Text(FormatCurrency(_data.Box17StateTax)).FontSize(8).Bold();
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                        .Text(FormatCurrency(_data.Box18LocalWages)).FontSize(8).Bold();
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                        .Text(FormatCurrency(_data.Box19LocalTax)).FontSize(8).Bold();
                    table.Cell().Border(1).BorderColor(LightBorderColor).Padding(4)
                        .Text(_data.Box20LocalityName).FontSize(8).Bold();
                });
            });
        });
    }

    private static void ComposeBox(IContainer container, string label, decimal value, bool showZero = true)
    {
        container.Border(1).BorderColor(BorderColor).Background(BoxBg).Column(col =>
        {
            col.Item().Padding(3).Text(label)
                .FontSize(6).FontColor(LabelColor);
            col.Item().PaddingLeft(5).PaddingBottom(4).AlignRight().PaddingRight(5)
                .Text(showZero || value != 0 ? FormatCurrency(value) : "")
                .FontSize(10).Bold();
        });
    }

    private static void ComposeTextBox(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(BorderColor).Background(BoxBg).Column(col =>
        {
            col.Item().Padding(3).Text(label)
                .FontSize(6).FontColor(LabelColor);
            col.Item().PaddingLeft(5).PaddingBottom(4)
                .Text(value).FontSize(8);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(LightBorderColor);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Form W-2  Tax Year {_data.TaxYear}")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignCenter()
                    .Text("Copy B - To Be Filed With Employee's FEDERAL Tax Return")
                    .FontSize(7).FontColor("#94A3B8");
                row.RelativeItem().AlignRight()
                    .Text($"Generated {DateTime.Now:MM/dd/yyyy}")
                    .FontSize(7).FontColor("#94A3B8");
            });
        });
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("$#,##0.00");
    }

    private static string FormatEin(string ein)
    {
        if (ein.Length == 9)
            return $"{ein[..2]}-{ein[2..]}";
        return ein;
    }
}
