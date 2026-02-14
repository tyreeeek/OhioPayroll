using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace OhioPayroll.App.Services;

/// <summary>
/// Manages database backups: create, verify, prune, and list.
/// </summary>
public class BackupService
{
    private readonly string _dbFilePath;
    private readonly string _backupDirectory;

    public BackupService(string dbFilePath, string backupDirectory)
    {
        _dbFilePath = dbFilePath;
        _backupDirectory = backupDirectory;
    }

    /// <summary>
    /// Copies the encrypted DB file to the backup directory with a timestamp filename.
    /// </summary>
    public string CreateBackup()
    {
        Directory.CreateDirectory(_backupDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"payroll_backup_{timestamp}.db";
        var backupPath = Path.Combine(_backupDirectory, backupFileName);

        File.Copy(_dbFilePath, backupPath, overwrite: true);

        return backupPath;
    }

    /// <summary>
    /// Returns the DateTime of the most recent backup, or null if no backups exist.
    /// </summary>
    public DateTime? GetLastBackupTime()
    {
        var files = GetBackupFiles();
        if (files.Count == 0)
            return null;

        return new FileInfo(files[0]).CreationTime;
    }

    /// <summary>
    /// Opens the backup read-only with SQLite and runs a simple query to verify integrity.
    /// </summary>
    public bool VerifyBackup(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
                return false;

            var connectionString = $"Data Source={backupPath};Mode=ReadOnly";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Employees";
            var result = cmd.ExecuteScalar();

            return result is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes backups older than keepCount files (keeps the newest keepCount backups).
    /// </summary>
    public void PruneOldBackups(int keepCount = 30)
    {
        var files = GetBackupFiles();
        if (files.Count <= keepCount)
            return;

        var toDelete = files.Skip(keepCount).ToList();
        foreach (var file in toDelete)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore deletion errors for individual files
            }
        }
    }

    /// <summary>
    /// Returns list of backup file paths sorted newest first.
    /// </summary>
    public List<string> GetBackupFiles()
    {
        if (!Directory.Exists(_backupDirectory))
            return new List<string>();

        return Directory.GetFiles(_backupDirectory, "payroll_backup_*.db")
            .OrderByDescending(f => f)
            .ToList();
    }
}

