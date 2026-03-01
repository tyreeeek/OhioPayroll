using FluentAssertions;
using OhioPayroll.App.Services;

namespace OhioPayroll.Engine.Tests;

/// <summary>
/// Tests for the PDF form field inspector utility.
/// These tests also serve as a utility to generate field mapping JSON files during development.
/// </summary>
public class PdfFormFieldInspectorTests
{
    private readonly string _outputDirectory;

    public PdfFormFieldInspectorTests()
    {
        // Output to a temp directory that's easy to find
        _outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "OhioPayroll_IRS_Fields"
        );

        Directory.CreateDirectory(_outputDirectory);
    }

    [Fact]
    public void InspectW2Form_GeneratesFieldMapping()
    {
        // Arrange
        var outputPath = Path.Combine(_outputDirectory, "fw2-fields.json");

        // Act
        PdfFormFieldInspector.InspectEmbeddedForm(
            "OhioPayroll.App.Assets.IrsForms.fw2.pdf",
            outputPath
        );

        // Assert
        File.Exists(outputPath).Should().BeTrue("field mapping JSON should be created");
        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.Should().BeGreaterThan(0, "JSON file should not be empty");

        // Output path for manual review
        Console.WriteLine($"W-2 field mapping saved to: {outputPath}");
    }

    [Fact]
    public void InspectForm1099Nec_GeneratesFieldMapping()
    {
        // Arrange
        var outputPath = Path.Combine(_outputDirectory, "f1099nec-fields.json");

        // Act
        PdfFormFieldInspector.InspectEmbeddedForm(
            "OhioPayroll.App.Assets.IrsForms.f1099nec.pdf",
            outputPath
        );

        // Assert
        File.Exists(outputPath).Should().BeTrue("field mapping JSON should be created");
        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.Should().BeGreaterThan(0, "JSON file should not be empty");

        Console.WriteLine($"1099-NEC field mapping saved to: {outputPath}");
    }

    [Fact]
    public void InspectForm940_GeneratesFieldMapping()
    {
        // Arrange
        var outputPath = Path.Combine(_outputDirectory, "f940-fields.json");

        // Act
        PdfFormFieldInspector.InspectEmbeddedForm(
            "OhioPayroll.App.Assets.IrsForms.f940.pdf",
            outputPath
        );

        // Assert
        File.Exists(outputPath).Should().BeTrue("field mapping JSON should be created");
        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.Should().BeGreaterThan(0, "JSON file should not be empty");

        Console.WriteLine($"Form 940 field mapping saved to: {outputPath}");
    }

    [Fact]
    public void InspectForm941_GeneratesFieldMapping()
    {
        // Arrange
        var outputPath = Path.Combine(_outputDirectory, "f941-fields.json");

        // Act
        PdfFormFieldInspector.InspectEmbeddedForm(
            "OhioPayroll.App.Assets.IrsForms.f941.pdf",
            outputPath
        );

        // Assert
        File.Exists(outputPath).Should().BeTrue("field mapping JSON should be created");
        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.Should().BeGreaterThan(0, "JSON file should not be empty");

        Console.WriteLine($"Form 941 field mapping saved to: {outputPath}");
    }

    [Fact]
    public void InspectAllIrsForms_GeneratesAllFieldMappings()
    {
        // Act
        PdfFormFieldInspector.InspectAllIrsForms(_outputDirectory);

        // Assert
        var expectedFiles = new[]
        {
            "fw2-fields.json",
            "f1099nec-fields.json",
            "f940-fields.json",
            "f941-fields.json"
        };

        foreach (var fileName in expectedFiles)
        {
            var filePath = Path.Combine(_outputDirectory, fileName);
            File.Exists(filePath).Should().BeTrue($"{fileName} should be created");

            var fileInfo = new FileInfo(filePath);
            fileInfo.Length.Should().BeGreaterThan(0, $"{fileName} should not be empty");
        }

        Console.WriteLine($"\nAll IRS form field mappings saved to: {_outputDirectory}");
        Console.WriteLine("You can now open these JSON files to see all available PDF field names.");
    }

    [Fact]
    public void InspectW2Form_ReturnsExpectedFieldCount()
    {
        // Arrange
        var outputPath = Path.Combine(_outputDirectory, "fw2-fields-test.json");

        // Act
        PdfFormFieldInspector.InspectEmbeddedForm(
            "OhioPayroll.App.Assets.IrsForms.fw2.pdf",
            outputPath
        );

        // Assert - W-2 form should have many fields (typically 100-300)
        var json = File.ReadAllText(outputPath);
        json.Should().Contain("\"TotalFields\":", "JSON should contain field count");
        json.Should().Contain("\"FieldName\":", "JSON should contain field definitions");
    }

    [Fact]
    public void InspectEmbeddedForm_WithInvalidResourceName_ThrowsException()
    {
        // Arrange
        var outputPath = Path.Combine(_outputDirectory, "invalid.json");

        // Act
        var act = () => PdfFormFieldInspector.InspectEmbeddedForm(
            "OhioPayroll.App.Assets.IrsForms.nonexistent.pdf",
            outputPath
        );

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
