using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OhioPayroll.App.ViewModels;
using OhioPayroll.App.Views;
using System;
using System.IO;
using System.Threading.Tasks;
using OhioPayroll.App.Services;

namespace OhioPayroll.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    // ── Global Exception Handlers ─────────────────────────────────────────

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogCriticalException("Unhandled", ex);

        // Attempt graceful shutdown
        if (e.IsTerminating)
        {
            SaveCrashReport(ex);
            Environment.Exit(1);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCriticalException("UnobservedTask", e.Exception);
        e.SetObserved(); // Prevent crash, log only
    }

    private void LogCriticalException(string source, Exception? ex)
    {
        if (ex == null) return;

        AppLogger.Error($"CRITICAL EXCEPTION [{source}]: {ex.Message}", ex);

        // Separate crash log
        try
        {
            var crashLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crashes.log");
            var logEntry = $"{DateTime.UtcNow:O}|{source}|{ex.GetType().Name}|{ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(crashLog, logEntry);
        }
        catch
        {
            // If crash logging fails, continue - don't make it worse
        }
    }

    private void SaveCrashReport(Exception? ex)
    {
        if (ex == null) return;

        try
        {
            var crashReportPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"crash_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");

            var report = $@"OHIO PAYROLL - CRASH REPORT
Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

Exception Type: {ex.GetType().FullName}
Message: {ex.Message}

Stack Trace:
{ex.StackTrace}

Inner Exception:
{ex.InnerException?.Message ?? "None"}
{ex.InnerException?.StackTrace ?? ""}
";

            File.WriteAllText(crashReportPath, report);
            AppLogger.Error($"Crash report saved to: {crashReportPath}");
        }
        catch
        {
            // If crash report fails, nothing we can do
        }
    }
}

