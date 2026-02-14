namespace OhioPayroll.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, int entityId,
        string? oldValue = null, string? newValue = null);
}

