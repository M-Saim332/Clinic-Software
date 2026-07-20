using Dapper;
using System.Collections.Generic;
using System.Linq;

namespace ClinicSystem.Data.Repositories;

public class SettingsRepository
{
    private readonly DatabaseSession _session;

    public SettingsRepository(DatabaseSession session) => _session = session;

    public string GetValue(string key, string defaultValue = "")
    {
        try
        {
            using var conn = _session.CreateConnection();
            var val = conn.QueryFirstOrDefault<string>(
                "SELECT SettingValue FROM Settings WHERE SettingKey = @Key", new { Key = key });
            return val ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public void SetValue(string key, string value)
    {
        try
        {
            using var conn = _session.CreateConnection();
            conn.Execute(@"
                IF EXISTS (SELECT 1 FROM Settings WHERE SettingKey = @Key)
                    UPDATE Settings SET SettingValue = @Value WHERE SettingKey = @Key
                ELSE
                    INSERT INTO Settings (SettingKey, SettingValue) VALUES (@Key, @Value)
            ", new { Key = key, Value = value });
        }
        catch { }
    }

    public Dictionary<string, string> GetAll()
    {
        try
        {
            using var conn = _session.CreateConnection();
            return conn.Query("SELECT SettingKey, SettingValue FROM Settings")
                       .ToDictionary(r => (string)r.SettingKey, r => (string)r.SettingValue);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
