using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OhioPayroll.App.Services;

/// <summary>
/// Shared platform-specific utilities.
/// </summary>
public static class PlatformHelper
{
    /// <summary>
    /// Opens the specified folder in the OS file explorer.
    /// Best-effort: silently catches any failure.
    /// </summary>
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AppLogger.Warning("OpenFolder called with null or empty path — ignoring.");
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", path)
                {
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo("open", path)
                {
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo("xdg-open", path)
                {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            // Sanitize path to avoid logging user-identifiable directory information
            var redactedPath = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(redactedPath))
            {
                redactedPath = "[folder]";
            }
            AppLogger.Warning($"Failed to open folder '{redactedPath}': {ex.Message}");
        }
    }
}
