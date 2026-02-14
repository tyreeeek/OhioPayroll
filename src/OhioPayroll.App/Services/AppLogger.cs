using System;
using Serilog;

namespace OhioPayroll.App.Services;

/// <summary>
/// Simple static logging helper that wraps the Serilog logger initialized in Program.cs.
/// </summary>
public static class AppLogger
{
    private static volatile ILogger? _logger;

    /// <summary>
    /// Initialize the logger. Called once from Program.Main.
    /// </summary>
    public static void Initialize(ILogger logger)
    {
        if (_logger != null) return;
        _logger = logger;
    }

    public static void Information(string message)
    {
        if (_logger == null) { Console.Error.WriteLine($"[LOG NOT INITIALIZED] {message}"); return; }
        _logger.Information(message);
    }

    public static void Warning(string message)
    {
        if (_logger == null) { Console.Error.WriteLine($"[LOG NOT INITIALIZED] {message}"); return; }
        _logger.Warning(message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (_logger == null) { Console.Error.WriteLine($"[LOG NOT INITIALIZED] {message} {ex}"); return; }
        if (ex is not null)
            _logger.Error(ex, message);
        else
            _logger.Error(message);
    }
}

