using OhioPayroll.App.Documents;
using OhioPayroll.App.Services.PdfFormFillers;

namespace OhioPayroll.App.Services;

/// <summary>
/// Production-grade service for rendering OFFICIAL IRS Form 940 PDFs.
/// Uses actual IRS fillable PDF form (f940.pdf) and fills it programmatically.
/// Returns flattened, non-editable PDFs ready for printing/filing.
/// </summary>
public static class Official940PdfService
{
    /// <summary>
    /// Renders official IRS Form 940 (Employer's Annual Federal Unemployment Tax Return)
    /// by filling the official IRS fillable PDF.
    /// </summary>
    /// <param name="data">Form 940 data to fill into the form</param>
    /// <returns>Official IRS Form 940 PDF as byte array</returns>
    public static byte[] Render940Pdf(Form940Data data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var filler = new Form940FormFiller();
        return filler.FillForm(data);
    }
}
