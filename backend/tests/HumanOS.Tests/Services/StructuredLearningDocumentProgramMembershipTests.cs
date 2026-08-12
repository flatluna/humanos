using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Models.Programs;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

public class StructuredLearningDocumentProgramMembershipTests
{
    [Fact]
    public async Task AttachAndListPrograms_WiresStructuredLearningDocumentToProgram() {
        var options = new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var ctx = new HumanOsDbContext(options))
        {
            var tenantId = Guid.NewGuid();
            var program = new LearningProgram
            {
                ProgramId = Guid.NewGuid(),
                Code = "PROG-1",
                Name = "Program 1",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            };

            var document = new StructuredLearningDocument
            {
                StructuredLearningDocumentId = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = Guid.NewGuid(),
                DocumentName = "Document 1",
                CreatedDate = DateTime.UtcNow,
            };

            ctx.Programs.Add(program);
            ctx.StructuredLearningDocuments.Add(document);
            await ctx.SaveChangesAsync();

            var service = new ProgramService(ctx);

            var attached = await service.AttachStructuredLearningDocumentAsync(program.ProgramId, document.StructuredLearningDocumentId);
            attached.Should().BeTrue();

            var memberships = await service.GetProgramsForStructuredLearningDocumentAsync(document.StructuredLearningDocumentId);
            memberships.Should().ContainSingle();
            memberships[0].ProgramId.Should().Be(program.ProgramId);

            var removed = await service.DetachStructuredLearningDocumentAsync(program.ProgramId, document.StructuredLearningDocumentId);
            removed.Should().BeTrue();
            (await service.GetProgramsForStructuredLearningDocumentAsync(document.StructuredLearningDocumentId)).Should().BeEmpty();
        }
    }
}
