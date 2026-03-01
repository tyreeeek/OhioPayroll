using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OhioPayroll.Core.Models;
using OhioPayroll.Core.Models.Enums;
using OhioPayroll.Data;

namespace OhioPayroll.App.Services;

/// <summary>
/// Handles application updates via GitHub releases.
/// Uses UpdateHelper.exe to apply updates after main app exits.
/// </summary>
public class UpdaterService
{
    private readonly PayrollDbContext _db;
    private readonly HttpClient _httpClient;
    private const string GitHubApiUrl = "https://api.github.com/repos/{0}/{1}/releases";

    // TODO: Replace with actual repository owner and name
    private const string RepoOwner = "your-github-username";
    private const string RepoName = "ohio-payroll";

    public UpdaterService(PayrollDbContext db)
    {
        _db = db;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "OhioPayroll-Updater");
    }

    /// <summary>
    /// Checks for available updates from GitHub releases.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool silent = false)
    {
        try
        {
            var settings = await _db.PayrollSettings.FirstOrDefaultAsync();
            if (settings != null)
            {
                // Update last check time
                settings.LastUpdateCheck = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // Get current version
            var currentVersion = GetCurrentVersion();
            if (!silent)
                AppLogger.Information($"Current version: {currentVersion}");

            // Build API URL based on update channel
            var apiUrl = string.Format(GitHubApiUrl, RepoOwner, RepoName);

            if (!silent)
                AppLogger.Information($"Checking for updates from: {apiUrl}");

            // Fetch releases from GitHub
            var releases = await _httpClient.GetFromJsonAsync<GitHubRelease[]>(apiUrl);

            if (releases == null || releases.Length == 0)
            {
                if (!silent)
                    AppLogger.Information("No releases found");
                return null;
            }

            // Filter releases based on update channel
            var filteredReleases = (settings?.UpdateChannel ?? UpdateChannel.Stable) switch
            {
                UpdateChannel.Stable => releases.Where(r => !r.Prerelease && !r.Draft),
                UpdateChannel.Beta => releases.Where(r => !r.Draft),
                UpdateChannel.Internal => releases,
                _ => releases.Where(r => !r.Prerelease && !r.Draft)
            };

            // Get the latest applicable release
            var latestRelease = filteredReleases
                .OrderByDescending(r => r.PublishedAt)
                .FirstOrDefault();

            if (latestRelease == null)
            {
                if (!silent)
                    AppLogger.Information("No applicable releases found for current update channel");
                return null;
            }

            // Parse version from tag (assumes tag format: v1.2.3)
            var latestVersion = latestRelease.TagName.TrimStart('v');

            if (!IsNewerVersion(currentVersion, latestVersion))
            {
                if (!silent)
                    AppLogger.Information($"Already on latest version ({currentVersion})");

                if (settings != null)
                {
                    settings.LastKnownVersion = latestVersion;
                    await _db.SaveChangesAsync();
                }
                return null;
            }

            // Find the appropriate asset (Windows x64 ZIP)
            var asset = latestRelease.Assets
                .FirstOrDefault(a => a.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase) &&
                                    a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                if (!silent)
                    AppLogger.Warning($"Release {latestVersion} found but no Windows x64 package available");
                return null;
            }

            var updateInfo = new UpdateInfo
            {
                Version = latestVersion,
                ReleaseName = latestRelease.Name ?? $"Version {latestVersion}",
                ReleaseNotes = latestRelease.Body ?? "No release notes available",
                PublishedAt = latestRelease.PublishedAt,
                DownloadUrl = asset.BrowserDownloadUrl,
                FileSize = asset.Size,
                IsPrerelease = latestRelease.Prerelease
            };

            if (!silent)
                AppLogger.Information($"Update available: {latestVersion} (current: {currentVersion})");

            if (settings != null)
            {
                settings.LastKnownVersion = latestVersion;
                await _db.SaveChangesAsync();
            }

            return updateInfo;
        }
        catch (HttpRequestException ex)
        {
            if (!silent)
                AppLogger.Error($"Network error checking for updates: {ex.Message}", ex);
            return null;
        }
        catch (Exception ex)
        {
            if (!silent)
                AppLogger.Error($"Error checking for updates: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Downloads the update package to a temporary location.
    /// </summary>
    public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progress = null)
    {
        try
        {
            AppLogger.Information($"Downloading update {updateInfo.Version} from {updateInfo.DownloadUrl}");

            var tempPath = Path.Combine(Path.GetTempPath(), "OhioPayroll_Update", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            var zipPath = Path.Combine(tempPath, "update.zip");

            using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(zipPath);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var progressPercent = (double)downloadedBytes / totalBytes * 100;
                        progress.Report(progressPercent);
                    }
                }
            }

            AppLogger.Information($"Download complete: {zipPath}");

            // TODO: Verify signature/checksum here
            // if (!await VerifyPackageAsync(zipPath, updateInfo.Checksum))
            // {
            //     AppLogger.Error("Package verification failed - aborting update");
            //     Directory.Delete(tempPath, true);
            //     return null;
            // }

            return zipPath;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error downloading update: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Applies the downloaded update by extracting it and launching UpdateHelper.
    /// </summary>
    public async Task<bool> ApplyUpdateAsync(string zipPath)
    {
        try
        {
            var tempDir = Path.GetDirectoryName(zipPath) ?? throw new InvalidOperationException("Invalid zip path");
            var extractPath = Path.Combine(tempDir, "extracted");

            // Remove existing extraction directory if it exists
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, recursive: true);

            // Extract the update package
            AppLogger.Information($"Extracting update to {extractPath}");
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            // Get paths
            var currentAppPath = AppContext.BaseDirectory;
            var helperPath = Path.Combine(extractPath, "UpdateHelper.exe");
            var mainExeName = "OhioPayroll.exe";

            // Verify UpdateHelper exists in the package
            if (!File.Exists(helperPath))
            {
                AppLogger.Error("UpdateHelper.exe not found in update package");
                return false;
            }

            // Log update attempt
            var updateHistory = new UpdateHistory
            {
                FromVersion = GetCurrentVersion(),
                ToVersion = "Unknown", // Will be updated by helper
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = Environment.UserName,
                WasSuccessful = false // Will be updated on next launch if successful
            };
            _db.UpdateHistory.Add(updateHistory);
            await _db.SaveChangesAsync();

            // Launch UpdateHelper with arguments
            AppLogger.Information("Launching UpdateHelper to complete update");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = helperPath,
                    Arguments = $"\"{currentAppPath}\" \"{extractPath}\" \"{mainExeName}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };

            if (process.Start())
            {
                AppLogger.Information("UpdateHelper launched successfully. Exiting main application...");

                // Signal to main app to exit
                // The actual exit will be handled by the calling code
                return true;
            }
            else
            {
                AppLogger.Error("Failed to launch UpdateHelper");
                return false;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error applying update: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    private string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }

    /// <summary>
    /// Compares two semantic version strings.
    /// </summary>
    private bool IsNewerVersion(string currentVersion, string newVersion)
    {
        try
        {
            var current = Version.Parse(currentVersion);
            var latest = Version.Parse(newVersion);
            return latest > current;
        }
        catch
        {
            // If version parsing fails, assume it's newer to be safe
            return true;
        }
    }

    /// <summary>
    /// Verifies the integrity of a downloaded package using SHA256.
    /// </summary>
    private async Task<bool> VerifyPackageAsync(string filePath, string expectedChecksum)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            var actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            if (actualChecksum != expectedChecksum.ToLowerInvariant())
            {
                AppLogger.Warning($"Checksum mismatch! Expected: {expectedChecksum}, Actual: {actualChecksum}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error verifying package: {ex.Message}", ex);
            return false;
        }
    }
}

/// <summary>
/// Information about an available update.
/// </summary>
public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsPrerelease { get; set; }
}

/// <summary>
/// GitHub Release API response model.
/// </summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("assets")]
    public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
}

/// <summary>
/// GitHub Release Asset API response model.
/// </summary>
internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
