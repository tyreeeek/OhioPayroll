using OhioPayroll.App.Documents;
using OhioPayroll.App.Services.PdfFormFillers;

namespace OhioPayroll.App.Services;

/// <summary>
/// Production-grade service for rendering OFFICIAL IRS Form W-3 PDFs.
/// Uses actual IRS fillable PDF form (fw3.pdf) and fills it programmatically.
/// Returns flattened, non-editable PDFs ready for printing/filing.
/// </summary>
public static class OfficialW3PdfService
{
    /// <summary>
    /// Renders official IRS Form W-3 by filling the official IRS fillable PDF.
    /// Form W-3 is the transmittal form that accompanies W-2s when filing with the SSA.
    /// </summary>
    /// <param name="data">W-3 data to fill into the form</param>
    /// <returns>Official IRS W-3 PDF as byte array</returns>
    public static byte[] RenderW3Pdf(W3Data data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var filler = new W3FormFiller();
        return filler.FillForm(data);
    }
}
