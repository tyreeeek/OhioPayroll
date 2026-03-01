using OhioPayroll.App.Documents;
using OhioPayroll.App.Services.PdfFormFillers;

namespace OhioPayroll.App.Services;

/// <summary>
/// Production-grade service for rendering OFFICIAL IRS Form 1099-NEC PDFs.
/// Uses actual IRS fillable PDF forms (f1099nec.pdf) and fills them programmatically.
/// Returns flattened, non-editable PDFs ready for printing/distribution.
/// </summary>
public static class Official1099NecPdfService
{
    /// <summary>
    /// Renders official IRS Form 1099-NEC recipient copies by filling the official IRS fillable PDF.
    /// Returns a flattened PDF with all copies (Copy 1, Copy 2, Copy B, Copy C) filled.
    /// </summary>
    /// <param name="data">1099-NEC data to fill into the form</param>
    /// <returns>Official IRS 1099-NEC PDF as byte array</returns>
    public static byte[] Render1099NecRecipientPdf(Form1099NecData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var filler = new Form1099NecFormFiller();
        return filler.FillForm(data);
    }
}
