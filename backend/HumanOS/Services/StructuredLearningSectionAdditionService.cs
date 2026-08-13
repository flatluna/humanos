using Microsoft.Data.SqlTypes;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using SharpToken;

namespace HumanOS.Services;

/// <summary>
/// Structured Learning's "Edit mode" add-section feature (2026-08-11) —
/// mirrors Graph's node-addition flow (GraphNodeAdditionAgent +
/// ExperienceDesignerAgent, both run synchronously within one request):
/// generates a new section's content via
/// <see cref="StructuredLearningSectionAdditionAgent"/>, inserts it into
/// the target chapter (renumbering every later section's SortOrder to
/// make room), then immediately generates its 6 pedagogical steps by
/// reusing <see cref="StructuredLearningPedagogyService"/>'s own RunAsync
/// AS-IS — that service already only (re)processes sections whose
/// PedagogyStatus != Completed, so calling it here does the right thing
/// with zero duplicated generation logic.
/// </summary>
public sealed class StructuredLearningSectionAdditionService
{
    private readonly StructuredLearningSectionAdditionAgent _agent;
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly StructuredLearningPedagogyService _pedagogyService;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly GptEncoding _tokenEncoding = GptEncoding.GetEncoding("cl100k_base");

    public StructuredLearningSectionAdditionService(
        StructuredLearningSectionAdditionAgent agent,
        CapabilityEmbeddingService embeddingService,
        StructuredLearningPedagogyService pedagogyService,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _agent = agent;
        _embeddingService = embeddingService;
        _pedagogyService = pedagogyService;
        _dbContextFactory = dbContextFactory;
    }

    public bool IsConfigured => _agent.IsConfigured && _embeddingService.IsConfigured;

    /// <summary>Creates the new section, generates its 6 steps, and
    /// returns its id. The caller re-fetches the full section (with
    /// Steps) for the response — this method only creates/persists.
    ///
    /// <paramref name="requiresSectionIds"/>/<paramref name="requiredBySectionIds"/>
    /// mirror Graph's AddGraphNodeModal "Requires (prerequisites)"/"Is
    /// required by (dependents)" checklists, adapted to this pipeline's
    /// LINEAR chapter sequencing (no prerequisite graph to wire): checking a
    /// section as a prerequisite means the new section must come right
    /// after it; checking one as a dependent means the new section must
    /// come right before it. Both empty (the default) appends at the end of
    /// the chapter, unchanged from the original behavior.
    ///
    /// <paramref name="illustrationMode"/>/<paramref name="uploadedImageBytes"/>/
    /// <paramref name="uploadedImageContentType"/> mirror Graph's 4-way
    /// illustration mode choice — see
    /// StructuredLearningPedagogyService.GenerateIllustrationsAsync's doc
    /// comment for the exact behavior of each mode. <paramref name="referenceImageUsageInstruction"/>
    /// is a SEPARATE, dedicated instruction (only meaningful for
    /// "AiGeneratedFromUpload") describing HOW the reference image should
    /// appear in the generated scenes (e.g. "put our logo on the
    /// volunteers' t-shirts") — kept independent from <paramref name="instruction"/>
    /// (which only describes the section's CONTENT) so a professor doesn't
    /// have to remember to interleave image-usage guidance into the
    /// content prompt.</summary>
    public async Task<Guid> AddSectionAsync(
        Guid documentId,
        string chapterTitle,
        string instruction,
        string languageCode,
        IReadOnlyList<Guid> requiresSectionIds,
        IReadOnlyList<Guid> requiredBySectionIds,
        string illustrationMode,
        byte[]? uploadedImageBytes,
        string? uploadedImageContentType,
        string? referenceImageUsageInstruction,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var document = await dbContext.StructuredLearningDocuments.FindAsync([documentId], cancellationToken)
            ?? throw new InvalidOperationException($"No se encontró el documento '{documentId}'.");

        var chapterSections = await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentId == documentId && s.ChapterTitle == chapterTitle)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        if (chapterSections.Count == 0)
        {
            throw new InvalidOperationException($"No se encontraron secciones para el capítulo '{chapterTitle}'.");
        }

        var response = await _agent.GenerateAsync(
            document.DocumentName, chapterTitle, chapterSections.Select(s => s.SubchapterTitle).ToList(),
            instruction, document.PedagogicalAudience, cancellationToken);

        var embedding = await _embeddingService.GenerateEmbeddingAsync(response.Text, cancellationToken);
        var tokenCount = _tokenEncoding.Encode(response.Text).Count;

        // Position within the chapter: a checked "requires" section pins the
        // new one right after it; a checked "is required by" section pins it
        // right before. With neither checked, keep the original default of
        // appending at the very end of the chapter.
        var requiresSections = chapterSections.Where(s => requiresSectionIds.Contains(s.StructuredLearningDocumentSectionId)).ToList();
        var requiredBySections = chapterSections.Where(s => requiredBySectionIds.Contains(s.StructuredLearningDocumentSectionId)).ToList();

        int newSortOrder;
        if (requiredBySections.Count > 0)
        {
            newSortOrder = requiredBySections.Min(s => s.SortOrder);
            if (requiresSections.Count > 0 && requiresSections.Max(s => s.SortOrder) >= newSortOrder)
            {
                throw new InvalidOperationException(
                    "Los prerrequisitos y dependientes elegidos se contradicen — un prerrequisito no puede quedar después de un dependiente.");
            }
        }
        else if (requiresSections.Count > 0)
        {
            newSortOrder = requiresSections.Max(s => s.SortOrder) + 1;
        }
        else
        {
            newSortOrder = chapterSections[^1].SortOrder + 1;
        }

        // Shift every section (any chapter) from the insertion point onward
        // forward by one to make room — same simplicity as a professor
        // inserting a new subsection into a real table of contents.
        var laterSections = await dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentId == documentId && s.SortOrder >= newSortOrder)
            .ToListAsync(cancellationToken);
        foreach (var later in laterSections)
        {
            later.SortOrder += 1;
        }

        var newSection = new StructuredLearningDocumentSection
        {
            StructuredLearningDocumentSectionId = Guid.NewGuid(),
            StructuredLearningDocumentId = documentId,
            TenantId = document.TenantId,
            SortOrder = newSortOrder,
            ChapterTitle = chapterTitle,
            Capability = string.IsNullOrWhiteSpace(response.Capability) ? $"The learner can engage with this topic in the {chapterTitle} chapter." : response.Capability,
            SubchapterTitle = response.SubchapterTitle,
            Text = response.Text,
            TotalCharacters = response.Text.Length,
            TotalTokens = tokenCount,
            Embedding = new SqlVector<float>(embedding),
            PedagogyStatus = StructuredLearningPedagogyStatus.NotStarted,
            CreatedDate = DateTime.UtcNow
        };
        dbContext.StructuredLearningDocumentSections.Add(newSection);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Reuses the Step 3 pipeline as-is — only the new (NotStarted)
        // section gets processed, every already-Completed section is
        // left untouched.
        await _pedagogyService.RunAsync(
            documentId, languageCode, (_, _, _, _) => { }, cancellationToken,
            illustrationOverride: new SectionIllustrationOverride
            {
                SectionId = newSection.StructuredLearningDocumentSectionId,
                Mode = illustrationMode,
                UploadedImageBytes = uploadedImageBytes,
                UploadedImageContentType = uploadedImageContentType,
                ReferenceImageUsageInstruction = referenceImageUsageInstruction
            });

        return newSection.StructuredLearningDocumentSectionId;
    }
}
