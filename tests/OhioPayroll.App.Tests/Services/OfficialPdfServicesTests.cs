using OhioPayroll.App.Documents;
using OhioPayroll.App.Services;

namespace OhioPayroll.App.Tests.Services;

public class OfficialPdfServicesTests
{
    [Fact]
    public void RenderW2RecipientPdf_ReturnsOfficialIrsPdf()
    {
        // Arrange
        var data = CreateSampleW2();

        // Act
        var pdfBytes = OfficialW2PdfService.RenderW2RecipientPdf(data);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000); // Official IRS PDF should be substantial

        // Verify PDF signature
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void RenderW2RecipientPdf_ThrowsOnNullData()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            OfficialW2PdfService.RenderW2RecipientPdf(null!));
    }

    [Fact]
    public void Render1099NecRecipientPdf_ReturnsOfficialIrsPdf()
    {
        // Arrange
        var data = CreateSample1099();

        // Act
        var pdfBytes = Official1099NecPdfService.Render1099NecRecipientPdf(data);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);

        // Verify PDF signature
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void Render1099NecRecipientPdf_ThrowsOnNullData()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Official1099NecPdfService.Render1099NecRecipientPdf(null!));
    }

    [Fact]
    public void RenderW3Pdf_ReturnsOfficialIrsPdf()
    {
        // Arrange
        var data = CreateSampleW3();

        // Act
        var pdfBytes = OfficialW3PdfService.RenderW3Pdf(data);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);

        // Verify PDF signature
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void RenderW3Pdf_ThrowsOnNullData()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            OfficialW3PdfService.RenderW3Pdf(null!));
    }

    [Fact]
    public void Render1096Pdf_ReturnsOfficialIrsPdf()
    {
        // Arrange
        var data = CreateSample1096();

        // Act
        var pdfBytes = Official1096PdfService.Render1096Pdf(data);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);

        // Verify PDF signature
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void Render1096Pdf_ThrowsOnNullData()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Official1096PdfService.Render1096Pdf(null!));
    }

    [Fact]
    public void Render940Pdf_ReturnsOfficialIrsPdf()
    {
        // Arrange
        var data = CreateSample940();

        // Act
        var pdfBytes = Official940PdfService.Render940Pdf(data);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);

        // Verify PDF signature
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void Render940Pdf_ThrowsOnNullData()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Official940PdfService.Render940Pdf(null!));
    }

    private static W2Data CreateSampleW2()
    {
        return new W2Data
        {
            EmployeeSsn = "111223333",
            EmployeeFirstName = "John",
            EmployeeLastName = "Doe",
            EmployeeAddress = "456 Oak St",
            EmployeeCity = "Columbus",
            EmployeeState = "OH",
            EmployeeZip = "43215",
            EmployerEin = "987654321",
            EmployerName = "Test Employer Inc",
            EmployerAddress = "789 Elm St",
            EmployerCity = "Columbus",
            EmployerState = "OH",
            EmployerZip = "43215",
            StateWithholdingId = "87654321",
            Box1WagesTips = 50000m,
            Box2FederalTaxWithheld = 7500m,
            Box3SocialSecurityWages = 50000m,
            Box4SocialSecurityTax = 3100m,
            Box5MedicareWages = 50000m,
            Box6MedicareTax = 725m,
            Box16StateWages = 50000m,
            Box17StateTax = 1500m,
            Box18LocalWages = 0m,
            Box19LocalTax = 0m,
            Box20LocalityName = "",
            TaxYear = 2025
        };
    }

    private static Form1099NecData CreateSample1099()
    {
        return new Form1099NecData
        {
            TaxYear = 2025,
            RecipientTin = "111223333",
            RecipientTinIsEin = false,
            RecipientName = "John Doe",
            RecipientAddress = "123 Main St",
            RecipientCity = "Columbus",
            RecipientState = "OH",
            RecipientZip = "43215",
            PayerTin = "987654321",
            PayerName = "Test Company Inc",
            PayerAddress = "456 Oak Ave",
            PayerCity = "Columbus",
            PayerState = "OH",
            PayerZip = "43215",
            PayerPhone = "6145551234",
            Box1_NonemployeeCompensation = 15000m,
            Box4_FederalTaxWithheld = 0m,
            StateCode = "OH",
            StatePayerNo = "87654321",
            StateIncome = 15000m,
            StateTaxWithheld = 0m,
            AccountNumber = ""
        };
    }

    private static W3Data CreateSampleW3()
    {
        return new W3Data
        {
            TaxYear = 2025,
            EmployerEin = "987654321",
            EmployerName = "Test Employer Inc",
            EmployerAddress = "789 Elm St",
            EmployerCity = "Columbus",
            EmployerState = "OH",
            EmployerZip = "43215",
            StateWithholdingId = "87654321",
            NumberOfW2s = 5,
            TotalWages = 250000m,
            TotalFederalTax = 37500m,
            TotalSsWages = 250000m,
            TotalSsTax = 15500m,
            TotalMedicareWages = 250000m,
            TotalMedicareTax = 3625m,
            TotalStateWages = 250000m,
            TotalStateTax = 7500m,
            TotalLocalWages = 0m,
            TotalLocalTax = 0m
        };
    }

    private static Form1096Data CreateSample1096()
    {
        return new Form1096Data
        {
            TaxYear = 2025,
            FilerTin = "987654321",
            FilerName = "Test Company Inc",
            FilerAddress = "456 Oak Ave",
            FilerCity = "Columbus",
            FilerState = "OH",
            FilerZip = "43215",
            ContactName = "Jane Smith",
            ContactPhone = "6145551234",
            ContactEmail = "payroll@testcompany.com",
            Box3_TotalForms = 10,
            Box4_FederalTaxWithheld = 0m,
            Box5_TotalAmount = 150000m,
            FormType = "1099-NEC"
        };
    }

    private static Form940Data CreateSample940()
    {
        return new Form940Data
        {
            TaxYear = 2025,
            EmployerEin = "987654321",
            EmployerName = "Test Employer Inc",
            EmployerAddress = "789 Elm St",
            EmployerCity = "Columbus",
            EmployerState = "OH",
            EmployerZip = "43215",
            Line1a_State = "OH",
            Line3_TotalPayments = 250000m,
            Line4_ExemptPayments = 0m,
            Line5_TaxableFutaWages = 35000m,
            Line6_FutaTaxBeforeAdjustments = 2100m,
            Line7_Adjustments = 0m,
            Line8_TotalFutaTax = 2100m,
            Line12_TotalDeposits = 2100m,
            Line14_BalanceDue = 0m,
            Q1Liability = 525m,
            Q2Liability = 525m,
            Q3Liability = 525m,
            Q4Liability = 525m
        };
    }
}
