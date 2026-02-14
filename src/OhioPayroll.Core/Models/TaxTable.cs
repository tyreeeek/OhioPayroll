using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class TaxTable
{
    public int Id { get; set; }
    public int TaxYear { get; set; }
    public TaxType Type { get; set; }
    public FilingStatus FilingStatus { get; set; }
    public decimal BracketStart { get; set; }
    public decimal BracketEnd { get; set; }
    public decimal Rate { get; set; }
    public decimal BaseAmount { get; set; }
    public string? DistrictCode { get; set; }
}
