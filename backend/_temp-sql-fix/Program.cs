using Microsoft.Data.SqlClient;

var connStr = "Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Authentication=Active Directory Default;";

await using var connection = new SqlConnection(connStr);
await connection.OpenAsync();

await using var cmd = new SqlCommand(
    """
    SELECT p.PersonId, p.Email, pp.DisplayName, p.OrgRole, p.IsActive,
           p.DepartmentId, d.Name AS DepartmentName,
           p.JobRoleId, jr.Title AS JobRoleTitle,
           p.ManagerId, mpp.DisplayName AS ManagerDisplayName
    FROM dbo.Person p
    LEFT JOIN dbo.PersonProfile pp ON pp.PersonId = p.PersonId
    LEFT JOIN dbo.Department d ON d.DepartmentId = p.DepartmentId
    LEFT JOIN dbo.JobRole jr ON jr.JobRoleId = p.JobRoleId
    LEFT JOIN dbo.Person m ON m.PersonId = p.ManagerId
    LEFT JOIN dbo.PersonProfile mpp ON mpp.PersonId = m.PersonId
    WHERE p.Email = 'jorgeruizagent@gmail.com';
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

Console.WriteLine();
Console.WriteLine("=== related-data presence check ===");

await using var cmd2 = new SqlCommand(
    """
    DECLARE @pid UNIQUEIDENTIFIER = (SELECT PersonId FROM dbo.Person WHERE Email = 'jorgeruizagent@gmail.com');

    SELECT 'PersonId' AS Item, CAST(@pid AS nvarchar(50)) AS Value
    UNION ALL
    SELECT 'PersonProfile row exists', CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.PersonProfile WHERE PersonId=@pid) THEN 1 ELSE 0 END AS nvarchar(10))
    UNION ALL
    SELECT 'PersonResume row exists', CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.PersonResume WHERE PersonId=@pid) THEN 1 ELSE 0 END AS nvarchar(10))
    UNION ALL
    SELECT 'PersonCareerPlan row exists', CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.PersonCareerPlan WHERE PersonId=@pid) THEN 1 ELSE 0 END AS nvarchar(10))
    UNION ALL
    SELECT 'PersonCareerPlan.IsAccepted', ISNULL(CAST((SELECT CAST(IsAccepted AS int) FROM dbo.PersonCareerPlan WHERE PersonId=@pid) AS nvarchar(10)), 'n/a')
    UNION ALL
    SELECT 'HumanProfile row exists', CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.HumanProfile WHERE PersonId=@pid) THEN 1 ELSE 0 END AS nvarchar(10))
    UNION ALL
    SELECT 'Person.JobRoleId set', CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.Person WHERE PersonId=@pid AND JobRoleId IS NOT NULL) THEN 1 ELSE 0 END AS nvarchar(10))
    UNION ALL
    SELECT 'Person.DepartmentId set', CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.Person WHERE PersonId=@pid AND DepartmentId IS NOT NULL) THEN 1 ELSE 0 END AS nvarchar(10))
    UNION ALL
    SELECT 'Person.ManagerId set', CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.Person WHERE PersonId=@pid AND ManagerId IS NOT NULL) THEN 1 ELSE 0 END AS nvarchar(10));
    """,
    connection);

await using var reader2 = await cmd2.ExecuteReaderAsync();
while (await reader2.ReadAsync())
{
    Console.WriteLine($"{reader2.GetString(0)}: {reader2.GetString(1)}");
}



