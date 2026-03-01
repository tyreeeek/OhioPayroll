using OhioPayroll.App.Documents;
using OhioPayroll.App.Services.PdfFormFillers;

namespace OhioPayroll.App.Services;

/// <summary>
/// Production-grade service for rendering OFFICIAL IRS Form 1096 PDFs.
/// Uses actual IRS fillable PDF form (f1096.pdf) and fills it programmatically.
/// Returns flattened, non-editable PDFs ready for printing/filing.
/// </summary>
public static class Official1096PdfService
{
    /// <summary>
    /// Renders official IRS Form 1096 by filling the official IRS fillable PDF.
    /// Form 1096 is the transmittal form that accompanies 1099 forms when filing with the IRS.
    /// </summary>
    /// <param name="data">Form 1096 data to fill into the form</param>
    /// <returns>Official IRS Form 1096 PDF as byte array</returns>
    public static byte[] Render1096Pdf(Form1096Data data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var filler = new Form1096FormFiller();
        return filler.FillForm(data);
    }
}
