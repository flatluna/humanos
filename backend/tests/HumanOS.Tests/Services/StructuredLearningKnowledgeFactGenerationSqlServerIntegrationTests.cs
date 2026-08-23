using Azure.Core;
using Azure.Identity;
using HumanOS.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace HumanOS.Tests.Services;

/// <summary>
/// SQL Server integration fixture for Step 5. It is intentionally opt-in:
/// set HUMANOS_SQL_TEST_CONNECTION_STRING to an isolated test database. The
/// development HumanOS database is never selected implicitly.
/// </summary>
[Trait("Category", "Integration")]
public sealed class StructuredLearningKnowledgeFactGenerationSqlServerIntegrationTests
{
    [Fact]
    public async Task AzureSqlDevelopment_ConnectivityAndKnowledgeFactTablesAreAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("HUMANOS_SQL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // xUnit 2.6 has no runtime skip API; CI invokes this fixture only
            // when HUMANOS_SQL_TEST_CONNECTION_STRING is configured.
            return;
        }

        var credential = new AzureCliCredential();
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://database.windows.net/.default"]));
        await using var connection = new SqlConnection(connectionString);
        connection.AccessToken = token.Token;
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_NAME IN ('KnowledgeFacts', 'KnowledgeFactGraphPathSteps', 'StructuredLearningConnection', 'StructuredLearningDocumentSection')
            ORDER BY TABLE_NAME;
            """;
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        tables.Should().BeEquivalentTo([
            "KnowledgeFacts",
            "KnowledgeFactGraphPathSteps",
            "StructuredLearningConnection",
            "StructuredLearningDocumentSection"
        ]);
    }
}
