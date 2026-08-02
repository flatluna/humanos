using Microsoft.Data.SqlClient;

var connStr = "Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Authentication=Active Directory Default;";

// Links Alejandro Díaz's existing seeded Person row to the real CIAM identity
// created via self-service sign-up on human-os-employee (twinetwork.ciamlogin.com),
// replacing the placeholder AzureOid/AzureTid/Email values from the original seed script.
await using var connection = new SqlConnection(connStr);
await connection.OpenAsync();

await using var cmd = new SqlCommand(
    """
    UPDATE dbo.Person
    SET AzureOid = @azureOid,
        AzureTid = @azureTid,
        Email = @email,
        UpdatedDate = SYSUTCDATETIME()
    WHERE PersonId = @personId;

    SELECT PersonId, TenantId, AzureOid, AzureTid, Email, IsActive
    FROM dbo.Person
    WHERE PersonId = @personId;
    """,
    connection);
cmd.Parameters.AddWithValue("@personId", Guid.Parse("562cb73a-414b-47a8-957e-9f1950ae9622"));
cmd.Parameters.AddWithValue("@azureOid", "d1afcf4c-b6dd-4dbf-ba1c-276021d298dd");
cmd.Parameters.AddWithValue("@azureTid", "0e9c8663-a4ff-440e-af94-be25e63a1a6a");
cmd.Parameters.AddWithValue("@email", "jorgepmoon+alejandro@gmail.com");

await using var reader = await cmd.ExecuteReaderAsync();
var count = 0;
while (await reader.ReadAsync())
{
    count++;
    Console.WriteLine(
        $"PersonId={reader["PersonId"]} TenantId={reader["TenantId"]} " +
        $"AzureOid={reader["AzureOid"]} AzureTid={reader["AzureTid"]} " +
        $"Email={reader["Email"]} IsActive={reader["IsActive"]}");
}

Console.WriteLine($"--- Updated {count} row(s) ---");

