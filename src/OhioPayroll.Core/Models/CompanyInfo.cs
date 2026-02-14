namespace OhioPayroll.Core.Models;

public class CompanyInfo
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Ein { get; set; } = string.Empty;
    public string StateWithholdingId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = "OH";
    public string ZipCode { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime UpdatedAt { get; set; }
}

