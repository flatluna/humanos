using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Deletes a Structured Learning document and all content generated under it.
/// The document itself has cascade relationships to entities/connections, and
/// every section has cascade relationships to its steps, assessment rounds,
/// recall attempts, and question attempts. The only orphaned rows are the
/// section illustration metadata rows, which are removed explicitly here.
/// </summary>
public sealed class StructuredLearningDocumentDeletionService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningDocumentDeletionService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .AnyAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);

        if (!exists)
        {
            return false;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningIllustration WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId})", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningEntitySection WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId})", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.KnowledgeFactGraphPathSteps WHERE KnowledgeFactId IN (SELECT KnowledgeFactId FROM dbo.KnowledgeFacts WHERE EngramId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId}))", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningConnection WHERE SourceSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId}) OR SourceEntityId IN (SELECT StructuredLearningEntityId FROM dbo.StructuredLearningEntity WHERE StructuredLearningDocumentId = {documentId}) OR TargetEntityId IN (SELECT StructuredLearningEntityId FROM dbo.StructuredLearningEntity WHERE StructuredLearningDocumentId = {documentId}) OR StructuredLearningDocumentId = {documentId}", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningAssessmentQuestionAttempt WHERE StructuredLearningAssessmentRoundId IN (SELECT StructuredLearningAssessmentRoundId FROM dbo.StructuredLearningAssessmentRound WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId}))", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningAssessmentRound WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId})", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningRecallAttempt WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId})", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningPersonSectionState WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId})", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningHypothesisResponse WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId})", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningSectionStep WHERE StructuredLearningDocumentSectionId IN (SELECT StructuredLearningDocumentSectionId FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId})", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningGenerationUsage WHERE StructuredLearningDocumentId = {documentId}", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningDocumentEnrollment WHERE StructuredLearningDocumentId = {documentId}", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningDocumentProgramMembership WHERE StructuredLearningDocumentId = {documentId}", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningDocumentSection WHERE StructuredLearningDocumentId = {documentId}", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.StructuredLearningDocument WHERE StructuredLearningDocumentId = {documentId}", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return true;
    }
}
