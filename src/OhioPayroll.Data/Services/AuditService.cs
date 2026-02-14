using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models;

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
}

