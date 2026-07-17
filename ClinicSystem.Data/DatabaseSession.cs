using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.IO;
using Dapper;

namespace ClinicSystem.Data;

/// <summary>
/// Opens SQL Server connections from the configured connection string.
/// The connection string is read from appsettings.json in the app directory.
/// </summary>
public class DatabaseSession
{
    private readonly string _connectionString;

    public DatabaseSession(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ClinicDB")
            ?? throw new InvalidOperationException(
                "Connection string 'ClinicDB' not found in appsettings.json. " +
                "Please configure the database connection.");
    }

    /// <summary>Opens and returns an open SQL connection (caller must dispose).</summary>
    public IDbConnection CreateConnection()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open();
        EnsureSchemaUpdated(conn);
        return conn;
    }

    private bool _schemaChecked = false;
    private void EnsureSchemaUpdated(IDbConnection conn)
    {
        if (_schemaChecked) return;
        _schemaChecked = true;
        try
        {
            // Ensure Permissions column exists on Users
            conn.Execute(@"
                IF COL_LENGTH('Users', 'Permissions') IS NULL
                BEGIN
                    ALTER TABLE Users ADD Permissions VARCHAR(1000) NULL;
                END
            ");
        }
        catch { }

        try
        {
            // Ensure ActivityLogs table exists for the dashboard feed
            conn.Execute(@"
                IF OBJECT_ID('ActivityLogs', 'U') IS NULL
                BEGIN
                    CREATE TABLE ActivityLogs (
                        ActivityId  INT IDENTITY(1,1) PRIMARY KEY,
                        Title       NVARCHAR(200)  NOT NULL,
                        Description NVARCHAR(500)  NOT NULL DEFAULT '',
                        Module      NVARCHAR(100)  NOT NULL DEFAULT '',
                        UserId      INT            NOT NULL DEFAULT 0,
                        UserName    NVARCHAR(100)  NOT NULL DEFAULT '',
                        CreatedAt   DATETIME2      NOT NULL DEFAULT GETDATE()
                    );
                END
            ");
        }
        catch { }
    }

    /// <summary>Tests connectivity — returns null on success, error message on failure.</summary>
    public string? TestConnection()
    {
        try
        {
            using var conn = CreateConnection();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Backs up the database to the specified path, handling permission constraints.</summary>
    public void Backup(string destinationPath)
    {
        using var conn = CreateConnection();
        try
        {
            // Attempt to backup directly to the destination path
            conn.Execute("BACKUP DATABASE ClinicDB TO DISK = @destinationPath WITH FORMAT", new { destinationPath });
        }
        catch (SqlException ex) when (ex.Number == 3201 || ex.Number == 3013)
        {
            // Error 3201/3013 usually indicates operating system error / permission denied on target path.
            // Fall back to SQL Server's own backup directory and copy the file from there.
            string defaultDir = GetDefaultBackupDirectory(conn);
            string tempFile = Path.Combine(defaultDir, "ClinicDB_temp_backup.bak");
            try
            {
                conn.Execute("BACKUP DATABASE ClinicDB TO DISK = @tempFile WITH FORMAT", new { tempFile });
                File.Copy(tempFile, destinationPath, true);
            }
            catch (Exception innerEx)
            {
                throw new Exception(
                    $"Backup failed. SQL Server could not write to the destination path, and the fallback backup also failed.\n\n" +
                    $"Details: {innerEx.Message}\n\n" +
                    $"Tip: Try backing up to a folder that both SQL Server and your user account can access (e.g. C:\\Temp), " +
                    $"or run the application with elevated administrator privileges.", innerEx);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch { }
            }
        }
    }

    private string GetDefaultBackupDirectory(IDbConnection conn)
    {
        try
        {
            var path = conn.QueryFirstOrDefault<string>(@"
                DECLARE @BackupDir NVARCHAR(4000);
                EXEC master.dbo.xp_instance_regread
                    N'HKEY_LOCAL_MACHINE',
                    N'Software\Microsoft\MSSQLServer\MSSQLServer',
                    N'BackupDirectory',
                    @BackupDir OUTPUT;
                SELECT @BackupDir;");
            if (!string.IsNullOrEmpty(path)) return path;
        }
        catch { }

        // Common defaults for SQL Server Express 2019/2022
        var paths = new[]
        {
            @"C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup",
            @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup",
            @"C:\Program Files\Microsoft SQL Server\MSSQL14.SQLEXPRESS\MSSQL\Backup"
        };
        foreach (var p in paths)
        {
            if (Directory.Exists(p)) return p;
        }

        return @"C:\Windows\Temp"; // Fallback folder that SQL Server usually has access to
    }

    /// <summary>Restores the database from the specified path.</summary>
    public void Restore(string sourcePath)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };
        using var conn = new SqlConnection(builder.ConnectionString);
        conn.Open();
        
        conn.Execute(@"
            ALTER DATABASE ClinicDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            RESTORE DATABASE ClinicDB FROM DISK = @sourcePath WITH REPLACE;
            ALTER DATABASE ClinicDB SET MULTI_USER;", new { sourcePath });
    }

    /// <summary>
    /// Returns the auto-rollback backup file path used by ResetAllData.
    /// </summary>
    public string GetRollbackBackupPath()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        string dir = GetDefaultBackupDirectory(conn);
        return Path.Combine(dir, "ClinicDB_PreReset_Rollback.bak");
    }

    /// <summary>
    /// Creates a rollback backup, then deletes all clinical data (FK order respected).
    /// Tables wiped: SaleItems, Sales, PurchaseItems, Purchases, Prescriptions, Appointments,
    /// Returns, Patients, Products, Products, Companies, Suppliers.
    /// Users and Settings are preserved.
    /// </summary>
    public void ResetAllData()
    {
        // Step 1 — auto rollback backup so user can recover
        using var backupConn = new SqlConnection(_connectionString);
        backupConn.Open();
        string backupDir = GetDefaultBackupDirectory(backupConn);
        string rollbackPath = Path.Combine(backupDir, "ClinicDB_PreReset_Rollback.bak");
        backupConn.Execute("BACKUP DATABASE ClinicDB TO DISK = @rollbackPath WITH FORMAT", new { rollbackPath });

        // Step 2 — delete all data in FK-safe order
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        // Delete all data in FK-safe order; skip tables that may not exist yet
        var tables = new[]
        {
            "SaleItems", "Sales", "PurchaseItems", "Purchases",
            "ProductReturns", "PrescriptionItems", "Prescriptions",
            "Appointments", "Patients", "Products", "Companies", "Suppliers"
        };
        foreach (var t in tables)
        {
            try
            {
                conn.Execute($"IF OBJECT_ID('{t}', 'U') IS NOT NULL DELETE FROM [{t}]");
            }
            catch { /* ignore if table does not exist */ }
        }
    }

    /// <summary>
    /// Restores from the automatic pre-reset rollback backup created by ResetAllData.
    /// </summary>
    public void RollbackReset()
    {
        string rollbackPath = "";
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            string dir = GetDefaultBackupDirectory(conn);
            rollbackPath = Path.Combine(dir, "ClinicDB_PreReset_Rollback.bak");
        }

        if (!File.Exists(rollbackPath))
            throw new FileNotFoundException("No rollback backup found. A reset must be performed first before rollback is available.");

        Restore(rollbackPath);
    }
}

