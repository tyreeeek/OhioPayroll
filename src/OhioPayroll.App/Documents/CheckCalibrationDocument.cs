using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OhioPayroll.App.Documents;

/// <summary>
/// Prints a calibration/alignment test page for check printing.
/// Displays ruler markers and section boundary indicators so the user
/// can measure their printer's offset and enter it into PayrollSettings.
/// Uses only QuestPDF fluent API (no SkiaSharp Canvas dependency).
/// </summary>
public class CheckCalibrationDocument : IDocument
{
    private const float Margin = 0.25f; // inches

    // Check section boundaries (inches from top of printable area)
    private const float StubOneEnd = 3.5f;
    private const float CheckStart = 3.667f;
    private const float CheckEnd = 7.167f;
    private const float StubTwoStart = 7.333f;

    private static readonly string GridColor = "#CCCCCC";
    private static readonly string BoundaryColor = "#FF0000";
    private static readonly string LabelColor = "#333333";


    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(Margin, Unit.Inch);
            page.DefaultTextStyle(x => x.FontSize(7));

            page.Content().Column(column =>
            {
                // Title and instructions
                column.Item().PaddingBottom(4).Text("CHECK ALIGNMENT CALIBRATION PAGE")
                    .FontSize(14).Bold().FontColor(LabelColor);

                column.Item().PaddingBottom(2).Text(
                    "Print this page on your check printer. Measure any offset from the expected positions.")
                    .FontSize(9).FontColor("#666666");

                column.Item().PaddingBottom(2).Text(
                    "Horizontal rulers mark 0.25\" increments. Red lines mark check section boundaries.")
                    .FontSize(9).FontColor("#666666");

                column.Item().PaddingBottom(6).Text(
                    "Enter measured X/Y offset (inches, positive = right/down) into Settings > Check Offset.")
                    .FontSize(9).FontColor("#666666");

                column.Item().PaddingBottom(8).LineHorizontal(1).LineColor(LabelColor);

                // Top horizontal ruler (X-axis reference)
                column.Item().Element(ComposeHorizontalRuler);

                column.Item().PaddingTop(6);

                // Main calibration body with vertical position markers
                column.Item().Element(ComposeCalibrationBody);
            });
        });
    }

    private void ComposeHorizontalRuler(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text("HORIZONTAL RULER (inches from left margin)")
                .FontSize(7).Bold().FontColor(LabelColor);

            col.Item().PaddingTop(2).Row(row =>
            {
                // Create tick marks every 0.25" across 8" printable width
                for (int i = 0; i <= 32; i++) // 32 increments of 0.25" = 8"
                {
                    bool isInch = i % 4 == 0;
                    bool isHalfInch = i % 2 == 0;

                    row.ConstantItem(0.25f, Unit.Inch).Column(tickCol =>
                    {
                        if (isInch)
                        {
                            tickCol.Item().AlignLeft().Text($"{i / 4}\"")
                                .FontSize(6).Bold().FontColor(LabelColor);
                            tickCol.Item().AlignLeft().Width(1).Height(12)
                                .Background(LabelColor);
                        }
                        else if (isHalfInch)
                        {
                            tickCol.Item().AlignLeft().Width(1).Height(8)
                                .Background(GridColor);
                        }
                        else
                        {
                            tickCol.Item().AlignLeft().Width(1).Height(4)
                                .Background(GridColor);
                        }
                    });
                }
            });

            col.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(GridColor);
        });
    }

    private void ComposeCalibrationBody(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text("VERTICAL POSITIONS (inches from top of printable area)")
                .FontSize(7).Bold().FontColor(LabelColor);

            col.Item().PaddingTop(4);

            // Section markers with labels
            var markers = new (float inches, string label, bool isBoundary)[]
            {
                (0.25f, "0.25\"", false),
                (0.50f, "0.50\"", false),
                (0.75f, "0.75\"", false),
                (1.00f, "1.00\" --", false),
                (1.50f, "1.50\"", false),
                (2.00f, "2.00\" --", false),
                (2.50f, "2.50\"", false),
                (3.00f, "3.00\" --", false),
                (3.25f, "3.25\"", false),
                (3.50f, "3.50\" == EMPLOYEE STUB ENDS / PERFORATION ==", true),
                (3.667f, "3.67\" == CHECK SECTION BEGINS ==", true),
                (4.00f, "4.00\" --", false),
                (4.50f, "4.50\"", false),
                (5.00f, "5.00\" --", false),
                (5.50f, "5.50\"", false),
                (6.00f, "6.00\" --", false),
                (6.50f, "6.50\"", false),
                (7.00f, "7.00\" --", false),
                (7.167f, "7.17\" == CHECK SECTION ENDS / PERFORATION ==", true),
                (7.333f, "7.33\" == COMPANY STUB BEGINS ==", true),
                (7.50f, "7.50\"", false),
                (8.00f, "8.00\" --", false),
                (8.50f, "8.50\"", false),
                (9.00f, "9.00\" --", false),
                (9.50f, "9.50\"", false),
                (10.00f, "10.00\" --", false),
            };

            float previousPos = 0f;

            foreach (var (inches, label, isBoundary) in markers)
            {
                float gap = inches - previousPos;
                if (gap > 0)
                {
                    col.Item().Height(gap, Unit.Inch);
                }
                previousPos = inches;

                if (isBoundary)
                {
                    // Red boundary line with bold label
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(6).Height(2).Background(BoundaryColor);
                        row.RelativeItem().Height(2).Background(BoundaryColor);
                    });
                    col.Item().PaddingTop(1).Text(label)
                        .FontSize(8).Bold().FontColor(BoundaryColor);
                }
                else
                {
                    // Regular position marker
                    bool isWholeInch = label.Contains("--");
                    col.Item().Row(row =>
                    {
                        if (isWholeInch)
                        {
                            row.ConstantItem(6).Height(1.5f).Background(LabelColor);
                            row.ConstantItem(150).Height(1.5f).Background(LabelColor);
                        }
                        else
                        {
                            row.ConstantItem(6).Height(1).Background(GridColor);
                            row.ConstantItem(80).Height(1).Background(GridColor);
                        }
                        row.RelativeItem();
                    });
                    col.Item().Text(label)
                        .FontSize(isWholeInch ? 7 : 6)
                        .FontColor(isWholeInch ? LabelColor : GridColor);
                }
            }

            // Corner markers section
            col.Item().PaddingTop(12).LineHorizontal(1).LineColor(LabelColor);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Column(infoCol =>
                {
                    infoCol.Item().Text("CALIBRATION REFERENCE").FontSize(9).Bold().FontColor(LabelColor);
                    infoCol.Item().PaddingTop(2).Text("Expected section layout on letter paper (8.5\" x 11\"):").FontSize(8);
                    infoCol.Item().PaddingTop(2).Text("  Employee Stub:  0.00\" - 3.50\"  (3.50\" tall)").FontSize(8).FontFamily("Courier");
                    infoCol.Item().Text("  Perforation:    3.50\" - 3.67\"  (0.167\")").FontSize(8).FontFamily("Courier");
                    infoCol.Item().Text("  Check Section:  3.67\" - 7.17\"  (3.50\" tall)").FontSize(8).FontFamily("Courier");
                    infoCol.Item().Text("  Perforation:    7.17\" - 7.33\"  (0.167\")").FontSize(8).FontFamily("Courier");
                    infoCol.Item().Text("  Company Stub:   7.33\" - 10.50\" (3.167\" tall)").FontSize(8).FontFamily("Courier");
                    infoCol.Item().PaddingTop(4).Text("If lines appear shifted, enter the difference as Check Offset X/Y in Settings.")
                        .FontSize(8).FontColor("#666666");
                });
            });
        });
    }
}

