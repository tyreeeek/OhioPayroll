using Avalonia;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OhioPayroll.Core.Interfaces;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;
using OhioPayroll.Data.Services;
using OhioPayroll.Engine.Calculators;
using OhioPayroll.Engine.TaxTables;
using OhioPayroll.App.ViewModels;
using OhioPayroll.App.Services;
using Serilog;

namespace OhioPayroll.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // ── Initialize Serilog ──────────────────────────────────────────
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OhioPayroll", "logs");
        Directory.CreateDirectory(logDir);

        var logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(logDir, "payroll-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        AppLogger.Initialize(logger);
        AppLogger.Information("OhioPayroll application starting");

        try
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            App.Services = InitializeServices();

            // ── Auto-backup on startup ──────────────────────────────────
            try
            {
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OhioPayroll");
                var dbPath = Path.Combine(appDataDir, "payroll.db");
                var backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "OhioPayroll", "Backups");

                var backupService = new BackupService(dbPath, backupDir);
                var lastBackup = backupService.GetLastBackupTime();

                if (lastBackup is null || (DateTime.Now - lastBackup.Value).TotalDays > 7)
                {
                    AppLogger.Information("Auto-backup triggered (last backup: " +
                        (lastBackup.HasValue ? lastBackup.Value.ToString("yyyy-MM-dd HH:mm") : "never") + ")");
                    var backupPath = backupService.CreateBackup();
                    if (backupService.VerifyBackup(backupPath))
                    {
                        AppLogger.Information("Auto-backup created and verified: " + Path.GetFileName(backupPath));
                        backupService.PruneOldBackups(30);
                    }
                    else
                    {
                        AppLogger.Warning("Auto-backup created but verification failed: " + backupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Auto-backup failed", ex);
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppLogger.Information("OhioPayroll application shutting down");
            logger.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static IServiceProvider InitializeServices()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OhioPayroll");
        Directory.CreateDirectory(appDataDir);
        var dbPath = Path.Combine(appDataDir, "payroll.db");
        var connectionString = $"Data Source={dbPath}";

        // Initialize database (migrations + seed data)
        var optionsBuilder = new DbContextOptionsBuilder<PayrollDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        try
        {
            using (var db = new PayrollDbContext(optionsBuilder.Options))
            {
                new DatabaseInitializer(db).InitializeAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Database initialization failed", ex);
            throw;
        }

        // Load tax brackets and build calculation engine
        FederalTaxCalculator federalCalc;
        OhioStateTaxCalculator ohioCalc;

        using (var db = new PayrollDbContext(optionsBuilder.Options))
        {
            var settings = db.PayrollSettings.First();
            int taxYear = settings.CurrentTaxYear;

            var allTables = db.TaxTables
                .Where(t => t.TaxYear == taxYear)
                .ToList();

            var fedSingle = allTables
                .Where(t => t.Type == TaxType.Federal && t.FilingStatus == FilingStatus.Single)
                .Select(t => new TaxBracket
                {
                    BracketStart = t.BracketStart,
                    BracketEnd = t.BracketEnd,
                    Rate = t.Rate,
                    BaseAmount = t.BaseAmount
                }).ToList();

            var fedMarried = allTables
                .Where(t => t.Type == TaxType.Federal && t.FilingStatus == FilingStatus.Married)
                .Select(t => new TaxBracket
                {
                    BracketStart = t.BracketStart,
                    BracketEnd = t.BracketEnd,
                    Rate = t.Rate,
                    BaseAmount = t.BaseAmount
                }).ToList();

            var fedHoh = allTables
                .Where(t => t.Type == TaxType.Federal && t.FilingStatus == FilingStatus.HeadOfHousehold)
                .Select(t => new TaxBracket
                {
                    BracketStart = t.BracketStart,
                    BracketEnd = t.BracketEnd,
                    Rate = t.Rate,
                    BaseAmount = t.BaseAmount
                }).ToList();

            var ohioBrackets = allTables
                .Where(t => t.Type == TaxType.Ohio && t.FilingStatus == FilingStatus.Single)
                .Select(t => new TaxBracket
                {
                    BracketStart = t.BracketStart,
                    BracketEnd = t.BracketEnd,
                    Rate = t.Rate,
                    BaseAmount = t.BaseAmount
                }).ToList();

            federalCalc = new FederalTaxCalculator(fedSingle, fedMarried, fedHoh);
            ohioCalc = new OhioStateTaxCalculator(ohioBrackets);
        }

        var engine = new PayrollCalculationEngine(federalCalc, ohioCalc);

        // Build DI container
        var services = new ServiceCollection();

        services.AddDbContext<PayrollDbContext>(
            options => options.UseSqlite(connectionString),
            ServiceLifetime.Transient);

        var encKey = DeriveEncryptionKey();
        services.AddSingleton<IEncryptionService>(new EncryptionService(encKey));
        services.AddTransient<IAuditService, AuditService>();
        services.AddSingleton<IPayrollCalculationEngine>(engine);

        // ViewModels (transient so each navigation creates a fresh instance)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<EmployeeListViewModel>();
        services.AddTransient<PayrollRunViewModel>();
        services.AddTransient<BankAccountsViewModel>();
        services.AddTransient<CheckPrintingViewModel>();
        services.AddTransient<DirectDepositViewModel>();
        services.AddTransient<TaxLiabilityViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<YearEndViewModel>();

        return services.BuildServiceProvider();
    }

    private static byte[] DeriveEncryptionKey()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OhioPayroll");
        Directory.CreateDirectory(appDataDir);
        var keyFilePath = Path.Combine(appDataDir, ".enckey");

        // Build a DataProtection provider that persists keys to the app data directory
        var keysDir = Path.Combine(appDataDir, "dp-keys");
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection()
            .SetApplicationName("OhioPayroll")
            .PersistKeysToFileSystem(new DirectoryInfo(keysDir));
        using var services = serviceCollection.BuildServiceProvider();
        var protector = services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("EncryptionKey");

        if (File.Exists(keyFilePath))
        {
            var protectedKey = File.ReadAllText(keyFilePath);
            var keyBytes = Convert.FromBase64String(protector.Unprotect(protectedKey));
            return keyBytes;
        }
        else
        {
            var key = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(key);
            var protectedKey = protector.Protect(Convert.ToBase64String(key));
            File.WriteAllText(keyFilePath, protectedKey);
            return key;
        }
    }
}

