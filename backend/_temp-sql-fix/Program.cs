using Microsoft.Data.SqlClient;

const string connStr = "Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Authentication=Active Directory Default;";
await using var conn = new SqlConnection(connStr);
await conn.OpenAsync();

await using var cmd = new SqlCommand(
    @"SELECT c.CapabilityId, c.Name, c.OwnerPersonId, c.OwnerTenantId, c.Category, c.TargetSkill,
             p.Email AS OwnerPersonEmail, t.Name AS OwnerTenantName
      FROM dbo.Capability c
      LEFT JOIN dbo.Person p ON p.PersonId = c.OwnerPersonId
      LEFT JOIN dbo.Tenant t ON t.TenantId = c.OwnerTenantId
      WHERE c.CapabilityId = 'a93d743f-ed3e-4696-9b79-6fa566ca533a'", conn);
await using (var reader = await cmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"{reader["CapabilityId"]} | {reader["Name"]} | OwnerPersonId={reader["OwnerPersonId"]} | OwnerTenantId={reader["OwnerTenantId"]} | Category={reader["Category"]} | TargetSkill={reader["TargetSkill"]} | OwnerPersonEmail={reader["OwnerPersonEmail"]} | OwnerTenantName={reader["OwnerTenantName"]}");
    }
}

await using var cmd2 = new SqlCommand(
    @"SELECT p.PersonId, p.Email, p.TenantId, p.OrgRole, t.Name AS TenantName
      FROM dbo.Person p
      LEFT JOIN dbo.Tenant t ON t.TenantId = p.TenantId
      WHERE p.PersonId = '555e2d56-cbc7-4d5c-b524-3c261552c442'", conn);
await using (var reader2 = await cmd2.ExecuteReaderAsync())
{
    while (await reader2.ReadAsync())
    {
        Console.WriteLine($"PERSON: {reader2["PersonId"]} | {reader2["Email"]} | TenantId={reader2["TenantId"]} | OrgRole={reader2["OrgRole"]} | TenantName={reader2["TenantName"]}");
    }
}

await using var cmd3 = new SqlCommand("SELECT TenantId, Name FROM dbo.Tenant", conn);
await using (var reader3 = await cmd3.ExecuteReaderAsync())
{
    while (await reader3.ReadAsync())
    {
        Console.WriteLine($"TENANT: {reader3["TenantId"]} | {reader3["Name"]}");
    }
}

await using var cmd4 = new SqlCommand(
    @"SELECT p.PersonId, p.Email, p.TenantId, p.OrgRole, t.Name AS TenantName
      FROM dbo.Person p
      LEFT JOIN dbo.Tenant t ON t.TenantId = p.TenantId
      WHERE p.Email LIKE '%alejandro%'", conn);
await using (var reader4 = await cmd4.ExecuteReaderAsync())
{
    while (await reader4.ReadAsync())
    {
        Console.WriteLine($"ALEJANDRO: {reader4["PersonId"]} | {reader4["Email"]} | TenantId={reader4["TenantId"]} | OrgRole={reader4["OrgRole"]} | TenantName={reader4["TenantName"]}");
    }
}

await using var cmd5 = new SqlCommand(
    @"SELECT p.PersonId, p.Email, p.TenantId, pp.DisplayName, pp.FirstName, pp.LastName
      FROM dbo.Person p
      LEFT JOIN dbo.PersonProfile pp ON pp.PersonId = p.PersonId
      WHERE p.PersonId = '555e2d56-cbc7-4d5c-b524-3c261552c442'", conn);
await using (var reader5 = await cmd5.ExecuteReaderAsync())
{
    while (await reader5.ReadAsync())
    {
        Console.WriteLine($"JORGE_PROFILE: {reader5["PersonId"]} | {reader5["Email"]} | TenantId={reader5["TenantId"]} | DisplayName={reader5["DisplayName"]} | First={reader5["FirstName"]} | Last={reader5["LastName"]}");
    }
}


