using OhioPayroll.Core.Models.Enums;

namespace OhioPayroll.Core.Models;

public class PayrollSettings
{
    public int Id { get; set; }
    public PayFrequency PayFrequency { get; set; } = PayFrequency.BiWeekly;
    public int CurrentTaxYear { get; set; } = DateTime.Now.Year;
    public decimal LocalTaxRate { get; set; }
    public decimal SchoolDistrictRate { get; set; }
    public string? SchoolDistrictCode { get; set; }
    public decimal SutaRate { get; set; } = 0.027m;
    public string BackupDirectory { get; set; } = string.Empty;
    public int NextCheckNumber { get; set; } = 1001;
    public decimal CheckOffsetX { get; set; }
    public decimal CheckOffsetY { get; set; }

    // Auto-updater settings
    public bool AutoCheckForUpdates { get; set; } = true;
    public DateTime? LastUpdateCheck { get; set; }
    public string? LastKnownVersion { get; set; }
    public string? UpdateChannelUrl { get; set; }  // Configurable (see UpdateChannel enum)
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Stable;
    public DateTime? ContractorPayrollLockDate { get; set; }  // Optional: prevent payroll before date

    public DateTime UpdatedAt { get; set; }
}

