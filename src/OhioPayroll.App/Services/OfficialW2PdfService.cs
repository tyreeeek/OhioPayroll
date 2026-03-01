using OhioPayroll.App.Documents;
using OhioPayroll.App.Services.PdfFormFillers;

namespace OhioPayroll.App.Services;

/// <summary>
/// Production-grade service for rendering OFFICIAL IRS Form W-2 PDFs.
/// Uses actual IRS fillable PDF forms (fw2.pdf) and fills them programmatically.
/// Returns flattened, non-editable PDFs ready for printing/distribution.
/// </summary>
public static class OfficialW2PdfService
{
    /// <summary>
    /// Renders official IRS Form W-2 recipient copies by filling the official IRS fillable PDF.
    /// Returns a flattened PDF with all 4 copies (A, B, C, D) filled.
    /// </summary>
    /// <param name="data">W-2 data to fill into the form</param>
    /// <returns>Official IRS W-2 PDF as byte array</returns>
    public static byte[] RenderW2RecipientPdf(W2Data data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var filler = new W2FormFiller();
        return filler.FillForm(data);
    }
}
