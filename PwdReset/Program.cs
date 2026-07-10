using System;
using BC = BCrypt.Net.BCrypt;
using Microsoft.Data.SqlClient;

const string connStr = "Server=localhost;Database=ClinicDB;Integrated Security=True;TrustServerCertificate=True;";
const string newPassword = "Admin@123";

var hash = BC.HashPassword(newPassword);
Console.WriteLine($"Generated hash: {hash}");

using var conn = new SqlConnection(connStr);
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = "UPDATE Users SET PasswordHash = @h WHERE Username = 'admin'";
cmd.Parameters.AddWithValue("@h", hash);
var rows = cmd.ExecuteNonQuery();
Console.WriteLine($"✅ Password reset complete — {rows} row(s) updated.");
Console.WriteLine($"   Username: admin");
Console.WriteLine($"   Password: {newPassword}");
