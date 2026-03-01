using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;
using System.IO;

namespace OhioPayroll.Data.Services;

public class AuditService : IAuditService
{
    private readonly PayrollDbContext _db;

    public AuditService(PayrollDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string action, string entityType, int entityId,
        string? oldValue = null, string? newValue = null, string actor = "System")
    {
        try
        {
            _db.AuditLog.Add(new AuditLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                Actor = actor
            });
            await _db.SaveChangesAsync();
        }
#pragma warning disable CA1031 // Intentionally catch all exceptions - audit must never block payroll
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Audit must NEVER block payroll
            // Log to separate file sink for traceability
            try
            {
                var auditFailureLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit_failures.log");
                var logEntry = $"{DateTime.UtcNow:O}|AUDIT_FAILURE|{action}|{entityType}|{entityId}|{ex.Message}\n";
                await File.AppendAllTextAsync(auditFailureLog, logEntry);
            }
#pragma warning disable CA1031 // Intentionally catch all exceptions - even file logging failure must not block
            catch
#pragma warning restore CA1031
            {
                // If even file logging fails, silently continue - payroll must not be blocked
            }
        }
    }
}

