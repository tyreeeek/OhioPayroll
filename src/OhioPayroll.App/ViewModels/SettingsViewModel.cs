using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.App.Extensions;
using OhioPayroll.App.Services;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly PayrollDbContext _db;
    private readonly UpdaterService _updaterService;

    // ── Company Info ────────────────────────────────────────────────
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _ein = string.Empty;
    [ObservableProperty] private string _stateWithholdingId = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _state = "OH";
    [ObservableProperty] private string _zipCode = string.Empty;
    [ObservableProperty] private string? _phone;

    // ── Payroll Settings ────────────────────────────────────────────
    [ObservableProperty] private PayFrequency _payFrequency = PayFrequency.BiWeekly;
    [ObservableProperty] private int _currentTaxYear = DateTime.Now.Year;
    [ObservableProperty] private decimal _localTaxRate;
    [ObservableProperty] private decimal _schoolDistrictRate;
    [ObservableProperty] private string? _schoolDistrictCode;
    [ObservableProperty] private decimal _sutaRate = 0.027m;
    [ObservableProperty] private string _backupDirectory = string.Empty;
    [ObservableProperty] private int _nextCheckNumber = 1001;
    [ObservableProperty] private decimal _checkOffsetX;
    [ObservableProperty] private decimal _checkOffsetY;

    // ── Backup ──────────────────────────────────────────────────────
    [ObservableProperty] private string _lastBackupDisplay = "Never";

    // ── UI State ────────────────────────────────────────────────────
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _validationError;

    private int _companyInfoId;
    private int _payrollSettingsId;

    // ── Auto-Updater ────────────────────────────────────────────────
    [ObservableProperty] private bool _autoCheckForUpdates = true;
    [ObservableProperty] private UpdateChannel _selectedUpdateChannel = UpdateChannel.Stable;
    [ObservableProperty] private string _currentVersion = "0.0.0";
    [ObservableProperty] private string _lastUpdateCheckText = "Never";
    [ObservableProperty] private bool _isCheckingForUpdates;
    [ObservableProperty] private UpdateInfo? _availableUpdate;

    public List<PayFrequency> AvailablePayFrequencies { get; } =
        Enum.GetValues<PayFrequency>().ToList();

    public UpdateChannel[] UpdateChannels { get; } = Enum.GetValues<UpdateChannel>();

    public SettingsViewModel(PayrollDbContext db, UpdaterService updaterService)
    {
        _db = db;
        _updaterService = updaterService;

        // Get current version from assembly
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        CurrentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";

        LoadDataAsync().FireAndForgetSafeAsync(errorContext: "loading settings");
    }

    private string GetBackupDirectory()
    {
        if (!string.IsNullOrWhiteSpace(BackupDirectory))
            return BackupDirectory;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "OhioPayroll", "Backups");
    }

    private string GetDbFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OhioPayroll", "payroll.db");
    }

    private void LoadLastBackupTime()
    {
        try
        {
            var backupService = new BackupService(GetDbFilePath(), GetBackupDirectory());
            var lastBackup = backupService.GetLastBackupTime();
            LastBackupDisplay = lastBackup.HasValue
                ? lastBackup.Value.ToString("yyyy-MM-dd hh:mm tt")
                : "Never";
        }
        catch
        {
            LastBackupDisplay = "Never";
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading settings...";

            var company = await _db.CompanyInfo.FirstOrDefaultAsync();
            if (company is not null)
            {
                _companyInfoId = company.Id;
                CompanyName = company.CompanyName;
                Ein = company.Ein;
                StateWithholdingId = company.StateWithholdingId;
                Address = company.Address;
                City = company.City;
                State = company.State;
                ZipCode = company.ZipCode;
                Phone = company.Phone;
            }

            var settings = await _db.PayrollSettings.FirstOrDefaultAsync();
            if (settings is not null)
            {
                _payrollSettingsId = settings.Id;
                PayFrequency = settings.PayFrequency;
                CurrentTaxYear = settings.CurrentTaxYear;
                LocalTaxRate = settings.LocalTaxRate;
                SchoolDistrictRate = settings.SchoolDistrictRate;
                SchoolDistrictCode = settings.SchoolDistrictCode;
                SutaRate = settings.SutaRate;
                BackupDirectory = settings.BackupDirectory;
                NextCheckNumber = settings.NextCheckNumber;
                CheckOffsetX = settings.CheckOffsetX;
                CheckOffsetY = settings.CheckOffsetY;

                // Update settings
                AutoCheckForUpdates = settings.AutoCheckForUpdates;
                SelectedUpdateChannel = settings.UpdateChannel;
                if (settings.LastUpdateCheck.HasValue)
                    LastUpdateCheckText = $"Last checked: {settings.LastUpdateCheck.Value:g}";
            }

            LoadLastBackupTime();

            HasChanges = false;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // ── Validation ──────────────────────────────────────────
        ValidationError = null;

        if (!string.IsNullOrWhiteSpace(Ein))
        {
            if (!Regex.IsMatch(Ein.Trim(), @"^\d{2}-\d{7}$"))
            {
                ValidationError = "EIN must be in the format XX-XXXXXXX (2 digits, dash, 7 digits).";
                StatusMessage = ValidationError;
                return;
            }
        }

        if (SutaRate < 0m || SutaRate > 0.15m)
        {
            ValidationError = "SUTA rate must be between 0 and 0.15 (15%).";
            StatusMessage = ValidationError;
            return;
        }

        if (LocalTaxRate < 0m || LocalTaxRate > 0.05m)
        {
            ValidationError = "Local tax rate must be between 0 and 0.05 (5%).";
            StatusMessage = ValidationError;
            return;
        }

        if (SchoolDistrictRate < 0m || SchoolDistrictRate > 0.05m)
        {
            ValidationError = "School district rate must be between 0 and 0.05 (5%).";
            StatusMessage = ValidationError;
            return;
        }

        // Use ExecuteWithLoadingAsync for save operation
        var success = await ExecuteWithLoadingAsync(SaveInternalAsync, "Saving settings...");

        if (success)
        {
            StatusMessage = "Settings saved successfully.";
        }
    }

    private async Task SaveInternalAsync()
    {
        // ── Company Info ────────────────────────────────────────
        var company = await _db.CompanyInfo.FindAsync(_companyInfoId);
        if (company is null)
        {
            company = new CompanyInfo();
            _db.CompanyInfo.Add(company);
        }

        company.CompanyName = CompanyName;
        company.Ein = Ein;
        company.StateWithholdingId = StateWithholdingId;
        company.Address = Address;
        company.City = City;
        company.State = State;
        company.ZipCode = ZipCode;
        company.Phone = Phone;
        company.UpdatedAt = DateTime.Now;

        // ── Payroll Settings ────────────────────────────────────
        var settings = await _db.PayrollSettings.FindAsync(_payrollSettingsId);
        if (settings is null)
        {
            settings = new PayrollSettings();
            _db.PayrollSettings.Add(settings);
        }

        settings.PayFrequency = PayFrequency;
        settings.CurrentTaxYear = CurrentTaxYear;
        settings.LocalTaxRate = LocalTaxRate;
        settings.SchoolDistrictRate = SchoolDistrictRate;
        settings.SchoolDistrictCode = SchoolDistrictCode;
        settings.SutaRate = SutaRate;
        settings.BackupDirectory = BackupDirectory;
        settings.NextCheckNumber = NextCheckNumber;
        settings.CheckOffsetX = CheckOffsetX;
        settings.CheckOffsetY = CheckOffsetY;
        settings.AutoCheckForUpdates = AutoCheckForUpdates;
        settings.UpdateChannel = SelectedUpdateChannel;
        settings.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        // Capture the generated IDs for new records
        _companyInfoId = company.Id;
        _payrollSettingsId = settings.Id;

        HasChanges = false;
        AppLogger.Information("Settings saved successfully");
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        try
        {
            StatusMessage = "Creating backup...";
            var backupService = new BackupService(GetDbFilePath(), GetBackupDirectory());

            await Task.Run(() =>
            {
                var backupPath = backupService.CreateBackup();
                var verified = backupService.VerifyBackup(backupPath);

                if (verified)
                {
                    AppLogger.Information($"Manual backup created and verified: {Path.GetFileName(backupPath)}");
                    backupService.PruneOldBackups(30);
                }
                else
                {
                    AppLogger.Warning($"Manual backup created but verification failed: {backupPath}");
                }
            });

            LoadLastBackupTime();
            StatusMessage = "Backup created successfully.";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error creating backup: {ex.Message}", ex);
            StatusMessage = $"Error creating backup: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenBackupFolderAsync()
    {
        try
        {
            var backupDir = GetBackupDirectory();
            Directory.CreateDirectory(backupDir);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", backupDir) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo("open", backupDir) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo("xdg-open", backupDir) { UseShellExecute = true });
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening backup folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            IsCheckingForUpdates = true;
            StatusMessage = "Checking for updates...";
            AvailableUpdate = null;

            var updateInfo = await _updaterService.CheckForUpdatesAsync(silent: false);

            if (updateInfo != null)
            {
                AvailableUpdate = updateInfo;
                StatusMessage = $"Update available: Version {updateInfo.Version}";
                LastUpdateCheckText = $"Last checked: {DateTime.Now:g} - Update available!";
            }
            else
            {
                StatusMessage = "You are running the latest version";
                LastUpdateCheckText = $"Last checked: {DateTime.Now:g}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error checking for updates: {ex.Message}";
            AppLogger.Error($"Error checking for updates: {ex.Message}", ex);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    /// <summary>
    /// Called by generated property setters via partial methods to track changes.
    /// </summary>
    partial void OnCompanyNameChanged(string value) => HasChanges = true;
    partial void OnEinChanged(string value) => HasChanges = true;
    partial void OnStateWithholdingIdChanged(string value) => HasChanges = true;
    partial void OnAddressChanged(string value) => HasChanges = true;
    partial void OnCityChanged(string value) => HasChanges = true;
    partial void OnStateChanged(string value) => HasChanges = true;
    partial void OnZipCodeChanged(string value) => HasChanges = true;
    partial void OnPhoneChanged(string? value) => HasChanges = true;
    partial void OnPayFrequencyChanged(PayFrequency value) => HasChanges = true;
    partial void OnCurrentTaxYearChanged(int value) => HasChanges = true;
    partial void OnLocalTaxRateChanged(decimal value) => HasChanges = true;
    partial void OnSchoolDistrictRateChanged(decimal value) => HasChanges = true;
    partial void OnSchoolDistrictCodeChanged(string? value) => HasChanges = true;
    partial void OnSutaRateChanged(decimal value) => HasChanges = true;
    partial void OnBackupDirectoryChanged(string value) => HasChanges = true;
    partial void OnNextCheckNumberChanged(int value) => HasChanges = true;
    partial void OnCheckOffsetXChanged(decimal value) => HasChanges = true;
    partial void OnCheckOffsetYChanged(decimal value) => HasChanges = true;
    partial void OnAutoCheckForUpdatesChanged(bool value) => HasChanges = true;
    partial void OnSelectedUpdateChannelChanged(UpdateChannel value) => HasChanges = true;
}

