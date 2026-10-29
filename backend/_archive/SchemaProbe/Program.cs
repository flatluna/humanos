using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

class Program {
    static void Main() {
        var jsonText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HumanOS", "local.settings.json"));
        using var doc = JsonDocument.Parse(jsonText);
        var values = doc.RootElement.GetProperty("Values");
        var connStr = values.TryGetProperty("HumanOSDatabase", out var prop) ? prop.GetString() : values.GetProperty("SqlConnectionString").GetString();
        using var conn = new SqlConnection(connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT p.PersonId, p.Name, p.Email, p.TenantId, p.DepartmentId, d.Name as DeptName, p.JobRoleId, jr.Title as RoleTitle
FROM dbo.Person p
LEFT JOIN dbo.Department d ON p.DepartmentId = d.DepartmentId
LEFT JOIN dbo.JobRole jr ON p.JobRoleId = jr.JobRoleId
WHERE p.Name LIKE '%Andres%' OR p.Email LIKE '%andres%';
";
        using var rdr = cmd.ExecuteReader();
        Console.WriteLine("=== PEOPLE ===");
        while (rdr.Read()) {
            Console.WriteLine($"PersonId: {rdr[0]} | Name: {rdr[1]} | Email: {rdr[2]} | TenantId: {rdr[3]} | DeptId: {rdr[4]} | DeptName: {rdr[5]} | JobRoleId: {rdr[6]} | RoleTitle: {rdr[7]}");
        }
        rdr.Close();

        cmd.CommandText = "SELECT DepartmentId, TenantId, Name FROM dbo.Department;";
        using var rdr2 = cmd.ExecuteReader();
        Console.WriteLine("=== DEPARTMENTS ===");
        while (rdr2.Read()) {
            Console.WriteLine($"DeptId: {rdr2[0]} | TenantId: {rdr2[1]} | Name: {rdr2[2]}");
        }
    }
}