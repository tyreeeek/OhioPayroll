using System;
using System.Threading;

namespace OhioPayroll.App.Services;

/// <summary>
/// Manages correlation IDs for tracking payroll operations across async boundaries.
/// Each payroll operation gets a unique correlation ID that flows through all related log entries.
/// </summary>
public class PayrollCorrelationContext : IDisposable
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets the current correlation ID, or "None" if not in a correlated context.
    /// </summary>
    public static string CurrentId => _correlationId.Value ?? "None";

    /// <summary>
    /// Creates a new correlation context with a unique ID.
    /// The ID persists across async/await boundaries within this logical call context.
    /// </summary>
    public PayrollCorrelationContext()
    {
        _correlationId.Value = Guid.NewGuid().ToString("N")[..8]; // Use first 8 chars for brevity
    }

    /// <summary>
    /// Creates a correlation context with a specific ID (for testing or external correlation).
    /// </summary>
    public PayrollCorrelationContext(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    /// <summary>
    /// Clears the correlation ID when the context is disposed.
    /// </summary>
    public void Dispose()
    {
        _correlationId.Value = null;
    }
}
