using OhioPayroll.App.Documents;

namespace OhioPayroll.App.Services.PdfFormFillers;

/// <summary>
/// Fills IRS Form 940 (Employer's Annual Federal Unemployment Tax Return) PDF.
/// </summary>
public class Form940FormFiller : IrsFormFillerBase<Form940Data>
{
    protected override string EmbeddedResourceName => "OhioPayroll.App.Assets.IrsForms.f940.pdf";

    protected override Dictionary<string, string> MapDataToFields(Form940Data data)
    {
        var fields = new Dictionary<string, string>();

        var prefix = "topmostSubform[0].Page1[0]";
        var entityArea = $"{prefix}.EntityArea[0]";

        // Employer information (EntityArea section)
        AddField(fields, $"{entityArea}.f1_1[0]", FormatTin(data.EmployerEin));
        AddField(fields, $"{entityArea}.f1_2[0]", data.EmployerName);
        AddField(fields, $"{entityArea}.f1_3[0]", data.EmployerAddress);
        AddField(fields, $"{entityArea}.f1_4[0]", data.EmployerCity);
        AddField(fields, $"{entityArea}.f1_5[0]", data.EmployerState);
        AddField(fields, $"{entityArea}.f1_6[0]", data.EmployerZip);

        // Part 1 - Line 1a (State)
        AddField(fields, $"{prefix}.f1_12[0]", data.Line1a_State);

        // Part 2 - Lines 3-8 (based on sequential field numbering)
        AddField(fields, $"{prefix}.f1_13[0]", FormatMoney(data.Line3_TotalPayments));
        AddField(fields, $"{prefix}.f1_14[0]", FormatMoney(data.Line4_ExemptPayments));
        AddField(fields, $"{prefix}.f1_15[0]", FormatMoney(data.Line5_TaxableFutaWages));
        AddField(fields, $"{prefix}.f1_16[0]", FormatMoney(data.Line6_FutaTaxBeforeAdjustments));
        AddField(fields, $"{prefix}.f1_17[0]", FormatMoney(data.Line7_Adjustments));
        AddField(fields, $"{prefix}.f1_18[0]", FormatMoney(data.Line8_TotalFutaTax));

        // Part 3 - Line 12 (Total deposits)
        AddField(fields, $"{prefix}.f1_19[0]", FormatMoney(data.Line12_TotalDeposits));

        // Part 3 - Line 14 (Balance due)
        AddField(fields, $"{prefix}.f1_20[0]", FormatMoney(data.Line14_BalanceDue));

        // Part 5 - Quarterly breakdown (Lines 16a-16d)
        AddField(fields, $"{prefix}.f1_21[0]", FormatMoney(data.Q1Liability));
        AddField(fields, $"{prefix}.f1_22[0]", FormatMoney(data.Q2Liability));
        AddField(fields, $"{prefix}.f1_23[0]", FormatMoney(data.Q3Liability));
        AddField(fields, $"{prefix}.f1_24[0]", FormatMoney(data.Q4Liability));

        return fields;
    }
}
