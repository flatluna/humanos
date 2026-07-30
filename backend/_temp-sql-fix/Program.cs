using Microsoft.Data.SqlClient;

var connStr = "Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Authentication=Active Directory Default;";

var capabilityId = Guid.NewGuid();
var capabilityGraphId = Guid.NewGuid();
var domainId = Guid.Parse("9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"); // MIND
var subjectId = Guid.Parse("bfc17ca4-040e-4407-8ece-38703b95d0cf"); // k-12

await using var connection = new SqlConnection(connStr);
await connection.OpenAsync();

await using var cmd = new SqlCommand(
    """
    INSERT INTO dbo.Capability
        (CapabilityId, CapabilityDomainId, SubjectId, Code, Name, Description,
         IsActive, ContentLanguageCode, PedagogicalAudience, CreatedDate, UpdatedDate)
    VALUES
        (@capabilityId, @domainId, @subjectId, @code, @name, @description,
         1, 'en', 'YoungLearner', SYSUTCDATETIME(), SYSUTCDATETIME());

    INSERT INTO dbo.CapabilityGraphs
        (CapabilityGraphId, CapabilityId, Name, Description, CreatedDate)
    VALUES
        (@capabilityGraphId, @capabilityId, @graphName, @graphDescription, SYSUTCDATETIME());
    """,
    connection);
cmd.Parameters.AddWithValue("@capabilityId", capabilityId);
cmd.Parameters.AddWithValue("@capabilityGraphId", capabilityGraphId);
cmd.Parameters.AddWithValue("@domainId", domainId);
cmd.Parameters.AddWithValue("@subjectId", subjectId);
cmd.Parameters.AddWithValue("@code", "grade1-reading-en");
cmd.Parameters.AddWithValue("@name", "Grade 1 Reading");
cmd.Parameters.AddWithValue("@description",
    "A full Grade 1 (ages 6-7) English reading & phonics course — letters, sounds, sight words, " +
    "blending, and simple comprehension — written directly to the child in a warm classroom-teacher voice.");
cmd.Parameters.AddWithValue("@graphName", "Grade 1 Reading - Learning Graph");
cmd.Parameters.AddWithValue("@graphDescription",
    "Empty learning graph — lessons are added one at a time as chained nodes via the " +
    "AddCapabilityGraphNode endpoint (bootstrap script, 2026-07-26).");
var rows = await cmd.ExecuteNonQueryAsync();
Console.WriteLine($"Rows affected: {rows}");
Console.WriteLine($"CapabilityId: {capabilityId}");
Console.WriteLine($"CapabilityGraphId: {capabilityGraphId}");


