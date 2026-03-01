using System;
using System.Threading;
using Serilog;

namespace OhioPayroll.App.Services;

/// <summary>
/// Simple static logging helper that wraps the Serilog logger initialized in Program.cs.
/// Automatically includes correlation IDs for tracing payroll operations.
/// </summary>
public static class AppLogger
{
    private static volatile ILogger? _logger;

    /// <summary>
    /// Initialize the logger. Called once from Program.Main.
    /// Uses atomic compare-and-exchange for thread-safe one-time initialization.
    /// </summary>
    public static void Initialize(ILogger logger)
    {
        Interlocked.CompareExchange(ref _logger, logger, null);
    }

    public static void Information(string message)
    {
        if (_logger == null) { Console.Error.WriteLine($"[LOG NOT INITIALIZED] {message}"); return; }
        var correlatedMessage = $"[{PayrollCorrelationContext.CurrentId}] {message}";
        _logger.Information(correlatedMessage);
    }

    public static void Warning(string message)
    {
        if (_logger == null) { Console.Error.WriteLine($"[LOG NOT INITIALIZED] {message}"); return; }
        var correlatedMessage = $"[{PayrollCorrelationContext.CurrentId}] {message}";
        _logger.Warning(correlatedMessage);
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (_logger == null) { Console.Error.WriteLine($"[LOG NOT INITIALIZED] {message} {ex}"); return; }
        var correlatedMessage = $"[{PayrollCorrelationContext.CurrentId}] {message}";
        if (ex is not null)
            _logger.Error(ex, correlatedMessage);
        else
            _logger.Error(correlatedMessage);
    }
}

