using HumanOS.Tests;
using Microsoft.Extensions.Configuration;

namespace HumanOS;

/// <summary>
/// Ejecuta la prueba Curador → GraphArchitect
/// Se puede invocar desde Program.cs o desde un endpoint HTTP
/// </summary>
public static class RunTest
{
    public static async Task ExecuteAsync()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ Iniciando Prueba: Curador → GraphArchitect                   ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        // Construir configuration desde appsettings
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var test = new TestCuradorGraphArchitectFlow(config);
        await test.RunAsync();

        Console.WriteLine("\n✅ Prueba completada. Ver archivo de resultados para detalles.\n");
    }
}
