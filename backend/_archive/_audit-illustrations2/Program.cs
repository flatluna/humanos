using Microsoft.Data.SqlClient;
using System.Text;

var output = new StringBuilder();
void Log(string s) { Console.WriteLine(s); output.AppendLine(s); }

const string connectionString = "Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Authentication=Active Directory Default;";

await using var conn = new SqlConnection(connectionString);
await conn.OpenAsync();
Console.WriteLine("Connected.");

// Overall split: deterministic vs generative.
await using (var cmd = new SqlCommand(@"
    SELECT
        CASE WHEN ImageModel = 'deterministic-skiasharp-v1' THEN 'deterministic' ELSE ImageModel END AS Model,
        COUNT(*) AS Cnt
    FROM CapabilityGraphNodeIllustrations
    GROUP BY CASE WHEN ImageModel = 'deterministic-skiasharp-v1' THEN 'deterministic' ELSE ImageModel END
    ORDER BY Cnt DESC", conn))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    Console.WriteLine("\n=== Overall ImageModel split ===");
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"{reader["Model"]}: {reader["Cnt"]}");
    }
}

// Generative illustrations whose Prompt text mentions coordinate-plane-
// specific vocabulary (NOT generic arithmetic word problems like "9 x 6 =
// 54 caramelos", which are legitimately fine as freeform images) — these
// are the ones that likely SHOULD have used MathDiagram instead.
await using (var cmd = new SqlCommand(@"
    SELECT
        cg.Name AS CapabilityName,
        n.CapabilityGraphNodeId,
        n.Name AS NodeName,
        i.CapabilityGraphNodeIllustrationId,
        i.Purpose,
        i.Prompt,
        i.CreatedDate
    FROM CapabilityGraphNodeIllustrations i
    JOIN CapabilityGraphNodes n ON n.CapabilityGraphNodeId = i.CapabilityGraphNodeId
    JOIN CapabilityGraphs cg ON cg.CapabilityGraphId = n.CapabilityGraphId
    WHERE i.ImageModel <> 'deterministic-skiasharp-v1'
      AND (
        i.Prompt LIKE '%plano cartesiano%'
        OR i.Prompt LIKE '%eje x%'
        OR i.Prompt LIKE '%eje y%'
        OR i.Prompt LIKE '%pendiente%'
        OR i.Prompt LIKE '%par%bola%'
        OR i.Prompt LIKE '%el%pse%'
        OR i.Prompt LIKE '%c%rculo%'
        OR i.Prompt LIKE '%circunferencia%'
        OR i.Prompt LIKE '%recta vertical%'
        OR i.Prompt LIKE '%y = %'
        OR i.Prompt LIKE '%y=%'
        OR i.Prompt LIKE '%x^2%'
        OR i.Prompt LIKE '%coordenada%'
        OR i.Prompt LIKE '%intersecci%n%'
        OR i.Prompt LIKE '%gr%fica de la funci%n%'
        OR i.Prompt LIKE '%gr%fica de la recta%'
      )
    ORDER BY cg.Name, n.Name, i.CreatedDate", conn))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    Console.WriteLine("\n=== Generative illustrations that look like coordinate-plane diagrams (heuristic) ===");
    var count = 0;
    while (await reader.ReadAsync())
    {
        count++;
        Console.WriteLine($"[{count}] Capability='{reader["CapabilityName"]}' Node='{reader["NodeName"]}' NodeId={reader["CapabilityGraphNodeId"]} IllustrationId={reader["CapabilityGraphNodeIllustrationId"]} Purpose={reader["Purpose"]}");
        Console.WriteLine($"    Prompt: {reader["Prompt"]}");
    }
    Console.WriteLine($"\nTotal heuristic matches: {count}");
}

// Grand total generative count (no heuristic filter) for context.
await using (var cmd = new SqlCommand(@"
    SELECT COUNT(*) FROM CapabilityGraphNodeIllustrations WHERE ImageModel <> 'deterministic-skiasharp-v1'", conn))
{
    var total = (int)(await cmd.ExecuteScalarAsync())!;
    Console.WriteLine($"\nGrand total generative (non-deterministic) illustrations in DB: {total}");
}
