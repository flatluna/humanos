using FluentAssertions;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class BuddyContextBuilderTests
{
    private static BuddyContextBuilder CreateBuilder()
    {
        var options = new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new HumanOsDbContext(options);
        return new BuddyContextBuilder(
            dbContext,
            new EmployeeScoringProfileService(dbContext),
            new PersonCqiProfileService(dbContext),
            new CapabilityEnrollmentService(dbContext, new GraphProgressionEngine()));
    }

    [Fact]
    public async Task SpanishInstructions_StartInSpanishAndAllowAdaptiveLanguageSwitching()
    {
        var builder = CreateBuilder();

        var instructions = await builder.BuildInstructionsAsync(
            employeeId: null, personName: "Ana", isEnglish: false, CancellationToken.None);

        instructions.Should().Contain("IDIOMA — REGLA ADAPTABLE")
            .And.Contain("habla SIEMPRE en el idioma que el empleado esté usando")
            .And.Contain("CONTEXTO EN OTRO IDIOMA")
            .And.Contain("¡Hola, Ana!")
            .And.NotContain("LANGUAGE — ADAPTIVE RULE")
            .And.NotContain("EMPLOYEE PROFILE");
    }

    [Fact]
    public async Task EnglishInstructions_StartInEnglishAndAllowAdaptiveLanguageSwitching()
    {
        var builder = CreateBuilder();

        var instructions = await builder.BuildInstructionsAsync(
            employeeId: null, personName: "Ana", isEnglish: true, CancellationToken.None);

        instructions.Should().Contain("LANGUAGE — ADAPTIVE RULE")
            .And.Contain("ALWAYS speak in whatever language the employee is")
            .And.Contain("MIXED-LANGUAGE CONTEXT")
            .And.Contain("Hi, Ana!")
            .And.NotContain("IDIOMA — REGLA ADAPTABLE")
            .And.NotContain("PERFIL DEL EMPLEADO");
    }
}
