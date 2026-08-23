using System.Text;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class StructuredLearningChatHistoryEntry
{
    /// <summary>"Student" or "Agent".</summary>
    public string Role { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>A cited Engram, resolved from the agent's UsedEngramIndices
/// against the retrieval package's own Engrams so the frontend can show
/// its full content in a modal and link straight to
/// /structured-learning/{DocumentId}/course/sections/{EngramId}.</summary>
public sealed class EngramReference
{
    public Guid EngramId { get; set; }

    public Guid DocumentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed class StructuredLearningChatResult
{
    public string Message { get; set; } = string.Empty;

    public List<EngramReference> UsedEngrams { get; set; } = [];

    public StructuredLearningRetrievalPackage RetrievalPackage { get; set; } = null!;
}

/// <summary>
/// Orchestrates one turn of the /ai-agents chat window (2026-08-18): pulls
/// a retrieval package from StructuredLearningKnowledgeRetrievalService
/// (exact/alias/semantic entity match -&gt; linked Engrams/Schemas -&gt; one
/// hop of related entities/Engrams), builds a grounded prompt from it plus
/// the conversation history, and calls StructuredLearningTutorAgent.
/// Stateless — the caller (frontend) owns/resends the conversation
/// history each turn, same convention as TutorService's History field.
/// </summary>
public sealed class StructuredLearningChatService
{
    private readonly StructuredLearningKnowledgeRetrievalService _retrieval;
    private readonly StructuredLearningTutorAgent _tutorAgent;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningChatService(
        StructuredLearningKnowledgeRetrievalService retrieval,
        StructuredLearningTutorAgent tutorAgent,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _retrieval = retrieval;
        _tutorAgent = tutorAgent;
        _dbContextFactory = dbContextFactory;
    }

    public bool IsConfigured => _tutorAgent.IsConfigured;

    public async Task<StructuredLearningChatResult> AskAsync(
        Guid tenantId,
        string userMessage,
        IReadOnlyList<StructuredLearningChatHistoryEntry> history,
        CancellationToken cancellationToken,
        Guid? subjectId = null,
        Guid? topicId = null,
        Guid? documentId = null,
        Guid? personId = null)
    {
        if (tenantId == Guid.Empty && documentId is Guid scopedDocumentId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            tenantId = await context.StructuredLearningDocuments
                .Where(d => d.StructuredLearningDocumentId == scopedDocumentId && d.IsGlobal && d.IsPublished)
                .Select(d => d.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
            if (tenantId == Guid.Empty)
            {
                throw new InvalidOperationException("The requested course is not available in the student catalog.");
            }
        }

        // Topic-level agent (no documentId — e.g. "Matemáticas/Álgebra"):
        // entities are scoped by TenantId (see
        // StructuredLearningKnowledgeRetrievalService.BuildRetrievalPackageAsync),
        // so an unauthenticated/tenant-less caller still needs a real
        // TenantId resolved from the Topic itself, same idea as the
        // documentId path above.
        if (tenantId == Guid.Empty && topicId is Guid scopedTopicId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            tenantId = await context.StructuredLearningTopics
                .Where(t => t.StructuredLearningTopicId == scopedTopicId)
                .Select(t => t.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
            if (tenantId == Guid.Empty)
            {
                throw new InvalidOperationException("The requested topic is not available in the student catalog.");
            }
        }

        var studentContext = await BuildStudentContextAsync(personId, documentId, subjectId, topicId, cancellationToken);
        var package = await _retrieval.BuildRetrievalPackageAsync(
            tenantId, userMessage, cancellationToken, subjectId, topicId, documentId);

        var prompt = BuildPrompt(package, history, userMessage, studentContext);
        var answer = await _tutorAgent.AnswerAsync(prompt, cancellationToken);

        var allEngrams = package.RetrievedEngrams.Concat(package.RelatedEngrams).ToList();
        var usedEngrams = answer.UsedEngramIndices
            .Where(index => index >= 1 && index <= allEngrams.Count)
            .Select(index => allEngrams[index - 1])
            .Select(e => new EngramReference { EngramId = e.EngramId, DocumentId = e.SchemaId, Title = e.Title, CourseName = e.SchemaName, Text = e.Text })
            .ToList();

        return new StructuredLearningChatResult
        {
            Message = answer.Message,
            UsedEngrams = usedEngrams,
            RetrievalPackage = package
        };
    }

    private static string BuildPrompt(
        StructuredLearningRetrievalPackage package, IReadOnlyList<StructuredLearningChatHistoryEntry> history, string userMessage, string studentContext)
    {
        var sb = new StringBuilder();

        sb.AppendLine("STUDENT CONTEXT (use only these facts; address the student by name when natural)");
        sb.AppendLine(studentContext);

        sb.AppendLine("RETRIEVAL PACKAGE");

        if (package.MatchedEntities.Count > 0)
        {
            sb.AppendLine("Matched entities:");
            foreach (var entity in package.MatchedEntities)
            {
                sb.AppendLine($"- {entity.CanonicalName} ({entity.EntityType}) [{entity.MatchType} match]");
            }
        }

        var nextIndex = 1;
        nextIndex = AppendEngrams(sb, "Retrieved Engrams (directly matched)", package.RetrievedEngrams, nextIndex);
        AppendEngrams(sb, "Related Engrams (one hop away)", package.RelatedEngrams, nextIndex);

        if (package.MatchedEntities.Count == 0 && package.RetrievedEngrams.Count == 0)
        {
            sb.AppendLine("(No relevant material was found in the course catalog for this query.)");
        }

        if (history.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("CONVERSATION HISTORY");
            foreach (var entry in history)
            {
                sb.AppendLine($"{entry.Role}: {entry.Text}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("STUDENT'S NEW QUESTION");
        sb.AppendLine(userMessage);

        return sb.ToString();
    }

    private async Task<string> BuildStudentContextAsync(Guid? personId, Guid? documentId, Guid? subjectId, Guid? topicId, CancellationToken cancellationToken)
    {
        if (personId is null) return "Student identity/progress unavailable. Do not invent a name or progress.";

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.PersonProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        var personName = !string.IsNullOrWhiteSpace(profile?.DisplayName) ? profile.DisplayName : $"{profile?.FirstName} {profile?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(personName)) personName = "student";

        var documentIds = await db.StructuredLearningDocumentEnrollments.AsNoTracking()
            .Where(e => e.PersonId == personId)
            .Select(e => e.StructuredLearningDocumentId)
            .ToListAsync(cancellationToken);

        var documents = db.StructuredLearningDocuments.AsNoTracking().Where(d => documentIds.Contains(d.StructuredLearningDocumentId));
        if (subjectId is not null) documents = documents.Where(d => d.SubjectId == subjectId);
        if (topicId is not null) documents = documents.Where(d => d.TopicId == topicId);

        var scopeDocumentIds = await documents.Select(d => d.StructuredLearningDocumentId).ToListAsync(cancellationToken);
        var sectionIds = await db.StructuredLearningDocumentSections.AsNoTracking()
            .Where(s => scopeDocumentIds.Contains(s.StructuredLearningDocumentId))
            .Select(s => s.StructuredLearningDocumentSectionId)
            .ToListAsync(cancellationToken);
        var passedRounds = await db.StructuredLearningAssessmentRounds.AsNoTracking()
            .Where(r => r.PersonId == personId && sectionIds.Contains(r.StructuredLearningDocumentSectionId) && r.Status == StructuredLearningAssessmentRoundStatus.Passed)
            .GroupBy(r => r.StructuredLearningDocumentSectionId)
            .Select(g => new { SectionId = g.Key, Score = g.OrderByDescending(r => r.RoundNumber).Select(r => r.FinalScore).FirstOrDefault() })
            .ToListAsync(cancellationToken);

        var currentSectionIds = documentId is null
            ? []
            : await db.StructuredLearningDocumentSections.AsNoTracking()
                .Where(s => s.StructuredLearningDocumentId == documentId)
                .Select(s => s.StructuredLearningDocumentSectionId)
                .ToListAsync(cancellationToken);
        var currentCompleted = documentId is null ? 0 : passedRounds.Count(r => currentSectionIds.Contains(r.SectionId));
        var currentTotal = documentId is null ? 0 : await db.StructuredLearningDocumentSections.CountAsync(s => s.StructuredLearningDocumentId == documentId, cancellationToken);
        var scores = passedRounds.Where(r => r.Score.HasValue).Select(r => r.Score!.Value).ToList();
        var scoreText = scores.Count == 0 ? "sin score todavía" : $"score promedio {Math.Round(scores.Average())}/100";

        return $"Student name: {personName}. Current course progress: {currentCompleted}/{currentTotal} engrams completed, {scoreText}. Domain/subdomain scope progress: {passedRounds.Count}/{sectionIds.Count} engrams completed.";
    }

    private static int AppendEngrams(StringBuilder sb, string label, List<RetrievedEngramDto> engrams, int startIndex)
    {
        if (engrams.Count == 0)
        {
            return startIndex;
        }

        sb.AppendLine();
        sb.AppendLine($"{label}:");
        var index = startIndex;
        foreach (var engram in engrams)
        {
            sb.AppendLine($"### [Engram {index}] {engram.Title} (course: {engram.SchemaName})");
            sb.AppendLine(engram.Text);
            index++;
        }

        return index;
    }
}
