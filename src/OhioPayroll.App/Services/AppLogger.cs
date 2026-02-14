using System;
using Serilog;

namespace OhioPayroll.App.Services;

/// <summary>
/// Simple static logging helper that wraps the Serilog logger initialized in Program.cs.
/// </summary>
public static class AppLogger
{
    private static ILogger? _logger;

    /// <summary>
    /// Initialize the logger. Called once from Program.Main.
    /// </summary>
    public static void Initialize(ILogger logger)
    {
        _logger = logger;
    }

    public static void Information(string message)
    {
        _logger?.Information(message);
    }

    public static void Warning(string message)
    {
        _logger?.Warning(message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (ex is not null)
            _logger?.Error(ex, message);
        else
            _logger?.Error(message);
    }
}

