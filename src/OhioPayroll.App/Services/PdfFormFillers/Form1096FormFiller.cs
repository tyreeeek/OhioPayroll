using OhioPayroll.App.Documents;

namespace OhioPayroll.App.Services.PdfFormFillers;

/// <summary>
/// Fills IRS Form 1096 (Annual Summary and Transmittal of U.S. Information Returns) PDF.
/// </summary>
public class Form1096FormFiller : IrsFormFillerBase<Form1096Data>
{
    protected override string EmbeddedResourceName => "OhioPayroll.App.Assets.IrsForms.f1096.pdf";

    protected override Dictionary<string, string> MapDataToFields(Form1096Data data)
    {
        var fields = new Dictionary<string, string>();

        var prefix = "topmostSubform[0].Page1[0]";

        // Filer information
        AddField(fields, $"{prefix}.f1_1[0]", data.FilerName);
        AddField(fields, $"{prefix}.f1_2[0]", data.FilerAddress);
        AddField(fields, $"{prefix}.f1_3[0]", $"{data.FilerCity}, {data.FilerState} {data.FilerZip}");
        AddField(fields, $"{prefix}.f1_4[0]", FormatTin(data.FilerTin));

        // Contact information
        if (!string.IsNullOrEmpty(data.ContactName))
            AddField(fields, $"{prefix}.f1_5[0]", data.ContactName);
        if (!string.IsNullOrEmpty(data.ContactPhone))
            AddField(fields, $"{prefix}.f1_6[0]", data.ContactPhone);
        if (!string.IsNullOrEmpty(data.ContactEmail))
            AddField(fields, $"{prefix}.f1_7[0]", data.ContactEmail);

        // Box 3 - Total number of forms
        AddField(fields, $"{prefix}.f1_8[0]", data.Box3_TotalForms.ToString());

        // Box 4 - Federal income tax withheld
        AddField(fields, $"{prefix}.f1_9[0]", FormatMoney(data.Box4_FederalTaxWithheld));

        // Box 5 - Total amount reported
        AddField(fields, $"{prefix}.f1_10[0]", FormatMoney(data.Box5_TotalAmount));

        // Form type checkbox - map to correct PDF field based on form type
        // Field names are based on the official IRS f1096.pdf form structure
        var formTypeCheckboxField = data.FormType switch
        {
            "1099-NEC" => $"{prefix}.c1_1[0]",
            "1099-MISC" => $"{prefix}.c1_2[0]",
            "1099-INT" => $"{prefix}.c1_3[0]",
            "1099-DIV" => $"{prefix}.c1_4[0]",
            "1099-R" => $"{prefix}.c1_5[0]",
            "1099-G" => $"{prefix}.c1_6[0]",
            "1099-B" => $"{prefix}.c1_7[0]",
            "1099-S" => $"{prefix}.c1_8[0]",
            "1099-K" => $"{prefix}.c1_9[0]",
            _ => throw new ArgumentException(
                $"Unsupported Form1096 form type: '{data.FormType}'. " +
                $"Supported types: 1099-NEC, 1099-MISC, 1099-INT, 1099-DIV, 1099-R, 1099-G, 1099-B, 1099-S, 1099-K",
                nameof(data))
        };
        AddField(fields, formTypeCheckboxField, FormatCheckbox(true));

        return fields;
    }
}
