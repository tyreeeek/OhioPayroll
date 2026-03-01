using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OhioPayroll.UpdateHelper;

/// <summary>
/// Standalone executable that updates the main application.
/// This runs AFTER the main app exits to replace locked files.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Ohio Payroll Update Helper");
        Console.WriteLine("===========================");

        if (args.Length < 3)
        {
            Console.WriteLine("ERROR: Invalid arguments");
            Console.WriteLine("Usage: UpdateHelper.exe <targetPath> <updatePath> <executableName>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  UpdateHelper.exe \"C:\\Program Files\\OhioPayroll\" \"C:\\Temp\\update\" \"OhioPayroll.exe\"");
            return 1;
        }

        string targetPath = args[0];
        string updatePath = args[1];
        string executableName = args[2];

        Console.WriteLine($"Target Path: {targetPath}");
        Console.WriteLine($"Update Path: {updatePath}");
        Console.WriteLine($"Executable: {executableName}");
        Console.WriteLine();

        try
        {
            // Step 1: Wait for main process to exit (give it up to 30 seconds)
            Console.WriteLine("Waiting for main application to exit...");
            var mainExePath = Path.Combine(targetPath, executableName);

            for (int i = 0; i < 60; i++)
            {
                if (!IsFileInUse(mainExePath))
                {
                    Console.WriteLine("Main application has exited.");
                    break;
                }

                Thread.Sleep(500);

                if (i == 59)
                {
                    Console.WriteLine("ERROR: Timeout waiting for main application to exit");
                    return 2;
                }
            }

            // Step 2: Create backup of current version
            Console.WriteLine("Creating backup of current version...");
            string backupPath = Path.Combine(targetPath, "backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backupPath);

            foreach (var file in Directory.GetFiles(targetPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                // Don't backup the update helper itself or data files
                if (fileName.Equals("UpdateHelper.exe", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                    continue;

                var backupFile = Path.Combine(backupPath, fileName);
                File.Copy(file, backupFile, overwrite: true);
            }

            Console.WriteLine($"Backup created at: {backupPath}");

            // Step 3: Copy new files from update directory
            Console.WriteLine("Installing update...");
            int filesUpdated = 0;

            foreach (var file in Directory.GetFiles(updatePath, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(updatePath, file);
                var targetFile = Path.Combine(targetPath, relativePath);

                // Create directory if needed
                var targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDir))
                    Directory.CreateDirectory(targetDir);

                // Copy file
                File.Copy(file, targetFile, overwrite: true);
                filesUpdated++;
                Console.WriteLine($"  Updated: {relativePath}");
            }

            Console.WriteLine($"Successfully updated {filesUpdated} files.");

            // Step 4: Clean up temp directory
            Console.WriteLine("Cleaning up...");
            try
            {
                Directory.Delete(updatePath, recursive: true);
                Console.WriteLine("Temp files removed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not delete temp directory: {ex.Message}");
            }

            // Step 5: Relaunch main application
            Console.WriteLine("Launching updated application...");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mainExePath,
                    UseShellExecute = true,
                    WorkingDirectory = targetPath
                }
            };

            if (process.Start())
            {
                Console.WriteLine("Application launched successfully!");
                Console.WriteLine("Update complete. This window will close in 3 seconds...");
                Thread.Sleep(3000);
                return 0;
            }
            else
            {
                Console.WriteLine("ERROR: Failed to launch application");
                Console.WriteLine("You may need to start it manually from:");
                Console.WriteLine(mainExePath);
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return 3;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL ERROR: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.ToString());
            Console.WriteLine();
            Console.WriteLine("The update could not be completed.");
            Console.WriteLine("You may need to reinstall the application manually.");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return 99;
        }
    }

    /// <summary>
    /// Checks if a file is currently in use by another process.
    /// </summary>
    private static bool IsFileInUse(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
