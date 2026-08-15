using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClinicSystem.UI.Services;

/// <summary>
/// Builds, validates and persists the SQL Server connection string
/// to appsettings.local.json without breaking other JSON keys.
/// </summary>
public static class ConnectionStringHelper
{
    // ── Path helpers ─────────────────────────────────────────────────────────
    public static string LocalJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

    public static string BaseJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    // ── Connection string builder ─────────────────────────────────────────────
    public static string Build(
        string server,
        string database,
        AuthMode authMode,
        string sqlUser = "",
        string sqlPassword = "")
    {
        string auth = authMode == AuthMode.Windows
            ? "Trusted_Connection=True;"
            : $"User Id={sqlUser};Password={sqlPassword};";

        return $"Server={server};Database={database};{auth}TrustServerCertificate=True;";
    }

    // ── Test connection ───────────────────────────────────────────────────────
    public static (bool ok, string error) TestConnection(string connectionString)
    {
        try
        {
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            conn.Open();
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Placeholder check ─────────────────────────────────────────────────────
    public static bool IsPlaceholder(string? cs) =>
        string.IsNullOrWhiteSpace(cs) || cs.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);

    // ── Read current effective connection string ──────────────────────────────
    public static string? ReadEffective()
    {
        // local overrides base
        string? local = ReadFromFile(LocalJsonPath);
        if (!string.IsNullOrWhiteSpace(local)) return local;
        return ReadFromFile(BaseJsonPath);
    }

    private static string? ReadFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = JsonNode.Parse(File.ReadAllText(path));
            return json?["ConnectionStrings"]?["ClinicDB"]?.GetValue<string>();
        }
        catch { return null; }
    }

    // ── Persist to appsettings.local.json ────────────────────────────────────
    public static void SaveToLocalJson(string connectionString)
    {
        JsonObject root;

        if (File.Exists(LocalJsonPath))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(LocalJsonPath))?.AsObject()
                       ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        // Ensure ConnectionStrings section exists
        if (root["ConnectionStrings"] is not JsonObject cs)
        {
            cs = new JsonObject();
            root["ConnectionStrings"] = cs;
        }

        cs["ClinicDB"] = connectionString;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(LocalJsonPath, root.ToJsonString(options));
    }
}

public enum AuthMode
{
    Windows,
    SqlServer
}
