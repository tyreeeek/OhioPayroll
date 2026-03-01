namespace OhioPayroll.Core.Models;

public class UpdateHistory
{
    public int Id { get; set; }
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = Environment.UserName;
    public bool WasSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
}
