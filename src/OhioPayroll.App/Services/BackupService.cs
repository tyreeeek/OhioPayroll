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
    /// Copies the DB file and encryption keys to the backup directory with a timestamp filename.
    /// Both the .enckey file and dp-keys directory are required to decrypt SSNs and bank accounts.
    /// </summary>
    public string CreateBackup()
    {
        Directory.CreateDirectory(_backupDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"payroll_backup_{timestamp}.db";
        var backupPath = Path.Combine(_backupDirectory, backupFileName);

        File.Copy(_dbFilePath, backupPath, overwrite: true);

        // Backup encryption keys (required to decrypt SSNs, TINs, bank accounts)
        var dbDir = Path.GetDirectoryName(_dbFilePath) ?? Path.GetPathRoot(_dbFilePath);
        if (string.IsNullOrEmpty(dbDir))
        {
            throw new InvalidOperationException(
                $"Cannot determine directory for database file '{_dbFilePath}'. Backup aborted to avoid losing encryption keys.");
        }

        var encKeyFile = Path.Combine(dbDir, ".enckey");
        var dpKeysDir = Path.Combine(dbDir, "dp-keys");

        var encKeyExists = File.Exists(encKeyFile);
        var dpKeysExists = Directory.Exists(dpKeysDir);

        if (encKeyExists != dpKeysExists)
        {
            // Partial key state: one artifact exists but not the other
            var missing = encKeyExists ? "dp-keys directory" : ".enckey file";
            var present = encKeyExists ? ".enckey file" : "dp-keys directory";
            AppLogger.Warning(
                $"Partial encryption key state detected during backup: {present} exists but {missing} is missing. " +
                "Both key artifacts are required to decrypt SSNs and bank accounts. Skipping key backup.");
        }
        else if (!encKeyExists && !dpKeysExists)
        {
            throw new InvalidOperationException(
                "No encryption key artifacts found (.enckey and dp-keys are both missing). " +
                "Encrypted fields (SSNs, TINs, bank accounts) will not be recoverable from this backup. " +
                "Backup aborted to prevent data loss.");
        }

        if (encKeyExists && dpKeysExists)
        {
            var keysBackupDir = Path.Combine(_backupDirectory, $"keys_{timestamp}");
            Directory.CreateDirectory(keysBackupDir);

            File.Copy(encKeyFile, Path.Combine(keysBackupDir, ".enckey"), overwrite: true);

            var dpKeysBackup = Path.Combine(keysBackupDir, "dp-keys");
            Directory.CreateDirectory(dpKeysBackup);
            foreach (var file in Directory.GetFiles(dpKeysDir))
            {
                File.Copy(file, Path.Combine(dpKeysBackup, Path.GetFileName(file)), overwrite: true);
            }
        }

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

