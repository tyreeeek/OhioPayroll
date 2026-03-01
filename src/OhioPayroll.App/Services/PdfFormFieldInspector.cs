using System.Text.Json;
using iText.Forms;
using iText.Kernel.Pdf;

namespace OhioPayroll.App.Services;

/// <summary>
/// Utility to inspect IRS PDF forms and discover all AcroForm field names, types, and values.
/// This is used during development to create field mapping spreadsheets.
/// </summary>
public static class PdfFormFieldInspector
{
    public class PdfFieldInfo
    {
        public string FieldName { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public string? CurrentValue { get; set; }
        public string? AlternateFieldName { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsRequired { get; set; }
    }

    /// <summary>
    /// Inspects an embedded PDF resource and exports all field information to JSON.
    /// </summary>
    /// <param name="embeddedResourceName">Full resource name, e.g., "OhioPayroll.App.Assets.IrsForms.fw2.pdf"</param>
    /// <param name="outputJsonPath">Path where JSON file will be saved</param>
    public static void InspectEmbeddedForm(string embeddedResourceName, string outputJsonPath)
    {
        var assembly = typeof(PdfFormFieldInspector).Assembly;

        using var stream = assembly.GetManifestResourceStream(embeddedResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded resource '{embeddedResourceName}' not found. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        InspectFormFromStream(stream, outputJsonPath);
    }

    /// <summary>
    /// Inspects a PDF file from disk and exports all field information to JSON.
    /// </summary>
    /// <param name="pdfFilePath">Path to the PDF file</param>
    /// <param name="outputJsonPath">Path where JSON file will be saved</param>
    public static void InspectFormFromFile(string pdfFilePath, string outputJsonPath)
    {
        using var stream = new FileStream(pdfFilePath, FileMode.Open, FileAccess.Read);
        InspectFormFromStream(stream, outputJsonPath);
    }

    private static void InspectFormFromStream(Stream pdfStream, string outputJsonPath)
    {
        var fields = new List<PdfFieldInfo>();

        using (var pdfReader = new PdfReader(pdfStream))
        using (var pdfDocument = new PdfDocument(pdfReader))
        {
            var acroForm = PdfAcroForm.GetAcroForm(pdfDocument, false);

            if (acroForm == null)
            {
                throw new InvalidOperationException("PDF does not contain AcroForm fields (not a fillable form).");
            }

            var formFields = acroForm.GetAllFormFields();

            foreach (var kvp in formFields)
            {
                var fieldName = kvp.Key;
                var field = kvp.Value;

                var fieldInfo = new PdfFieldInfo
                {
                    FieldName = fieldName,
                    FieldType = GetFieldTypeName(field),
                    CurrentValue = field.GetValueAsString(),
                    AlternateFieldName = field.GetAlternativeName()?.GetValue(),
                    IsReadOnly = field.IsReadOnly(),
                    IsRequired = field.IsRequired()
                };

                fields.Add(fieldInfo);
            }
        }

        // Sort by field name for easier review
        var sortedFields = fields.OrderBy(f => f.FieldName).ToList();

        // Export to JSON with nice formatting
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(new
        {
            TotalFields = sortedFields.Count,
            InspectionDate = DateTime.UtcNow,
            Fields = sortedFields
        }, options);

        File.WriteAllText(outputJsonPath, json);

        Console.WriteLine($"✓ Inspected PDF: {sortedFields.Count} fields found");
        Console.WriteLine($"✓ Exported to: {outputJsonPath}");
    }

    private static string GetFieldTypeName(iText.Forms.Fields.PdfFormField field)
    {
        // Map iText field types to readable names
        var fieldType = field.GetFormType();

        if (fieldType == null)
        {
            return "Unknown";
        }

        // In iText 9, GetFormType() returns a PdfName
        var typeString = fieldType.GetValue();

        // Map common PDF form field type names
        return typeString switch
        {
            "/Btn" => "Button",
            "/Tx" => "Text",
            "/Ch" => "Choice",
            "/Sig" => "Signature",
            _ => typeString ?? "Unknown"
        };
    }

    /// <summary>
    /// Quick helper to inspect all 4 IRS forms at once.
    /// </summary>
    public static void InspectAllIrsForms(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var forms = new[]
        {
            ("OhioPayroll.App.Assets.IrsForms.fw2.pdf", "fw2-fields.json"),
            ("OhioPayroll.App.Assets.IrsForms.f1099nec.pdf", "f1099nec-fields.json"),
            ("OhioPayroll.App.Assets.IrsForms.f940.pdf", "f940-fields.json"),
            ("OhioPayroll.App.Assets.IrsForms.f941.pdf", "f941-fields.json")
        };

        Console.WriteLine("Inspecting IRS PDF forms...\n");

        foreach (var (resourceName, outputFileName) in forms)
        {
            var outputPath = Path.Combine(outputDirectory, outputFileName);
            try
            {
                InspectEmbeddedForm(resourceName, outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to inspect {resourceName}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nAll field mappings exported to: {outputDirectory}");
    }
}
