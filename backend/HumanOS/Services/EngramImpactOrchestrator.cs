using HumanOS.Agents;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Orchestrator service responsible for retrieving or generating personalized Engram impact scores.
/// Implements cache-first resolution to prevent unnecessary AI calls when valid scores already exist.
/// </summary>
public sealed class EngramImpactOrchestrator
{
    private readonly HumanOsDbContext _dbContext;
    private readonly EmployeeScoringProfileService _profileService;
    private readonly EngramImpactScoringAgent _scoringAgent;
    private readonly EngramImpactScorePersistenceService _persistenceService;

    public EngramImpactOrchestrator(
        HumanOsDbContext dbContext,
        EmployeeScoringProfileService profileService,
        EngramImpactScoringAgent scoringAgent,
        EngramImpactScorePersistenceService persistenceService)
    {
        _dbContext = dbContext;
        _profileService = profileService;
        _scoringAgent = scoringAgent;
        _persistenceService = persistenceService;
    }

    /// <summary>
    /// Gets existing impact scores from cache/database or orchestrates the full generation workflow
    /// (Profile -> Course Engrams -> AI Scoring -> Persistence -> Result).
    /// </summary>
    public async Task<List<EngramImpactScore>> GetOrGenerateScoresAsync(
        Guid employeeId,
        Guid courseId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        // 1. Cache-hit path: If forceRefresh is false, check if scores already exist for this (EmployeeId, CourseId)
        if (!forceRefresh)
        {
            var existingScores = await _dbContext.EngramImpactScores
                .AsNoTracking()
                .Where(s => s.EmployeeId == employeeId && s.CourseId == courseId)
                .ToListAsync(cancellationToken);

            if (existingScores.Count > 0)
            {
                // Cache hit - return directly without invoking AI
                return existingScores;
            }
        }

        // 2. Cache miss or forceRefresh == true: Run complete orchestration pipeline

        // Stage A: Build Employee Profile
        EmployeeScoringProfile profile;
        try
        {
            profile = await _profileService.BuildProfileAsync(employeeId, cancellationToken);
            Console.WriteLine("================================================================================");
            Console.WriteLine($"[EngramImpactOrchestrator] EMPLOYEE SCORING PROFILE (EmployeeId: {employeeId}):");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("================================================================================");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[EngramImpactOrchestrator] Failed during Profile Building stage for Employee '{employeeId}': {ex.Message}",
                ex);
        }

        // Stage B: Retrieve Course Engrams preserving curriculum order
        List<EngramInput> engrams;
        try
        {
            engrams = await GetCourseEngramsAsync(courseId, cancellationToken);
            if (engrams.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No engrams or learning nodes found for Course/Document '{courseId}'.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[EngramImpactOrchestrator] Failed during Course Engrams Retrieval stage for Course '{courseId}': {ex.Message}",
                ex);
        }

        // Stage C: Score Engrams with AI Agent
        List<EngramScoreResult> scoreResults;
        try
        {
            scoreResults = await _scoringAgent.ScoreEngramsAsync(profile, engrams, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[EngramImpactOrchestrator] Failed during AI Scoring stage for Employee '{employeeId}' and Course '{courseId}': {ex.Message}",
                ex);
        }

        // Stage D: Persist Scores via Upsert
        try
        {
            await _persistenceService.SaveScoresAsync(employeeId, courseId, scoreResults, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[EngramImpactOrchestrator] Failed during Persistence stage for Employee '{employeeId}' and Course '{courseId}': {ex.Message}",
                ex);
        }

        // Stage E: Re-read saved scores from the database to return persisted entities with IDs and Timestamps
        try
        {
            return await _dbContext.EngramImpactScores
                .AsNoTracking()
                .Where(s => s.EmployeeId == employeeId && s.CourseId == courseId)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[EngramImpactOrchestrator] Failed during Final Retrieval stage for Employee '{employeeId}' and Course '{courseId}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Reads the list of Engrams / learning nodes for a course from the database in strict curriculum order.
    /// Supports StructuredLearningDocuments, CapabilityGraphs, and Traditional Capability Chapters/Lessons.
    /// </summary>
    private async Task<List<EngramInput>> GetCourseEngramsAsync(
        Guid courseId,
        CancellationToken cancellationToken)
    {
        // 1. Check StructuredLearningDocumentSection (Engrams in Structured Learning courses)
        var sections = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentId == courseId)
            .OrderBy(s => s.SortOrder)
            .Select(s => new EngramInput
            {
                EngramId = s.StructuredLearningDocumentSectionId.ToString(),
                Title = !string.IsNullOrWhiteSpace(s.Capability) ? s.Capability
                      : (!string.IsNullOrWhiteSpace(s.SubchapterTitle) ? s.SubchapterTitle
                      : s.ChapterTitle)
            })
            .ToListAsync(cancellationToken);

        if (sections.Count > 0)
        {
            return sections;
        }

        // 2. Check CapabilityGraphNode (Nodes in Graph-based Capabilities)
        var nodes = await _dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .Where(n => n.CapabilityGraphId == courseId || (n.CapabilityGraph != null && n.CapabilityGraph.CapabilityId == courseId))
            .OrderBy(n => n.SortOrder)
            .Select(n => new EngramInput
            {
                EngramId = n.CapabilityGraphNodeId.ToString(),
                Title = n.Name
            })
            .ToListAsync(cancellationToken);

        if (nodes.Count > 0)
        {
            return nodes;
        }

        // 3. Check Traditional Capability Lessons
        var lessons = await _dbContext.CapabilityLessons
            .AsNoTracking()
            .Where(l => l.CapabilityChapter != null && l.CapabilityChapter.CapabilityId == courseId)
            .OrderBy(l => l.CapabilityChapter!.SortOrder)
            .ThenBy(l => l.SortOrder)
            .Select(l => new EngramInput
            {
                EngramId = l.CapabilityLessonId.ToString(),
                Title = l.Title
            })
            .ToListAsync(cancellationToken);

        return lessons;
    }
}
