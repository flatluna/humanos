using System.Net;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio — professor/author tool: inserts ONE new learning node
/// into an ALREADY-PERSISTED CapabilityGraph, from a free-text prompt plus an
/// explicit choice of which existing nodes it requires (prerequisites) and/or
/// which existing nodes should now require it (dependents).
///
/// "Nivel" in the Studio UI is never a stored column — it's the node's depth
/// in the Requires-edge DAG (see capabilitystudio/PreviewGraphPage.tsx's
/// groupIntoLevels). Connecting the new node to require the right
/// prerequisite nodes (and, optionally, being required by the right
/// dependent nodes) is what actually places it at the desired level.
/// </summary>
public sealed class AddCapabilityGraphNodeFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly GraphNodeAdditionAgent _agent;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly CapabilityGraphPersistenceService _persistenceService;
    private readonly ExperienceDesignerAgent _experienceDesignerAgent;
    private readonly ExampleCardIllustrationService _exampleCardIllustrationService;
    private readonly NodeExperienceBlueprintPersistenceService _blueprintPersistenceService;
    private readonly BlueprintValidatorAgent _blueprintValidatorAgent;
    private readonly BlueprintValidationPersistenceService _blueprintValidationPersistenceService;

    public AddCapabilityGraphNodeFunction(
        HumanOsDbContext dbContext,
        GraphNodeAdditionAgent agent,
        GraphIllustrationImageService imageService,
        IConfiguration configuration,
        CapabilityGraphPersistenceService persistenceService,
        ExperienceDesignerAgent experienceDesignerAgent,
        ExampleCardIllustrationService exampleCardIllustrationService,
        NodeExperienceBlueprintPersistenceService blueprintPersistenceService,
        BlueprintValidatorAgent blueprintValidatorAgent,
        BlueprintValidationPersistenceService blueprintValidationPersistenceService)
    {
        _dbContext = dbContext;
        _agent = agent;
        _imageService = imageService;
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
        _persistenceService = persistenceService;
        _experienceDesignerAgent = experienceDesignerAgent;
        _exampleCardIllustrationService = exampleCardIllustrationService;
        _blueprintPersistenceService = blueprintPersistenceService;
        _blueprintValidatorAgent = blueprintValidatorAgent;
        _blueprintValidationPersistenceService = blueprintValidationPersistenceService;
    }

    [Function("AddCapabilityGraphNode")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "capabilities/{capabilityId:guid}/graph/nodes")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotImplemented, "NotConfigured",
                "The Graph Node Addition agent is not configured on this environment.", cancellationToken);
        }

        var body = await request.ReadFromJsonAsync<AddCapabilityGraphNodeRequest>(cancellationToken)
            ?? new AddCapabilityGraphNodeRequest();

        if (string.IsNullOrWhiteSpace(body.Prompt))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Prompt is required.", cancellationToken);
        }

        var graph = await _dbContext.CapabilityGraphs
            .Include(g => g.Nodes).ThenInclude(n => n.Illustrations)
            .Include(g => g.Edges)
            .FirstOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "GraphNotFound",
                $"No CapabilityGraph found for capability {capabilityId}.", cancellationToken);
        }

        // Read the owning Capability's language + tone directives so a node
        // added here matches the rest of its capability's content — without
        // this, every "add node" call silently defaulted to Spanish/Standard
        // tone regardless of the capability's own ContentLanguageCode/
        // PedagogicalAudience (2026-07-26 fix, K-12 lesson course feature).
        var capabilityMeta = await _dbContext.Capabilities
            .Where(c => c.CapabilityId == capabilityId)
            .Select(c => new { c.ContentLanguageCode, c.PedagogicalAudience, c.IncludeExamplesStep })
            .FirstOrDefaultAsync(cancellationToken);
        var languageCode = capabilityMeta?.ContentLanguageCode ?? "es";
        var pedagogicalAudience = capabilityMeta?.PedagogicalAudience;
        var includeExamplesStep = capabilityMeta?.IncludeExamplesStep ?? true;

        var nodesById = graph.Nodes.ToDictionary(n => n.CapabilityGraphNodeId);

        var unknownIds = body.RequiresNodeIds.Concat(body.RequiredByNodeIds).Where(id => !nodesById.ContainsKey(id)).ToList();
        if (unknownIds.Count > 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "UnknownNodeIds",
                "One or more RequiresNodeIds/RequiredByNodeIds do not belong to this capability's graph.",
                cancellationToken);
        }

        // Deterministic DAG-safety check BEFORE calling the LLM: simulate the
        // new node with a placeholder id and verify the chosen wiring cannot
        // create a circular prerequisite chain.
        var placeholderNodeId = Guid.NewGuid();
        var proposedEdges = body.RequiresNodeIds.Select(id => (Source: placeholderNodeId, Target: id))
            .Concat(body.RequiredByNodeIds.Select(id => (Source: id, Target: placeholderNodeId)));
        var existingEdges = graph.Edges.Select(e => (Source: e.SourceNodeId, Target: e.TargetNodeId));

        if (GraphCycleValidator.WouldCreateCycle(existingEdges, proposedEdges))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "WouldCreateCycle",
                "Connecting the new node this way would create a circular prerequisite chain. " +
                "Revisa los nodos elegidos como 'requiere' / 'es requerido por'.", cancellationToken);
        }

        var requiresNodes = body.RequiresNodeIds
            .Select(id => (nodesById[id].Name, nodesById[id].Description)).ToList();
        var requiredByNodes = body.RequiredByNodeIds
            .Select(id => (nodesById[id].Name, nodesById[id].Description)).ToList();
        var existingNodeNames = graph.Nodes.Select(n => n.Name).ToList();

        GraphNodeAdditionAgent.NodeAdditionResult result;
        try
        {
            result = await _agent.GenerateNodeAsync(
                graph.Name, graph.Description, existingNodeNames, requiresNodes, requiredByNodes,
                body.Prompt, languageCode, pedagogicalAudience, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "GenerationFailed",
                $"Failed to generate the new node: {ex.Message}", cancellationToken);
        }

        var node = result.Node;

        if (existingNodeNames.Any(n => string.Equals(n, node.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "DuplicateNodeName",
                $"El nodo generado ('{node.Name}') ya existe en este grafo. Intenta con un prompt más específico.",
                cancellationToken);
        }

        // Best-effort illustrations (never block node creation on failure).
        // tenantId is parsed from an existing illustration's StoragePath —
        // same technique KnowledgeExpansionService uses (2026-07-20) — since
        // neither Capability nor CapabilityGraph stores a TenantId column.
        var illustrationRecords = new List<CapabilityGraphPersistenceService.NodeIllustrationRecord>();
        if (body.GenerateIllustrations && _imageService.IsConfigured && _illustrationStorage.IsConfigured
            && node.IllustrationPrompts.Count > 0)
        {
            var tenantId = TryParseTenantIdFromExistingIllustration(graph.Nodes);
            if (tenantId is not null)
            {
                foreach (var illustrationPrompt in node.IllustrationPrompts)
                {
                    try
                    {
                        var generated = await _imageService.GenerateAsync(illustrationPrompt.Prompt, cancellationToken);
                        using var stream = generated.ImageBytes.ToStream();
                        var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                            tenantId.Value, capabilityId, node.NodeId, illustrationRecords.Count + 1, stream,
                            cancellationToken: cancellationToken);

                        illustrationRecords.Add(new CapabilityGraphPersistenceService.NodeIllustrationRecord
                        {
                            NodeId = node.NodeId,
                            StoragePath = storagePath,
                            Prompt = illustrationPrompt.Prompt,
                            ImageModel = generated.ImageModel,
                            Width = generated.Width,
                            Height = generated.Height,
                            Purpose = illustrationPrompt.Purpose
                        });
                    }
                    catch
                    {
                        // Best-effort: a single failed illustration never blocks node creation.
                    }
                }
            }
        }

        var nextSortOrder = (graph.Nodes.Count > 0 ? graph.Nodes.Max(n => n.SortOrder) : 0) + 1;

        var savedNode = await _persistenceService.AddNodeAsync(
            _dbContext, graph.CapabilityGraphId, node, body.RequiresNodeIds, body.RequiredByNodeIds,
            nextSortOrder, illustrationRecords, cancellationToken);

        // A node with no NodeExperienceBlueprint cannot be previewed or
        // taught at all (GetNodeBlueprintFunction 404s) — so this is NOT
        // best-effort/optional the way illustrations are: it mirrors Paso 3
        // (ExperienceDesigner) + Paso 4 (BlueprintValidator) of the main
        // capability-creation pipeline for this one new node.
        bool blueprintGenerated = false;
        string? blueprintWarning = null;
        var exampleIllustrationsGenerated = 0;
        try
        {
            var savedIllustrations = await _dbContext.CapabilityGraphNodeIllustrations
                .Where(i => i.CapabilityGraphNodeId == savedNode.CapabilityGraphNodeId)
                .OrderBy(i => i.CreatedDate)
                .ToListAsync(cancellationToken);

            var availableIllustrations = savedIllustrations
                .Select((illustration, index) => new AvailableIllustrationDto
                {
                    Index = index + 1,
                    Prompt = illustration.Prompt,
                    Caption = illustration.Caption,
                    Purpose = illustration.Purpose
                })
                .ToList();

            var designResult = await _experienceDesignerAgent.DesignBlueprintAsync(
                savedNode, availableIllustrations, webFindings: null, languageCode: languageCode,
                pedagogicalAudience: pedagogicalAudience, includeExamplesStep: includeExamplesStep, cancellationToken: cancellationToken);

            // Real illustrations for the Examples step's cards (2026-07-29):
            // must happen AFTER the agent invents the cards' Title/Text, and
            // BEFORE persisting, so each card's IllustrationId is already
            // resolved in the JSON blob. Best-effort, same tenantId-lookup
            // technique as the node illustrations above.
            if (includeExamplesStep)
            {
                var examplesStepDto = designResult.Blueprint.Steps
                    .FirstOrDefault(s => s.StepType == ExperienceStepType.DeepDive);
                if (examplesStepDto?.ExampleCards is { Count: > 0 })
                {
                    var exampleTenantId = TryParseTenantIdFromExistingIllustration(graph.Nodes);
                    if (exampleTenantId is not null)
                    {
                        exampleIllustrationsGenerated = await _exampleCardIllustrationService.GenerateAsync(
                            _dbContext, exampleTenantId.Value, capabilityId, savedNode.CapabilityGraphNodeId,
                            savedIllustrations.Count, examplesStepDto.ExampleCards, cancellationToken);
                    }
                }
            }

            var blueprintEntity = await _blueprintPersistenceService.PersistAsync(
                _dbContext, savedNode.CapabilityGraphNodeId, designResult.Blueprint, savedIllustrations,
                includeExamplesStep: includeExamplesStep, cancellationToken: cancellationToken);

            blueprintGenerated = true;

            if (_blueprintValidatorAgent.IsConfigured)
            {
                try
                {
                    var validationResult = await _blueprintValidatorAgent.ValidateAsync(
                        savedNode, blueprintEntity, savedIllustrations.Count, cancellationToken);

                    await _blueprintValidationPersistenceService.PersistAsync(
                        _dbContext, blueprintEntity.NodeExperienceBlueprintId, validationResult.Validation,
                        validationResult.TokenUsage, cancellationToken);
                }
                catch
                {
                    // Validation is a quality-gate record, not a hard
                    // requirement for the node to be previewable/teachable —
                    // never fail node creation just because the validator run failed.
                }
            }
        }
        catch (Exception ex)
        {
            blueprintWarning = $"El nodo se creó pero no se pudo generar su contenido pedagógico (blueprint): {ex.Message}";
        }

        return await FunctionResponseFactory.CreatedResponseAsync(
            request,
            new AddCapabilityGraphNodeResponse
            {
                CapabilityGraphNodeId = savedNode.CapabilityGraphNodeId,
                Name = savedNode.Name,
                Description = savedNode.Description,
                SortOrder = savedNode.SortOrder,
                IllustrationsGenerated = illustrationRecords.Count + exampleIllustrationsGenerated,
                BlueprintGenerated = blueprintGenerated,
                BlueprintWarning = blueprintWarning
            },
            cancellationToken);
    }

    private static Guid? TryParseTenantIdFromExistingIllustration(IEnumerable<CapabilityGraphNode> nodes)
    {
        foreach (var node in nodes)
        {
            var illustration = node.Illustrations.FirstOrDefault();
            if (illustration is null) continue;

            var segments = illustration.StoragePath.Split('/');
            if (segments.Length > 0 && Guid.TryParse(segments[0], out var tenantId))
            {
                return tenantId;
            }
        }

        return null;
    }
}
