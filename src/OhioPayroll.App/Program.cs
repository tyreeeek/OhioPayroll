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
            // CRITICAL: Backups include encryption keys which are required to decrypt
            // SSNs, TINs, and bank account numbers. Without key backups, this data is
            // permanently lost if the system fails.
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

                // Log critical warning if no backup has ever been created
                if (lastBackup is null)
                {
                    AppLogger.Warning(
                        "CRITICAL: No backups found! Your encryption keys and payroll data are not backed up. " +
                        $"If this machine fails, encrypted data (SSNs, bank accounts) will be PERMANENTLY LOST. " +
                        $"Backup location: {backupDir}");
                }
                else if ((DateTime.Now - lastBackup.Value).TotalDays > 30)
                {
                    AppLogger.Warning(
                        $"WARNING: Last backup is {(int)(DateTime.Now - lastBackup.Value).TotalDays} days old. " +
                        "Consider creating a more recent backup to protect your payroll data and encryption keys.");
                }

                if (lastBackup is null || (DateTime.Now - lastBackup.Value).TotalDays > 7)
                {
                    AppLogger.Information("Auto-backup triggered (last backup: " +
                        (lastBackup.HasValue ? lastBackup.Value.ToString("yyyy-MM-dd HH:mm") : "never") + ")");
                    var backupPath = backupService.CreateBackup();
                    if (backupService.VerifyBackup(backupPath))
                    {
                        AppLogger.Information("Auto-backup created and verified: " + Path.GetFileName(backupPath));
                        AppLogger.Information($"Backup includes encryption keys. Location: {backupDir}");
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
                AppLogger.Error("Auto-backup failed - CRITICAL: Encryption keys may not be backed up!", ex);
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
        // Enable WAL mode for better concurrency and add command timeout
        var connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared;Pooling=True";

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
        services.AddTransient<ContractorPayrollService>();
        services.AddTransient<UpdaterService>();

        // ViewModels (transient so each navigation creates a fresh instance)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<EmployeeListViewModel>();
        services.AddTransient<ContractorListViewModel>();
        services.AddTransient<PayrollRunViewModel>();
        services.AddTransient<ContractorPayrollViewModel>();
        services.AddTransient<BankAccountsViewModel>();
        services.AddTransient<CheckPrintingViewModel>();
        services.AddTransient<DirectDepositViewModel>();
        services.AddTransient<QuarterlyViewModel>();
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

        // ═══════════════════════════════════════════════════════════════════════════════
        // CRITICAL: ENCRYPTION KEY BACKUP REQUIREMENTS
        // ═══════════════════════════════════════════════════════════════════════════════
        //
        // BOTH of the following paths MUST be backed up together:
        //   1. Key file:        {LocalAppData}/OhioPayroll/.enckey
        //   2. DataProtection:  {LocalAppData}/OhioPayroll/dp-keys/
        //
        // WHAT IS ENCRYPTED:
        //   • Employee Social Security Numbers (SSNs)
        //   • Contractor Tax Identification Numbers (TINs)
        //   • Bank account numbers and routing numbers
        //
        // IF KEYS ARE LOST:
        //   • All encrypted data becomes PERMANENTLY UNRECOVERABLE
        //   • You will need to re-enter all SSNs, TINs, and bank account information
        //   • Historical W-2/1099 generation will fail for affected records
        //
        // BACKUP RECOMMENDATIONS:
        //   1. Include these paths in your regular backup routine
        //   2. Store backup copies in a secure, separate location
        //   3. Test key restoration on a separate machine periodically
        //   4. The BackupService automatically includes keys when creating backups
        //
        // PLATFORM PATHS:
        //   Windows: C:\Users\{user}\AppData\Local\OhioPayroll\
        //   macOS:   ~/Library/Application Support/OhioPayroll/
        //   Linux:   ~/.local/share/OhioPayroll/
        // ═══════════════════════════════════════════════════════════════════════════════

        var keyFilePath = Path.Combine(appDataDir, ".enckey");
        var keysDir = Path.Combine(appDataDir, "dp-keys");

        try
        {
            // Build a DataProtection provider that persists keys to the app data directory
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection()
                .SetApplicationName("OhioPayroll")
                .PersistKeysToFileSystem(new DirectoryInfo(keysDir));
            using var services = serviceCollection.BuildServiceProvider();
            var protector = services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("EncryptionKey");

            if (File.Exists(keyFilePath))
            {
                try
                {
                    var protectedKey = File.ReadAllText(keyFilePath);
                    var keyBytes = Convert.FromBase64String(protector.Unprotect(protectedKey));
                    return keyBytes;
                }
                catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException
                    or FormatException or IOException)
                {
                    AppLogger.Error(
                        $"Encryption key file is corrupted or unreadable at '{keyFilePath}': {ex.Message}. " +
                        "Renaming corrupted file and generating a new key. " +
                        "WARNING: Previously encrypted data will be unreadable.", ex);

                    // Rename corrupted key file so a new one can be generated
                    var corruptedPath = keyFilePath + $".corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                    try { File.Move(keyFilePath, corruptedPath); }
                    catch { /* Best effort rename */ }
                }
            }

            // Generate a new encryption key (first-time setup or recovery from corruption)
            AppLogger.Information(
                $"Generating new encryption key. Key file: '{keyFilePath}', DataProtection keys: '{keysDir}'");
            var key = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(key);
            var newProtectedKey = protector.Protect(Convert.ToBase64String(key));
            File.WriteAllText(keyFilePath, newProtectedKey);
            return key;
        }
        catch (Exception ex) when (ex is FileNotFoundException
            or System.Security.Cryptography.CryptographicException or FormatException or IOException)
        {
            AppLogger.Error(
                $"Fatal error deriving encryption key. Key file: '{keyFilePath}', Keys dir: '{keysDir}'. " +
                "To recover: restore backups of both the .enckey file and the dp-keys directory, " +
                "then restart the application. Contact support if backups are unavailable.", ex);
            throw;
        }
    }
}

