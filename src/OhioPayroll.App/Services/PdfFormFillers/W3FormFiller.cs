using OhioPayroll.App.Documents;

namespace OhioPayroll.App.Services.PdfFormFillers;

/// <summary>
/// Fills IRS Form W-3 (Transmittal of Wage and Tax Statements) PDF.
/// </summary>
public class W3FormFiller : IrsFormFillerBase<W3Data>
{
    protected override string EmbeddedResourceName => "OhioPayroll.App.Assets.IrsForms.fw3.pdf";

    protected override Dictionary<string, string> MapDataToFields(W3Data data)
    {
        var fields = new Dictionary<string, string>();

        var prefix = "topmostSubform[0].Page1[0]";

        // Box b - Employer Identification Number (EIN)
        AddField(fields, $"{prefix}.f1_1[0]", FormatTin(data.EmployerEin));

        // Box c - Employer's name
        AddField(fields, $"{prefix}.f1_2[0]", data.EmployerName);

        // Box d - Control number (optional)
        // AddField(fields, $"{prefix}.f1_3[0]", "");

        // Box e - Employer's address
        AddField(fields, $"{prefix}.f1_4[0]", data.EmployerAddress);

        // Box f - Employer's city, state, ZIP
        AddField(fields, $"{prefix}.f1_5[0]", $"{data.EmployerCity}, {data.EmployerState} {data.EmployerZip}");

        // Box 1 - Wages, tips, other compensation
        AddField(fields, $"{prefix}.f1_6[0]", FormatMoney(data.TotalWages));

        // Box 2 - Federal income tax withheld
        AddField(fields, $"{prefix}.f1_7[0]", FormatMoney(data.TotalFederalTax));

        // Box 3 - Social security wages
        AddField(fields, $"{prefix}.f1_8[0]", FormatMoney(data.TotalSsWages));

        // Box 4 - Social security tax withheld
        AddField(fields, $"{prefix}.f1_9[0]", FormatMoney(data.TotalSsTax));

        // Box 5 - Medicare wages and tips
        AddField(fields, $"{prefix}.f1_10[0]", FormatMoney(data.TotalMedicareWages));

        // Box 6 - Medicare tax withheld
        AddField(fields, $"{prefix}.f1_11[0]", FormatMoney(data.TotalMedicareTax));

        // Box 7 - Social security tips (not commonly used)
        // AddField(fields, $"{prefix}.f1_12[0]", FormatMoney(0m));

        // Box 8 - Allocated tips (not commonly used)
        // AddField(fields, $"{prefix}.f1_13[0]", FormatMoney(0m));

        // Box 10 - Dependent care benefits (not commonly used)
        // AddField(fields, $"{prefix}.f1_14[0]", FormatMoney(0m));

        // Box 15 - State/Employer's state ID number
        AddField(fields, $"{prefix}.f1_15[0]", data.StateWithholdingId);

        // Box 16 - State wages, tips, etc.
        AddField(fields, $"{prefix}.f1_16[0]", FormatMoney(data.TotalStateWages));

        // Box 17 - State income tax
        AddField(fields, $"{prefix}.f1_17[0]", FormatMoney(data.TotalStateTax));

        // Box 18 - Local wages, tips, etc.
        if (data.TotalLocalWages > 0)
            AddField(fields, $"{prefix}.f1_18[0]", FormatMoney(data.TotalLocalWages));

        // Box 19 - Local income tax
        if (data.TotalLocalTax > 0)
            AddField(fields, $"{prefix}.f1_19[0]", FormatMoney(data.TotalLocalTax));

        return fields;
    }
}
