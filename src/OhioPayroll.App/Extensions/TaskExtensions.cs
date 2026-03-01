using System;
using System.Threading.Tasks;
using OhioPayroll.App.Services;

namespace OhioPayroll.App.Extensions;

/// <summary>
/// Extension methods for safe asynchronous task execution.
/// Provides fire-and-forget functionality with proper exception handling and logging.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Safely executes a task in a fire-and-forget manner with exception handling.
    /// All exceptions are logged via AppLogger and optionally handled by a custom callback.
    /// </summary>
    /// <param name="task">The task to execute</param>
    /// <param name="onException">Optional custom exception handler</param>
    /// <param name="errorContext">Optional context for error messages (e.g., "loading employees")</param>
    public static async void FireAndForgetSafeAsync(
        this Task task,
        Action<Exception>? onException = null,
        string? errorContext = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            var context = errorContext ?? "background operation";
            AppLogger.Error($"Fire-and-forget task failed during {context}: {ex.Message}", ex);
            onException?.Invoke(ex);
        }
    }
}
