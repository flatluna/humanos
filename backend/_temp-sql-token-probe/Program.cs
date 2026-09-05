using Azure.Identity;
using Microsoft.Data.SqlClient;

var credential = new AzureCliCredential();
var token = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(["https://database.windows.net/.default"]));
await using var connection = new SqlConnection("Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
connection.AccessToken = token.Token;
await connection.OpenAsync();

Console.WriteLine("=== ALEJANDRO PERSON & TENANT ===");
await using (var command = connection.CreateCommand())
{
	command.CommandText = "SELECT PersonId, TenantId, JobRoleId, Email FROM dbo.Person WHERE Email LIKE '%alejandro%'";
	await using var reader = await command.ExecuteReaderAsync();
	while (await reader.ReadAsync())
	{
		Console.WriteLine($"PersonId: {reader.GetGuid(0)}, TenantId: {reader.GetGuid(1)}, JobRoleId: {reader.GetGuid(2)}, Email: {reader.GetString(3)}");
	}
}

Console.WriteLine("=== SKILLS FOR THAT TENANT ===");
await using (var command = connection.CreateCommand())
{
	command.CommandText = "SELECT SkillId, Name, Category FROM dbo.Skill WHERE TenantId = '81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b' AND Name LIKE '%Prompt%'";
	await using var reader = await command.ExecuteReaderAsync();
	while (await reader.ReadAsync())
	{
		Console.WriteLine($"SkillId: {reader.GetGuid(0)}, Name: '{reader.GetString(1)}', Cat: '{reader.GetString(2)}'");
	}
}

Console.WriteLine("=== ENROLLED STRUCTURED LEARNING DOCS FOR ALEJANDRO ===");
await using (var command = connection.CreateCommand())
{
	command.CommandText = @"
SELECT e.PersonId, d.StructuredLearningDocumentId, d.DocumentName
FROM dbo.StructuredLearningDocumentEnrollment e
JOIN dbo.StructuredLearningDocument d ON e.StructuredLearningDocumentId = d.StructuredLearningDocumentId
WHERE e.PersonId = '562cb73a-414b-47a8-957e-9f1950ae9622'";
	await using var reader = await command.ExecuteReaderAsync();
	while (await reader.ReadAsync())
	{
		Console.WriteLine($"Enrolled in DocId: {reader.GetGuid(1)}, Name: '{reader.GetString(2)}'");
	}
}
