using OhioPayroll.App.Documents;

namespace OhioPayroll.App.Services.PdfFormFillers;

/// <summary>
/// Fills IRS Form 1099-NEC (Nonemployee Compensation) PDF.
/// </summary>
public class Form1099NecFormFiller : IrsFormFillerBase<Form1099NecData>
{
    protected override string EmbeddedResourceName => "OhioPayroll.App.Assets.IrsForms.f1099nec.pdf";

    protected override Dictionary<string, string> MapDataToFields(Form1099NecData data)
    {
        var fields = new Dictionary<string, string>();
        var copy = "topmostSubform[0].Copy1[0]";

        // Tax year
        AddField(fields, $"{copy}.PgHeader[0].CalendarYear[0].f2_1[0]", data.TaxYear.ToString());

        // Payer info (left column)
        AddField(fields, $"{copy}.LeftCol[0].f2_2[0]", data.PayerName);
        AddField(fields, $"{copy}.LeftCol[0].f2_3[0]", data.PayerAddress);
        AddField(fields, $"{copy}.LeftCol[0].f2_4[0]", BuildCityStateZip(data.PayerCity, data.PayerState, data.PayerZip));
        AddField(fields, $"{copy}.LeftCol[0].f2_5[0]", data.PayerPhone ?? "");
        AddField(fields, $"{copy}.LeftCol[0].f2_6[0]", FormatTin(data.PayerTin));
        AddField(fields, $"{copy}.LeftCol[0].f2_7[0]", FormatTin(data.RecipientTin));
        AddField(fields, $"{copy}.LeftCol[0].f2_8[0]", data.AccountNumber);

        // Box 1: Nonemployee compensation
        AddField(fields, $"{copy}.RightCol[0].f2_9[0]", FormatMoney(data.Box1_NonemployeeCompensation));

        // Box 4: Federal tax withheld
        if (data.Box4_FederalTaxWithheld > 0)
            AddField(fields, $"{copy}.RightCol[0].f2_10[0]", FormatMoney(data.Box4_FederalTaxWithheld));

        // Recipient info
        AddField(fields, $"{copy}.RightCol[0].Box5_ReadOrder[0].f2_12[0]", data.RecipientName);
        AddField(fields, $"{copy}.RightCol[0].Box5_ReadOrder[0].f2_13[0]", data.RecipientAddress);
        AddField(fields, $"{copy}.RightCol[0].Box6_ReadOrder[0].f2_14[0]",
            BuildCityStateZip(data.RecipientCity, data.RecipientState, data.RecipientZip));

        // State info
        AddField(fields, $"{copy}.RightCol[0].Box7_ReadOrder[0].f2_16[0]", data.StatePayerNo);
        AddField(fields, $"{copy}.RightCol[0].Box7_ReadOrder[0].f2_17[0]", FormatMoney(data.StateIncome));
        if (data.StateTaxWithheld > 0)
            AddField(fields, $"{copy}.RightCol[0].f2_11[0]", FormatMoney(data.StateTaxWithheld));

        // Replicate to other copies
        ReplicateToAllCopies(fields);

        return fields;
    }

    private static string BuildCityStateZip(string? city, string? state, string? zip)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(city))
            parts.Add(city.Trim());
        if (!string.IsNullOrWhiteSpace(state))
            parts.Add(state.Trim());
        if (!string.IsNullOrWhiteSpace(zip))
            parts.Add(zip.Trim());

        if (parts.Count == 0)
            return string.Empty;

        // Format as "City, State Zip" or partial versions
        if (parts.Count == 3)
            return $"{parts[0]}, {parts[1]} {parts[2]}";
        if (parts.Count == 2)
            return $"{parts[0]}, {parts[1]}";
        return parts[0];
    }

    private void ReplicateToAllCopies(Dictionary<string, string> fields)
    {
        var copies = new[] { "Copy2", "CopyB", "CopyC" };
        var copy1Fields = fields.Where(kvp => kvp.Key.Contains("Copy1")).ToList();

        foreach (var copy in copies)
        {
            foreach (var (fieldName, value) in copy1Fields)
            {
                fields[fieldName.Replace("Copy1", copy)] = value;
            }
        }
    }
}
