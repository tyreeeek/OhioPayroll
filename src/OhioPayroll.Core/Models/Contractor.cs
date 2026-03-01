using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class Contractor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string EncryptedTin { get; set; } = string.Empty;
    public string TinLast4 { get; set; } = string.Empty;
    public bool IsEin { get; set; }
    public ContractorBusinessType BusinessType { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = "OH";
    public string ZipCode { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool Is1099Exempt { get; set; }

    // Rate fields
    public decimal? HourlyRate { get; set; }
    public decimal? DailyRate { get; set; }
    public ContractorRateType RateType { get; set; } = ContractorRateType.Flat;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ContractorPayment> Payments { get; set; } = new List<ContractorPayment>();

    // Validation property
    public bool HasValidRate => RateType switch
    {
        ContractorRateType.Hourly => HourlyRate.HasValue && HourlyRate.Value > 0,
        ContractorRateType.Daily => DailyRate.HasValue && DailyRate.Value > 0,
        ContractorRateType.Flat => true,
        _ => false
    };
}
