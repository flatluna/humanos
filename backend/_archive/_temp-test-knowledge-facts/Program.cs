using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

class Program {
    static void Main() {
        var jsonText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HumanOS", "local.settings.json"));
        using var doc = JsonDocument.Parse(jsonText);
        var connStr = doc.RootElement.GetProperty("Values").GetProperty("HumanOSDatabase").GetString();
        using var conn = new SqlConnection(connStr);
        conn.Open();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP 5 PersonId, Email, AzureOid, AzureTid, TenantId, LastLoginDate 
            FROM Person 
            ORDER BY LastLoginDate DESC;

            SELECT TOP 5 TenantInvitationId, Email, Status, AcceptedDate 
            FROM TenantInvitation 
            ORDER BY CreatedDate DESC;
        ";
        using var rdr = cmd.ExecuteReader();
        Console.WriteLine("=== RECENT PERSON LOGINS ===");
        while (rdr.Read()) {
            Console.WriteLine($"PersonId: {rdr[0]} | Email: {rdr[1]} | LastLogin: {rdr[5]} | Oid: {rdr[2]} | Tenant: {rdr[4]}");
        }
        rdr.NextResult();
        Console.WriteLine("=== RECENT INVITATIONS ===");
        while (rdr.Read()) {
            Console.WriteLine($"Inv: {rdr[0]} | Email: {rdr[1]} | Status: {rdr[2]} | Accepted: {rdr[3]}");
        }
    }
}
