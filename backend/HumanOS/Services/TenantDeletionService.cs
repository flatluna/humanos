using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Per-table/category row counts (and blob counts) actually
/// removed by <see cref="TenantDeletionService.DeleteTenantAsync"/> — shown
/// to the reviewer as the "mega warning" deletion receipt. Global/shared
/// catalog content (Capability, CapabilityGraph, StructuredLearningDocument
/// and everything under them) is NEVER touched — only this tenant's own
/// data and its people's enrollment/session link rows into that content.</summary>
public sealed class TenantDeletionSummary
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Dictionary<string, int> DeletedRowsByTable { get; } = new();

    public int DeletedBlobCount { get; set; }

    public int TotalRowsDeleted => DeletedRowsByTable.Values.Sum();
}

/// <summary>Irreversibly deletes a Tenant and everything exclusively owned
/// by it (2026-10-01) — its people and every one of their records, its org
/// catalog (Departments/JobRoles/Skills/Certifications/Responsibilities/
/// Tools/Activities/ExperienceRequirements), its Film/Video Productions,
/// its invitations, and its logo. Deliberately does NOT delete shared
/// global content (Capability, CapabilityGraph, StructuredLearningDocument,
/// Programs, etc.) even if this tenant happens to be the one that
/// originally authored it — only the tenant's own people's enrollment/
/// session rows into that content are removed, per explicit product
/// decision (2026-10-01).</summary>
public sealed class TenantDeletionService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly TenantLogoStorageService _logoStorage;
    private readonly PersonProfilePhotoStorageService _photoStorage;
    private readonly SkillDocumentStorageService _skillDocumentStorage;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly VideoProductionOrchestrator _videoProductionOrchestrator;

    public TenantDeletionService(
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        TenantLogoStorageService logoStorage,
        PersonProfilePhotoStorageService photoStorage,
        SkillDocumentStorageService skillDocumentStorage,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        VideoProductionOrchestrator videoProductionOrchestrator)
    {
        _dbContextFactory = dbContextFactory;
        _logoStorage = logoStorage;
        _photoStorage = photoStorage;
        _skillDocumentStorage = skillDocumentStorage;
        _illustrationStorage = illustrationStorage;
        _videoProductionOrchestrator = videoProductionOrchestrator;
    }

    /// <summary>Requires the caller to pass the tenant's own Slug as
    /// <paramref name="confirmationSlug"/> (case-insensitive) — the "mega
    /// warning" gate: the frontend must make the reviewer type the
    /// company's slug/name before this is even called, and the backend
    /// re-checks it so no UI bug can trigger an accidental delete.</summary>
    public async Task<TenantDeletionSummary> DeleteTenantAsync(
        Guid tenantId, string confirmationSlug, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        if (!string.Equals(tenant.Slug, confirmationSlug, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Confirmation text does not match this tenant's slug. Nothing was deleted.");
        }

        var summary = new TenantDeletionSummary { TenantId = tenantId, TenantName = tenant.Name };

        // ---- Blobs first (best-effort, never blocks the SQL delete) ----
        await DeleteBlobsAsync(dbContext, tenant, summary, cancellationToken);

        // ---- SQL rows, deepest children first, inside one transaction ----
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var personIdsSql = "(SELECT PersonId FROM Person WHERE TenantId = {0})";

        async Task ExecAsync(string label, string sql)
        {
            try
            {
                var rows = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                summary.DeletedRowsByTable[label] = rows;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Tenant deletion failed while deleting '{label}': {ex.Message}", ex);
            }
        }

        // Learning runtime (deepest first) — table names here are PLURAL
        // (LearningSessions/LearningSessionNodes/LearningSessionSteps/
        // LearningEvidences/LearningAssessmentResults/AssessmentRounds/
        // AssessmentQuestions), per their EF Core Fluent ToTable() overrides —
        // do NOT "fix" these to singular.
        await ExecAsync("LearningAssessmentResult",
            $"DELETE r FROM LearningAssessmentResults r INNER JOIN LearningSessionNodes n ON r.LearningSessionNodeId = n.LearningSessionNodeId INNER JOIN LearningSessions s ON n.LearningSessionId = s.LearningSessionId WHERE s.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("AssessmentQuestion",
            $"DELETE q FROM AssessmentQuestions q INNER JOIN AssessmentRounds ar ON q.AssessmentRoundId = ar.AssessmentRoundId INNER JOIN LearningSessionNodes n ON ar.LearningSessionNodeId = n.LearningSessionNodeId INNER JOIN LearningSessions s ON n.LearningSessionId = s.LearningSessionId WHERE s.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("AssessmentRound",
            $"DELETE ar FROM AssessmentRounds ar INNER JOIN LearningSessionNodes n ON ar.LearningSessionNodeId = n.LearningSessionNodeId INNER JOIN LearningSessions s ON n.LearningSessionId = s.LearningSessionId WHERE s.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("LearningEvidence",
            $"DELETE e FROM LearningEvidences e INNER JOIN LearningSessionSteps st ON e.LearningSessionStepId = st.LearningSessionStepId INNER JOIN LearningSessionNodes n ON st.LearningSessionNodeId = n.LearningSessionNodeId INNER JOIN LearningSessions s ON n.LearningSessionId = s.LearningSessionId WHERE s.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("LearningSessionStep",
            $"DELETE st FROM LearningSessionSteps st INNER JOIN LearningSessionNodes n ON st.LearningSessionNodeId = n.LearningSessionNodeId INNER JOIN LearningSessions s ON n.LearningSessionId = s.LearningSessionId WHERE s.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("LearningSessionNode",
            $"DELETE n FROM LearningSessionNodes n INNER JOIN LearningSessions s ON n.LearningSessionId = s.LearningSessionId WHERE s.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("LearningSession",
            $"DELETE FROM LearningSessions WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Structured Learning (link rows / per-student runtime state only — never the document/sections themselves)
        await ExecAsync("StructuredLearningAssessmentQuestionAttempt",
            $"DELETE a FROM StructuredLearningAssessmentQuestionAttempt a INNER JOIN StructuredLearningAssessmentRound r ON a.StructuredLearningAssessmentRoundId = r.StructuredLearningAssessmentRoundId WHERE r.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("StructuredLearningAssessmentRound",
            $"DELETE FROM StructuredLearningAssessmentRound WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("StructuredLearningRecallAttempt",
            $"DELETE FROM StructuredLearningRecallAttempt WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("StructuredLearningHypothesisResponse",
            $"DELETE FROM StructuredLearningHypothesisResponse WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("StructuredLearningPersonSectionState",
            $"DELETE FROM StructuredLearningPersonSectionState WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("StructuredLearningDocumentEnrollment",
            $"DELETE FROM StructuredLearningDocumentEnrollment WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Capability (enrollment/evidence link rows only — never Capability/CapabilityGraph themselves)
        // RecallAttempt/CapabilityPractice/CapabilityEvidence link via PersonCapabilityId, not PersonId directly.
        await ExecAsync("RecallAttempt",
            $"DELETE r FROM RecallAttempt r INNER JOIN PersonCapability pc ON r.PersonCapabilityId = pc.PersonCapabilityId WHERE pc.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("CapabilityPractice",
            $"DELETE c FROM CapabilityPractice c INNER JOIN PersonCapability pc ON c.PersonCapabilityId = pc.PersonCapabilityId WHERE pc.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("CapabilityEvidence",
            $"DELETE c FROM CapabilityEvidence c INNER JOIN PersonCapability pc ON c.PersonCapabilityId = pc.PersonCapabilityId WHERE pc.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("CapabilityEnrollment",
            $"DELETE FROM CapabilityEnrollment WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonCapability",
            $"DELETE FROM PersonCapability WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("ProgramEnrollment",
            $"DELETE FROM ProgramEnrollment WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Practice / assessments / evidence
        await ExecAsync("PracticeAttempt",
            $"DELETE a FROM PracticeAttempt a INNER JOIN PracticeSession s ON a.PracticeSessionId = s.PracticeSessionId WHERE s.PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PracticeSession",
            $"DELETE FROM PracticeSession WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("AssessmentAttempt",
            $"DELETE FROM AssessmentAttempt WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("Evidence",
            $"DELETE FROM Evidence WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("EngramReviewCompletion",
            $"DELETE FROM EngramReviewCompletions WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("EngramImpactScore",
            $"DELETE FROM EngramImpactScore WHERE EmployeeId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Goals / projects / motivations
        await ExecAsync("PersonGoal",
            $"DELETE FROM PersonGoal WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonProject",
            $"DELETE FROM PersonProject WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonMotivation",
            $"DELETE FROM PersonMotivation WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Gamification
        await ExecAsync("PlayerBadge",
            $"DELETE FROM PlayerBadge WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("RewardLedgerEntry",
            $"DELETE FROM RewardLedgerEntry WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PlayerProfile",
            $"DELETE FROM PlayerProfile WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Saved searches / growth / agent chat
        await ExecAsync("GrowWithUsSavedSearch",
            $"DELETE FROM GrowWithUsSavedSearch WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("ContentRecommendationSavedSearch",
            $"DELETE FROM ContentRecommendationSavedSearch WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("GrowthAction",
            $"DELETE FROM GrowthAction WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("AgentMessage",
            $"DELETE FROM AgentMessage WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonCurrentSituation",
            $"DELETE FROM PersonCurrentSituation WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonFutureDirection",
            $"DELETE FROM PersonFutureDirection WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonStartingPoint",
            $"DELETE FROM PersonStartingPoint WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("HumanState",
            $"DELETE FROM HumanState WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("HumanProfile",
            $"DELETE FROM HumanProfile WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Person's own profile/resume/job-description/career records
        await ExecAsync("PersonProfile.CurrentJobDescriptionId (cleared)",
            $"UPDATE PersonProfile SET CurrentJobDescriptionId = NULL WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("JobDescriptionRecord",
            $"DELETE FROM JobDescription WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonCareerPlan",
            $"DELETE FROM PersonCareerPlan WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonAcademicPreferences",
            $"DELETE FROM PersonAcademicPreferences WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonCqiProfile",
            $"DELETE FROM PersonCqiProfile WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonResume",
            $"DELETE FROM PersonResume WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");
        await ExecAsync("PersonProfile",
            $"DELETE FROM PersonProfile WHERE PersonId IN {string.Format(personIdsSql, $"'{tenantId}'")}");

        // Tenant invitations
        await ExecAsync("TenantInvitation", $"DELETE FROM TenantInvitation WHERE TenantId = '{tenantId}'");

        // Film Productions (DB rows only here — blobs already removed above)
        await ExecAsync("FilmScene",
            $"DELETE sc FROM FilmScene sc INNER JOIN FilmProduction p ON sc.FilmProductionId = p.FilmProductionId WHERE p.TenantId = '{tenantId}'");
        await ExecAsync("FilmProduction", $"DELETE FROM FilmProduction WHERE TenantId = '{tenantId}'");
        // Video Productions: rows + blobs already fully removed by
        // VideoProductionOrchestrator.DeleteProductionAsync in DeleteBlobsAsync above.

        // People themselves
        await ExecAsync("Person", $"DELETE FROM Person WHERE TenantId = '{tenantId}'");

        // Org catalog join tables, then catalog rows, then JobRole/Department
        await ExecAsync("JobRoleSkill",
            $"DELETE jrs FROM JobRoleSkill jrs INNER JOIN JobRole jr ON jrs.JobRoleId = jr.JobRoleId WHERE jr.TenantId = '{tenantId}'");
        await ExecAsync("JobRoleActivity",
            $"DELETE jra FROM JobRoleActivity jra INNER JOIN JobRole jr ON jra.JobRoleId = jr.JobRoleId WHERE jr.TenantId = '{tenantId}'");
        await ExecAsync("JobRoleResponsibility",
            $"DELETE jrr FROM JobRoleResponsibility jrr INNER JOIN JobRole jr ON jrr.JobRoleId = jr.JobRoleId WHERE jr.TenantId = '{tenantId}'");
        await ExecAsync("JobRoleExperienceRequirement",
            $"DELETE jre FROM JobRoleExperienceRequirement jre INNER JOIN JobRole jr ON jre.JobRoleId = jr.JobRoleId WHERE jr.TenantId = '{tenantId}'");
        await ExecAsync("JobRoleCertification",
            $"DELETE jrc FROM JobRoleCertification jrc INNER JOIN JobRole jr ON jrc.JobRoleId = jr.JobRoleId WHERE jr.TenantId = '{tenantId}'");
        await ExecAsync("JobRoleTool",
            $"DELETE jrt FROM JobRoleTool jrt INNER JOIN JobRole jr ON jrt.JobRoleId = jr.JobRoleId WHERE jr.TenantId = '{tenantId}'");
        await ExecAsync("DepartmentJobRole",
            $"DELETE djr FROM DepartmentJobRole djr INNER JOIN Department d ON djr.DepartmentId = d.DepartmentId WHERE d.TenantId = '{tenantId}'");

        await ExecAsync("Skill", $"DELETE FROM Skill WHERE TenantId = '{tenantId}'");
        await ExecAsync("Certification", $"DELETE FROM Certification WHERE TenantId = '{tenantId}'");
        await ExecAsync("Responsibility", $"DELETE FROM Responsibility WHERE TenantId = '{tenantId}'");
        await ExecAsync("Tool", $"DELETE FROM Tool WHERE TenantId = '{tenantId}'");
        await ExecAsync("Activity", $"DELETE FROM Activity WHERE TenantId = '{tenantId}'");
        await ExecAsync("ExperienceRequirement", $"DELETE FROM ExperienceRequirement WHERE TenantId = '{tenantId}'");

        // JobRole self-reference (ReportsToJobRoleId) must be cleared before deleting the rows
        await ExecAsync("JobRole.ReportsToJobRoleId (cleared)",
            $"UPDATE JobRole SET ReportsToJobRoleId = NULL WHERE TenantId = '{tenantId}'");
        await ExecAsync("JobRole", $"DELETE FROM JobRole WHERE TenantId = '{tenantId}'");

        // Department self-reference (ParentDepartmentId) must be cleared before deleting the rows
        await ExecAsync("Department.ParentDepartmentId (cleared)",
            $"UPDATE Department SET ParentDepartmentId = NULL WHERE TenantId = '{tenantId}'");
        await ExecAsync("Department", $"DELETE FROM Department WHERE TenantId = '{tenantId}'");

        // Finally, the tenant itself
        await ExecAsync("Tenant", $"DELETE FROM Tenant WHERE TenantId = '{tenantId}'");

        await transaction.CommitAsync(cancellationToken);

        return summary;
    }

    private async Task DeleteBlobsAsync(
        HumanOsDbContext dbContext, Models.Tenancy.Tenant tenant, TenantDeletionSummary summary, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;

        try
        {
            if (await _logoStorage.DeleteLogoAsync(tenantId, cancellationToken))
            {
                summary.DeletedBlobCount++;
            }
        }
        catch (Exception) when (true) { /* best-effort, never blocks */ }

        try
        {
            var skillDocPaths = await dbContext.Skills
                .Where(s => s.TenantId == tenantId && s.DocumentStoragePath != null)
                .Select(s => s.DocumentStoragePath!)
                .ToListAsync(cancellationToken);
            foreach (var path in skillDocPaths)
            {
                await _skillDocumentStorage.DeleteSkillDocumentAsync(tenantId, path, cancellationToken);
                summary.DeletedBlobCount++;
            }
        }
        catch (Exception) when (true) { /* best-effort */ }

        var personIds = await dbContext.People
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.PersonId)
            .ToListAsync(cancellationToken);

        foreach (var personId in personIds)
        {
            try
            {
                summary.DeletedBlobCount += await _photoStorage.DeletePhotosAsync(tenantId, personId, cancellationToken);
            }
            catch (Exception) when (true) { /* best-effort */ }
        }

        var filmProductionIds = await dbContext.FilmProductions
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.FilmProductionId)
            .ToListAsync(cancellationToken);

        foreach (var filmProductionId in filmProductionIds)
        {
            var scenes = await dbContext.FilmScenes
                .Where(s => s.FilmProductionId == filmProductionId)
                .ToListAsync(cancellationToken);
            foreach (var scene in scenes)
            {
                foreach (var path in new[] { scene.AnchorImageStoragePath, scene.FinalImageStoragePath, scene.FinalVideoStoragePath })
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    try { await _illustrationStorage.DeleteIllustrationAsync(path, cancellationToken); summary.DeletedBlobCount++; }
                    catch (Exception) when (true) { /* best-effort */ }
                }
            }
        }

        var videoProductionIds = await dbContext.VideoProductions
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.VideoProductionId)
            .ToListAsync(cancellationToken);

        foreach (var videoProductionId in videoProductionIds)
        {
            try
            {
                await _videoProductionOrchestrator.DeleteProductionAsync(videoProductionId, cancellationToken);
                summary.DeletedBlobCount++;
            }
            catch (Exception) when (true) { /* best-effort — row deletion below is the source of truth */ }
        }
    }
}
