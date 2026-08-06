using Microsoft.Data.SqlClient;

var connStr = "Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Authentication=Active Directory Default;";

await using var connection = new SqlConnection(connStr);
await connection.OpenAsync();

await using var cmd = new SqlCommand(
    """
    SELECT CapabilityId, Name, Code, IsActive, OwnerPersonId, OwnerTenantId, CreatedDate
    FROM dbo.Capability
    WHERE IsActive = 1
    ORDER BY CreatedDate;
    """,
    connection);








await using (var reader = await cmd.ExecuteReaderAsync())
{
    do
    {
        Console.WriteLine("--- result set ---");
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                Console.Write($"{reader.GetName(i)}={values[i]} | ");
            }
            Console.WriteLine();
        }
    } while (await reader.NextResultAsync());
}




