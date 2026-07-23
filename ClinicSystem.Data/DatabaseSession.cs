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

    private bool _schemaChecked = false;
    private void EnsureSchemaUpdated(IDbConnection conn)
    {
        if (_schemaChecked) return;
        _schemaChecked = true;
        
        // ── 1. Check if Core Schema Exists (Patients table) ──
        bool schemaExists = false;
        try
        {
            conn.ExecuteScalar<int>("SELECT TOP 1 1 FROM Patients");
            schemaExists = true;
        }
        catch { }

        if (!schemaExists)
        {
            // Auto-run Schema.sql
            ExecuteSqlScript(conn, "Schema.sql");
        }

        // ── 2. Check if Bulk Data is missing (Empty Patients table) ──
        try
        {
            int patientCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Patients");
            if (patientCount == 0)
            {
                ExecuteSqlScript(conn, "BulkTestData.sql");
            }
        }
        catch { }

        try
        {
            // Ensure Permissions column exists on Users
            conn.Execute(@"
                IF COL_LENGTH('Users', 'Permissions') IS NULL
                BEGIN
                    ALTER TABLE Users ADD Permissions VARCHAR(1000) NULL;
                END
            ");
            
            // Profile fields for Users
            conn.Execute(@"
                IF COL_LENGTH('Users', 'Email') IS NULL ALTER TABLE Users ADD Email NVARCHAR(100) NULL;
                IF COL_LENGTH('Users', 'Phone') IS NULL ALTER TABLE Users ADD Phone NVARCHAR(50) NULL;
                IF COL_LENGTH('Users', 'CNIC') IS NULL ALTER TABLE Users ADD CNIC NVARCHAR(50) NULL;
                IF COL_LENGTH('Users', 'Address') IS NULL ALTER TABLE Users ADD Address NVARCHAR(500) NULL;
                IF COL_LENGTH('Users', 'Gender') IS NULL ALTER TABLE Users ADD Gender NVARCHAR(20) NULL;
                IF COL_LENGTH('Users', 'Qualification') IS NULL ALTER TABLE Users ADD Qualification NVARCHAR(200) NULL;
                IF COL_LENGTH('Users', 'Designation') IS NULL ALTER TABLE Users ADD Designation NVARCHAR(200) NULL;
                IF COL_LENGTH('Users', 'LicenseNumber') IS NULL ALTER TABLE Users ADD LicenseNumber NVARCHAR(100) NULL;
                IF COL_LENGTH('Users', 'DateOfBirth') IS NULL ALTER TABLE Users ADD DateOfBirth DATETIME2 NULL;
                IF COL_LENGTH('Users', 'ProfilePicture') IS NULL ALTER TABLE Users ADD ProfilePicture VARBINARY(MAX) NULL;
            ");

            // Audit & Security fields for Users
            conn.Execute(@"
                IF COL_LENGTH('Users', 'LastLogin') IS NULL ALTER TABLE Users ADD LastLogin DATETIME2 NULL;
                IF COL_LENGTH('Users', 'UpdatedAt') IS NULL ALTER TABLE Users ADD UpdatedAt DATETIME2 NULL;
                IF COL_LENGTH('Users', 'ForcePasswordChange') IS NULL ALTER TABLE Users ADD ForcePasswordChange BIT NOT NULL DEFAULT 0;
            ");

            // Ensure Settings table exists
            conn.Execute(@"
                IF OBJECT_ID('Settings', 'U') IS NULL
                BEGIN
                    CREATE TABLE Settings (
                        SettingKey NVARCHAR(100) PRIMARY KEY,
                        SettingValue NVARCHAR(MAX) NULL
                    );
                END
            ");

            // Ensure Returns table exists (new redesign)
            conn.Execute(@"
                IF OBJECT_ID('Returns', 'U') IS NULL
                BEGIN
                    CREATE TABLE Returns (
                        ReturnId INT IDENTITY(1,1) PRIMARY KEY,
                        ReturnNo NVARCHAR(50) NOT NULL,
                        ProductId INT NOT NULL,
                        BatchNo NVARCHAR(50) NULL,
                        Quantity INT NOT NULL,
                        ReturnType NVARCHAR(50) NOT NULL,
                        Reason NVARCHAR(200) NULL,
                        Notes NVARCHAR(500) NULL,
                        PatientId INT NULL,
                        SupplierId INT NULL,
                        SaleId INT NULL,
                        RefundAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
                        CreatedBy INT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
                    );
                END
                ELSE
                BEGIN
                    IF COL_LENGTH('Returns', 'PatientId') IS NULL ALTER TABLE Returns ADD PatientId INT NULL;
                    IF COL_LENGTH('Returns', 'SupplierId') IS NULL ALTER TABLE Returns ADD SupplierId INT NULL;
                    IF COL_LENGTH('Returns', 'SaleId') IS NULL ALTER TABLE Returns ADD SaleId INT NULL;
                    IF COL_LENGTH('Returns', 'RefundAmount') IS NULL ALTER TABLE Returns ADD RefundAmount DECIMAL(12,2) NOT NULL DEFAULT 0;
                END
            ");

            // Ensure DiscountRefunds table exists
            conn.Execute(@"
                IF OBJECT_ID('DiscountRefunds', 'U') IS NULL
                BEGIN
                    CREATE TABLE DiscountRefunds (
                        RefundID INT IDENTITY(1,1) PRIMARY KEY,
                        PatientName NVARCHAR(100) NOT NULL,
                        TokenNumber NVARCHAR(50) NULL,
                        OriginalFee DECIMAL(18,2) NOT NULL,
                        DiscountedFee DECIMAL(18,2) NOT NULL,
                        RefundAmount AS (OriginalFee - DiscountedFee) PERSISTED,
                        Notes NVARCHAR(500) NULL,
                        ApprovedByUserID INT NULL,
                        ApprovedByName NVARCHAR(100) NULL,
                        ApprovedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                        CompletedByUserID INT NULL,
                        CompletedByName NVARCHAR(100) NULL,
                        CompletedAt DATETIME2 NULL,
                        IsCompleted BIT NOT NULL DEFAULT 0
                    );
                END
            ");
        }
        catch { }

        try
        {
            // Ensure additional columns exist on Appointments
            conn.Execute(@"
                IF COL_LENGTH('Appointments', 'Gender') IS NULL
                BEGIN
                    ALTER TABLE Appointments ADD Gender NVARCHAR(20) NULL;
                END
                IF COL_LENGTH('Appointments', 'Age') IS NULL
                BEGIN
                    ALTER TABLE Appointments ADD Age INT NULL;
                END
                IF COL_LENGTH('Appointments', 'Phone') IS NULL
                BEGIN
                    ALTER TABLE Appointments ADD Phone VARCHAR(50) NULL;
                END
                IF COL_LENGTH('Appointments', 'Remarks') IS NULL
                BEGIN
                    ALTER TABLE Appointments ADD Remarks VARCHAR(255) NULL;
                END
                IF COL_LENGTH('Appointments', 'CancellationReason') IS NULL
                BEGIN
                    ALTER TABLE Appointments ADD CancellationReason VARCHAR(255) NULL;
                END
                IF COL_LENGTH('Appointments', 'AppointmentNo') IS NULL ALTER TABLE Appointments ADD AppointmentNo VARCHAR(50) NULL;
                IF COL_LENGTH('Appointments', 'PatientName') IS NULL ALTER TABLE Appointments ADD PatientName VARCHAR(150) NULL;
                IF COL_LENGTH('Appointments', 'Reason') IS NULL ALTER TABLE Appointments ADD Reason VARCHAR(255) NULL;
            ");
        }
        catch { }

        try
        {
            // Ensure Visit Tracking columns exist on Patients
            conn.Execute(@"
                IF COL_LENGTH('Patients', 'VisitStatus') IS NULL
                BEGIN
                    ALTER TABLE Patients ADD VisitStatus VARCHAR(20) NULL;
                END
                IF COL_LENGTH('Patients', 'LastVisitDate') IS NULL
                BEGIN
                    ALTER TABLE Patients ADD LastVisitDate DATE NULL;
                END
                IF COL_LENGTH('Patients', 'Age') IS NULL ALTER TABLE Patients ADD Age INT NULL;
                IF COL_LENGTH('Patients', 'Gender') IS NULL ALTER TABLE Patients ADD Gender VARCHAR(10) NULL;
                IF COL_LENGTH('Patients', 'Diagnosis') IS NULL ALTER TABLE Patients ADD Diagnosis TEXT NULL;
                IF COL_LENGTH('Patients', 'Prescription') IS NULL ALTER TABLE Patients ADD Prescription TEXT NULL;
                IF COL_LENGTH('Patients', 'ConsultationFee') IS NULL ALTER TABLE Patients ADD ConsultationFee DECIMAL(10,2) DEFAULT 0;
                IF COL_LENGTH('Patients', 'Discount') IS NULL ALTER TABLE Patients ADD Discount DECIMAL(10,2) DEFAULT 0;
                IF COL_LENGTH('Patients', 'NextAppointmentDate') IS NULL ALTER TABLE Patients ADD NextAppointmentDate DATE NULL;
                IF COL_LENGTH('Patients', 'NextAppointmentTime') IS NULL ALTER TABLE Patients ADD NextAppointmentTime TIME NULL;
                IF COL_LENGTH('Patients', 'CNIC') IS NULL ALTER TABLE Patients ADD CNIC NVARCHAR(50) NULL;
            ");
        }
        catch { }

        try
        {
            // Ensure missing columns exist on Suppliers
            conn.Execute(@"
                IF COL_LENGTH('Suppliers', 'CNIC') IS NULL ALTER TABLE Suppliers ADD CNIC NVARCHAR(50) NULL;
            ");
        }
        catch { }

        try
        {
            // Ensure missing columns exist on Products
            conn.Execute(@"
                IF COL_LENGTH('Products', 'GenericName') IS NULL ALTER TABLE Products ADD GenericName VARCHAR(150) NULL;
                IF COL_LENGTH('Products', 'CompanyName') IS NULL ALTER TABLE Products ADD CompanyName VARCHAR(150) NULL;
                IF COL_LENGTH('Products', 'SupplierID') IS NULL ALTER TABLE Products ADD SupplierID INT NULL FOREIGN KEY REFERENCES Suppliers(SupplierID);
                IF COL_LENGTH('Products', 'SupplierName') IS NULL ALTER TABLE Products ADD SupplierName VARCHAR(150) NULL;
                IF COL_LENGTH('Products', 'BatchNumber') IS NULL ALTER TABLE Products ADD BatchNumber VARCHAR(50) NULL;
                IF COL_LENGTH('Products', 'Type') IS NULL ALTER TABLE Products ADD Type VARCHAR(50) NULL;
                IF COL_LENGTH('Products', 'Category') IS NULL ALTER TABLE Products ADD Category VARCHAR(100) NULL;
                IF COL_LENGTH('Products', 'Rack') IS NULL ALTER TABLE Products ADD Rack VARCHAR(50) NULL;
                IF COL_LENGTH('Products', 'ExpiryDate') IS NULL ALTER TABLE Products ADD ExpiryDate DATE NULL;
                IF COL_LENGTH('Products', 'PurchasePrice') IS NULL ALTER TABLE Products ADD PurchasePrice DECIMAL(10,2) DEFAULT 0;
                IF COL_LENGTH('Products', 'Stock') IS NULL ALTER TABLE Products ADD Stock INT DEFAULT 0;
                IF COL_LENGTH('Products', 'MinimumStockLevel') IS NULL ALTER TABLE Products ADD MinimumStockLevel INT DEFAULT 0;
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

    private void ExecuteSqlScript(IDbConnection conn, string fileName)
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
                if (trimmed.StartsWith("USE master", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("USE ClinicDB", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase) ||
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
                    System.Console.WriteLine($"Failed to execute script part from {fileName}:\n{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
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

