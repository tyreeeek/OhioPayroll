using iText.Forms;
using iText.Kernel.Pdf;

namespace OhioPayroll.App.Services.PdfFormFillers;

/// <summary>
/// Base class for filling IRS PDF forms with data from the payroll system.
/// Handles loading embedded PDF templates, filling form fields, and saving the result.
/// </summary>
/// <typeparam name="TData">The data model type (e.g., W2Data, Form1099NecData)</typeparam>
public abstract class IrsFormFillerBase<TData>
{
    /// <summary>
    /// The embedded resource name for the IRS PDF template.
    /// Example: "OhioPayroll.App.Assets.IrsForms.fw2.pdf"
    /// </summary>
    protected abstract string EmbeddedResourceName { get; }

    /// <summary>
    /// Maps data from the model to PDF form field names and values.
    /// Derived classes must implement this to provide form-specific field mappings.
    /// </summary>
    /// <param name="data">The data to map</param>
    /// <returns>Dictionary of PDF field names to values</returns>
    protected abstract Dictionary<string, string> MapDataToFields(TData data);

    /// <summary>
    /// Fills an IRS PDF form with data and returns it as a byte array.
    /// </summary>
    /// <param name="data">The data to fill into the form</param>
    /// <returns>PDF byte array with filled and flattened form</returns>
    public byte[] FillForm(TData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // Get the field mappings from the derived class
        var fieldMappings = MapDataToFields(data);

        // Load the embedded PDF template
        var assembly = typeof(IrsFormFillerBase<TData>).Assembly;
        using var resourceStream = assembly.GetManifestResourceStream(EmbeddedResourceName);

        if (resourceStream == null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        // Copy to seekable MemoryStream (required for iText, prevents reuse issues)
        using var templateStream = new MemoryStream();
        resourceStream.CopyTo(templateStream);
        templateStream.Position = 0;

        // Create output stream for filled PDF
        using var outputStream = new MemoryStream();

        // Fill the PDF form
        using (var pdfReader = new PdfReader(templateStream))
        using (var pdfWriter = new PdfWriter(outputStream))
        using (var pdfDocument = new PdfDocument(pdfReader, pdfWriter))
        {
            var acroForm = PdfAcroForm.GetAcroForm(pdfDocument, false);

            if (acroForm == null)
            {
                throw new InvalidOperationException(
                    $"PDF template '{EmbeddedResourceName}' does not contain fillable form fields.");
            }

            // Track any field fill failures for diagnostics
            var fieldFailures = new List<(string FieldName, string? Value, string Error)>();

            // Fill each field
            foreach (var (fieldName, fieldValue) in fieldMappings)
            {
                var field = acroForm.GetField(fieldName);
                if (field == null) continue;

                try
                {
                    field.SetValue(fieldValue ?? string.Empty);
                }
                catch (Exception ex)
                {
                    // Record the failure for diagnostics
                    fieldFailures.Add((fieldName, fieldValue, ex.Message));
                    System.Diagnostics.Debug.WriteLine(
                        $"[IrsFormFiller] Failed to fill field '{fieldName}' with value '{fieldValue}': {ex.Message}");
                }
            }

            // Log summary of failures if any occurred
            if (fieldFailures.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[IrsFormFiller] {fieldFailures.Count} field(s) failed to fill in {EmbeddedResourceName}");
            }

            // Flatten the form (make it non-editable)
            acroForm.FlattenFields();
        }

        return outputStream.ToArray();
    }

    /// <summary>
    /// Fills an IRS PDF form with data and saves it to the specified path.
    /// </summary>
    public void FillAndSave(TData data, string outputPath)
    {
        var pdfBytes = FillForm(data);
        File.WriteAllBytes(outputPath, pdfBytes);
    }

    /// <summary>
    /// Fills multiple forms and saves them to individual files.
    /// </summary>
    /// <param name="dataList">List of data objects to fill</param>
    /// <param name="outputDirectory">Directory where filled PDFs will be saved</param>
    /// <param name="fileNameGenerator">Function to generate filename from data</param>
    /// <returns>List of generated file paths</returns>
    public List<string> FillAndSaveMultiple(
        IEnumerable<TData> dataList,
        string outputDirectory,
        Func<TData, string> fileNameGenerator)
    {
        Directory.CreateDirectory(outputDirectory);

        var generatedFiles = new List<string>();

        foreach (var data in dataList)
        {
            var fileName = fileNameGenerator(data);
            var outputPath = Path.Combine(outputDirectory, fileName);

            FillAndSave(data, outputPath);
            generatedFiles.Add(outputPath);
        }

        return generatedFiles;
    }

    /// <summary>
    /// Helper method to format Tax Identification Numbers (SSN/EIN).
    /// Removes all non-numeric characters.
    /// </summary>
    protected static string FormatTin(string? tin)
    {
        if (string.IsNullOrWhiteSpace(tin))
        {
            return string.Empty;
        }

        // Remove all non-numeric characters (dashes, spaces, etc.)
        var digitsOnly = new string(tin.Where(char.IsDigit).ToArray());

        // Validate length
        if (digitsOnly.Length != 9)
        {
            throw new ArgumentException($"TIN must be exactly 9 digits, got {digitsOnly.Length} digits from '{tin}'");
        }

        return digitsOnly;
    }

    /// <summary>
    /// Helper method to format currency amounts.
    /// Uses InvariantCulture with period as decimal separator (required by IRS forms).
    /// </summary>
    protected static string FormatMoney(decimal amount)
    {
        // IRS forms expect decimal amounts without currency symbols
        // and with period (not comma) as decimal separator
        return amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Helper method to format dates for IRS forms.
    /// </summary>
    protected static string FormatDate(DateTime? date, string format = "MM/dd/yyyy")
    {
        return date?.ToString(format) ?? string.Empty;
    }

    /// <summary>
    /// Helper method to format checkbox values.
    /// IRS PDFs may expect "1", "Yes", "X", or "On" for checked boxes.
    /// </summary>
    protected static string FormatCheckbox(bool isChecked, string checkedValue = "1")
    {
        return isChecked ? checkedValue : string.Empty;
    }

    /// <summary>
    /// Helper method to sanitize text for PDF fields.
    /// Removes or replaces characters that may cause issues in PDF forms.
    /// </summary>
    protected static string SanitizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Remove control characters and normalize whitespace
        var sanitized = new string(text
            .Where(c => !char.IsControl(c) || c == '\r' || c == '\n')
            .ToArray());

        return sanitized.Trim();
    }

    /// <summary>
    /// Helper method to add a field to the field mapping dictionary.
    /// Only adds non-null, non-whitespace values after sanitization.
    /// </summary>
    /// <param name="fields">The field dictionary to add to</param>
    /// <param name="fieldName">The PDF field name</param>
    /// <param name="value">The value to set (will be sanitized)</param>
    protected static void AddField(Dictionary<string, string> fields, string fieldName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[fieldName] = SanitizeText(value);
        }
    }
}
