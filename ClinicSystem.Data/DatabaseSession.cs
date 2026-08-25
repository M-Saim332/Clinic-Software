using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
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

    private bool _schemaChecked;
    private readonly object _schemaLock = new();
    private void EnsureSchemaUpdated(IDbConnection conn)
    {
        lock (_schemaLock)
        {
            if (_schemaChecked) return;

            // The base schema must succeed before optional migrations are attempted.
            ExecuteSqlScript(conn, "Schema.sql", throwOnError: true);
            ExecuteSqlScript(conn, "Migration_AddDiscountRefunds.sql");

            string migrationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "Migrations");
            if (Directory.Exists(migrationsDir))
            {
                var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql").OrderBy(f => f);
                foreach (var file in sqlFiles)
                    ExecuteSqlScript(conn, Path.Combine("Migrations", Path.GetFileName(file)));
            }

            // Patch purchase item columns before strict verification so existing installs can upgrade in place.
            try
            {
                int packMRPExists = conn.ExecuteScalar<int>("SELECT CASE WHEN COL_LENGTH('PurchaseItems', 'PackMRP') IS NULL THEN 0 ELSE 1 END");
                if (packMRPExists == 0)
                {
                    conn.Execute("ALTER TABLE PurchaseItems ADD PackMRP DECIMAL(18,2) NOT NULL DEFAULT 0");
                }

                int unitsPerPackageExists = conn.ExecuteScalar<int>("SELECT CASE WHEN COL_LENGTH('PurchaseItems', 'UnitsPerPackage') IS NULL THEN 0 ELSE 1 END");
                if (unitsPerPackageExists == 0)
                {
                    conn.Execute("ALTER TABLE PurchaseItems ADD UnitsPerPackage INT NOT NULL DEFAULT 1");
                }
            }
            catch { }

            VerifyRequiredSchema(conn);

            _schemaChecked = true;

            // Ensure the recovery administrator exists without changing an existing password.
            try
            {
                int adminCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Username = 'admin'");
                if (adminCount == 0)
                    conn.Execute("INSERT INTO Users (Username, PasswordHash, Role, FullName, IsActive) VALUES ('admin', '$2a$11$u0LyGgHmhN2kTeoBK.a5m.FVHXHSUA/xHZFJ9tE1O4Oj4QvICWT.O', 'Admin', 'System Admin', 1)");
            }
            catch { }

        }
    }

    private static void VerifyRequiredSchema(IDbConnection conn)
    {
        string[] tables = ["Patients", "Products", "Suppliers", "Companies", "Users", "Appointments", "Purchases", "PurchaseItems", "Sales", "SaleItems", "Prescriptions", "PrescriptionItems", "Returns", "ReturnItems"];
        var missingTables = tables.Where(table => conn.ExecuteScalar<int>(
            "SELECT CASE WHEN OBJECT_ID(@table, 'U') IS NULL THEN 1 ELSE 0 END", new { table }) == 1).ToList();
        if (missingTables.Count > 0)
            throw new InvalidOperationException($"Database upgrade incomplete. Missing tables: {string.Join(", ", missingTables)}. Ensure the database user has CREATE TABLE and ALTER permissions.");

        (string Table, string Column)[] columns =
        [
            ("Patients", "Phone"), ("Patients", "IsActive"),
            ("Products", "TabletsPerBox"), ("Products", "IsActive"),
            ("Sales", "ReceptionistId"), ("Sales", "IsActive"),
            ("SaleItems", "StockQuantity"), ("PurchaseItems", "PackageQuantity"),
            ("Returns", "StockQuantity"),
            ("Companies", "CCode"), ("Suppliers", "SCode"), ("Products", "PCode"),
            ("Products", "Packing"), ("Products", "PiecesPerUnit"), ("Products", "LastStockUpdateDate"),
            ("Appointments", "CNIC"), ("Patients", "PatientContext"), ("Patients", "ReasonOfVisit"),
            ("Purchases", "IsPosted"), ("Purchases", "PostedAt"), ("Purchases", "ATax"), ("PurchaseItems", "ExtraDiscount"),
            ("PurchaseItems", "ATax"), ("PurchaseItems", "CompanySalesTax"), ("PurchaseItems", "PackMRP"), ("PurchaseItems", "UnitsPerPackage"), ("Sales", "SalesTax"), ("Sales", "PostedAt"),
            ("Returns", "IsPosted"), ("Returns", "PostedAt")
        ];
        var missingColumns = columns.Where(item => conn.ExecuteScalar<int>(
            "SELECT CASE WHEN COL_LENGTH(@table, @column) IS NULL THEN 1 ELSE 0 END",
            new { table = item.Table, column = item.Column }) == 1).Select(item => $"{item.Table}.{item.Column}").ToList();
        if (missingColumns.Count > 0)
            throw new InvalidOperationException($"Database upgrade incomplete. Missing columns: {string.Join(", ", missingColumns)}. Ensure the database user has ALTER permissions.");
    }

    private void ExecuteSqlScript(IDbConnection conn, string fileName, bool throwOnError = false)
    {
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", fileName);
        if (!File.Exists(filePath)) return;

        try
        {
            string script = File.ReadAllText(filePath);
            var commands = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            
            foreach (var cmd in commands)
            {
                string trimmed = cmd.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                
                // Skip database creation lines since we are already connected to ClinicDB
                if (trimmed.IndexOf("USE master", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    trimmed.IndexOf("USE ClinicDB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    trimmed.IndexOf("CREATE DATABASE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    trimmed.Contains("sys.databases WHERE name = 'ClinicDB'"))
                {
                    continue;
                }

                try
                {
                    conn.Execute(cmd);
                }
                catch (Exception ex)
                {
                    if (throwOnError)
                        throw new InvalidOperationException($"Failed to execute required schema batch from {fileName}: {ex.Message}", ex);
                    System.Console.WriteLine($"Failed to execute script part from {fileName}:\n{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            if (throwOnError) throw;
            System.Console.WriteLine($"Failed to read {fileName}: {ex.Message}");
        }
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
            "Returns", "DiscountRefunds", "ActivityLogs", "PrescriptionItems", "Prescriptions",
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
