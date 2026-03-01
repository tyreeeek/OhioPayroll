using OhioPayroll.App.Documents;

namespace OhioPayroll.App.Services.PdfFormFillers;

/// <summary>
/// Fills IRS Form W-2 (Wage and Tax Statement) PDF with employee wage and tax data.
/// </summary>
public class W2FormFiller : IrsFormFillerBase<W2Data>
{
    protected override string EmbeddedResourceName => "OhioPayroll.App.Assets.IrsForms.fw2.pdf";

    protected override Dictionary<string, string> MapDataToFields(W2Data data)
    {
        var fields = new Dictionary<string, string>();

        var copy = "topmostSubform[0].Copy1[0]";

        // Box a: Employee SSN
        AddField(fields, $"{copy}.BoxA_ReadOrder[0].f2_01[0]", FormatTin(data.EmployeeSsn));

        // Box b: Employer EIN
        AddField(fields, $"{copy}.Col_Left[0].f2_02[0]", FormatTin(data.EmployerEin));

        // Box c: Employer name & address
        AddField(fields, $"{copy}.Col_Left[0].f2_03[0]", data.EmployerName);
        AddField(fields, $"{copy}.Col_Left[0].f2_04[0]", data.EmployerAddress);
        AddField(fields, $"{copy}.Col_Left[0].f2_07[0]", $"{data.EmployerCity}, {data.EmployerState} {data.EmployerZip}");

        // Box e: Employee name
        AddField(fields, $"{copy}.Col_Left[0].FirstName_ReadOrder[0].f2_05[0]", data.EmployeeFirstName);
        AddField(fields, $"{copy}.Col_Left[0].LastName_ReadOrder[0].f2_06[0]", data.EmployeeLastName);

        // Box f: Employee address
        AddField(fields, $"{copy}.Col_Left[0].f2_08[0]", $"{data.EmployeeAddress}, {data.EmployeeCity}, {data.EmployeeState} {data.EmployeeZip}");

        // Box 1: Wages
        AddField(fields, $"{copy}.Col_Right[0].Box1_ReadOrder[0].f2_09[0]", FormatMoney(data.Box1WagesTips));

        // Box 2: Federal tax
        AddField(fields, $"{copy}.Col_Right[0].f2_10[0]", FormatMoney(data.Box2FederalTaxWithheld));

        // Box 3: SS wages
        AddField(fields, $"{copy}.Col_Right[0].Box3_ReadOrder[0].f2_11[0]", FormatMoney(data.Box3SocialSecurityWages));

        // Box 4: SS tax
        AddField(fields, $"{copy}.Col_Right[0].f2_12[0]", FormatMoney(data.Box4SocialSecurityTax));

        // Box 5: Medicare wages
        AddField(fields, $"{copy}.Col_Right[0].Box5_ReadOrder[0].f2_13[0]", FormatMoney(data.Box5MedicareWages));

        // Box 6: Medicare tax
        AddField(fields, $"{copy}.Col_Right[0].f2_14[0]", FormatMoney(data.Box6MedicareTax));

        // Box 15: State & state ID
        AddField(fields, $"{copy}.Boxes15_ReadOrder[0].Box15_ReadOrder[0].f2_31[0]", data.EmployerState);
        AddField(fields, $"{copy}.Boxes15_ReadOrder[0].f2_32[0]", data.StateWithholdingId);

        // Box 16: State wages
        AddField(fields, $"{copy}.Box16_ReadOrder[0].f2_35[0]", FormatMoney(data.Box16StateWages));

        // Box 17: State tax
        AddField(fields, $"{copy}.Box17_ReadOrder[0].f2_37[0]", FormatMoney(data.Box17StateTax));

        // Box 18: Local wages
        if (data.Box18LocalWages > 0)
            AddField(fields, $"{copy}.Box18_ReadOrder[0].f2_39[0]", FormatMoney(data.Box18LocalWages));

        // Box 19: Local tax
        if (data.Box19LocalTax > 0)
            AddField(fields, $"{copy}.Box19_ReadOrder[0].f2_41[0]", FormatMoney(data.Box19LocalTax));

        // Replicate to other copies
        ReplicateToAllCopies(fields);

        return fields;
    }

    private void ReplicateToAllCopies(Dictionary<string, string> fields)
    {
        var copies = new[] { "CopyA", "CopyB", "CopyC", "CopyD" };
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
