using Azure.Identity;
using Microsoft.Data.SqlClient;

var credential = new AzureCliCredential();
var token = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(["https://database.windows.net/.default"]));
await using var connection = new SqlConnection("Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
connection.AccessToken = token.Token;
await connection.OpenAsync();
await using (var command = connection.CreateCommand())
{
	command.CommandText = "SELECT DB_NAME(), SUSER_SNAME()";
	await using var reader = await command.ExecuteReaderAsync();
	await reader.ReadAsync();
	Console.WriteLine($"Database={reader.GetString(0)}");
	Console.WriteLine($"Login={reader.GetString(1)}");
}

await using (var command = connection.CreateCommand())
{
	command.CommandText = "SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '%KnowledgeFact%' ORDER BY MigrationId";
	await using var reader = await command.ExecuteReaderAsync();
	while (await reader.ReadAsync()) Console.WriteLine($"Migration={reader.GetString(0)}");
}

await using (var command = connection.CreateCommand())
{
	command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME IN ('KnowledgeFacts','KnowledgeFactGraphPathSteps','StructuredLearningConnection','StructuredLearningDocumentSection') ORDER BY TABLE_NAME";
	await using var reader = await command.ExecuteReaderAsync();
	while (await reader.ReadAsync()) Console.WriteLine($"Table={reader.GetString(0)}");
}
