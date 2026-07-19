using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;

namespace HumanOS.Services;

/// <summary>
/// PASO 3 persistence layer: takes the structured output of
/// ExperienceDesignerAgent (a <see cref="NodeExperienceBlueprintResponse"/>,
/// still in-memory) and persists it as real rows in SQL:
///
///   NodeExperienceBlueprint
///   NodeExperienceBlueprintStep (x5 — Hypothesis, Teaching, Recall, Production, Assessment)
///
/// The fixed Memory Paradox order is enforced HERE (not trusted from the LLM
/// response order): steps are always written with SortOrder/StepType
/// matching <see cref="ExperienceStepType"/>'s canonical 0-4 sequence.
/// Illustration references are resolved from 1-based prompt indexes back to
/// real CapabilityGraphNodeIllustrationId values — no new illustration rows
/// or blobs are ever created here.
/// </summary>
public sealed class NodeExperienceBlueprintPersistenceService
{
    private static readonly ExperienceStepType[] CanonicalStepOrder =
    [
        ExperienceStepType.Hypothesis,
        ExperienceStepType.Teaching,
        ExperienceStepType.Recall,
        ExperienceStepType.Production,
        ExperienceStepType.Assessment
    ];

    /// <summary>
    /// Persists a NodeExperienceBlueprint (with its 5 steps) for the given node.
    /// </summary>
    /// <param name="dbContext">DbContext to write with (caller owns its lifetime).</param>
    /// <param name="capabilityGraphNodeId">The node this blueprint teaches.</param>
    /// <param name="response">The agent's structured output.</param>
    /// <param name="availableIllustrations">
    /// The SAME ordered list (1-based index) passed to the agent, so
    /// <see cref="NodeExperienceBlueprintStepDto.IllustrationIndexes"/> can be
    /// resolved back to real <see cref="CapabilityGraphNodeIllustration"/> IDs.
    /// </param>
    public async Task<NodeExperienceBlueprint> PersistAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        NodeExperienceBlueprintResponse response,
        IReadOnlyList<CapabilityGraphNodeIllustration> availableIllustrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(response);

        var blueprintEntity = new NodeExperienceBlueprint
        {
            NodeExperienceBlueprintId = response.NodeExperienceBlueprintId != Guid.Empty ? response.NodeExperienceBlueprintId : Guid.NewGuid(),
            CapabilityGraphNodeId = capabilityGraphNodeId,
            Name = response.Name,
            Description = response.Description,
            Version = 1,
            CreatedDate = DateTime.UtcNow
        };

        foreach (var stepType in CanonicalStepOrder)
        {
            var stepDto = response.Steps.FirstOrDefault(s => s.StepType == stepType);
            if (stepDto is null)
            {
                // Agent omitted this step type — skip rather than persist an
                // empty/fabricated one; the caller's validation summary
                // surfaces this as "missing step" for visibility.
                continue;
            }

            var referencedIllustrationIds = stepDto.IllustrationIndexes
                .Where(idx => idx >= 1 && idx <= availableIllustrations.Count)
                .Select(idx => availableIllustrations[idx - 1])
                .Where(illustration => MatchesStepPurpose(stepType, illustration.Purpose))
                .Select(illustration => illustration.CapabilityGraphNodeIllustrationId)
                .ToList();

            blueprintEntity.Steps.Add(new NodeExperienceBlueprintStep
            {
                NodeExperienceBlueprintStepId = Guid.NewGuid(),
                NodeExperienceBlueprintId = blueprintEntity.NodeExperienceBlueprintId,
                StepType = stepType,
                Content = stepDto.Content,
                ReferencedIllustrationIdsJson = JsonSerializer.Serialize(referencedIllustrationIds),
                SortOrder = (int)stepType,
                CreatedDate = DateTime.UtcNow
            });
        }

        dbContext.NodeExperienceBlueprints.Add(blueprintEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return blueprintEntity;
    }

    /// <summary>
    /// Deterministic safety net (code has final say, mirrors
    /// BlueprintValidationGuard's "LLM proposes, code decides" convention):
    /// an illustration may only be referenced by the step whose
    /// ExperienceStepType matches its IllustrationPurpose. Hypothesis can
    /// only ever show a Hypothesis-purpose (before-state, no-answer)
    /// illustration; Teaching can only ever show a Teaching-purpose (worked-
    /// example) illustration. Any other step never references illustrations
    /// today, so it's silently dropped rather than trusted from the LLM.
    /// </summary>
    private static bool MatchesStepPurpose(ExperienceStepType stepType, IllustrationPurpose purpose) => stepType switch
    {
        ExperienceStepType.Hypothesis => purpose == IllustrationPurpose.Hypothesis,
        ExperienceStepType.Teaching => purpose == IllustrationPurpose.Teaching,
        _ => false
    };
}
