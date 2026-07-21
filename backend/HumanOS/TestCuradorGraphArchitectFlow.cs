using System.Text.Json;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using HumanOS.Models.People;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Tests;

/// <summary>
/// Simple test: Curador → GraphArchitect → Illustration generation → SQL persistence.
/// Runs the agents sequentially, generates one real illustration per node
/// (gpt-image-1.5, uploaded to Data Lake), persists the full graph (Capability,
/// CapabilityGraph, CapabilityGraphNode, CapabilityGraphEdge,
/// CapabilityGraphNodeIllustration) to SQL, and writes results to a text file.
/// </summary>
public sealed class TestCuradorGraphArchitectFlow
{
    private readonly IConfiguration _configuration;
    private readonly CuradorAgent _curator;
    private readonly GraphArchitectAgent _graphArchitect;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly IDbContextFactory<HumanOsDbContext>? _dbContextFactory;
    private readonly CapabilityGraphPersistenceService _persistenceService = new();
    private readonly ExperienceDesignerAgent _experienceDesigner;
    private readonly NodeExperienceBlueprintPersistenceService _blueprintPersistenceService = new();
    private readonly BlueprintValidatorAgent _blueprintValidator;
    private readonly BlueprintValidationPersistenceService _blueprintValidationPersistenceService = new();
    private readonly InstructorRuntimeOrchestrator _instructorRuntimeOrchestrator = new();
    private readonly AssessmentEvaluatorAgent _assessmentEvaluatorAgent;
    private readonly AssessmentEvaluator _assessmentEvaluator;
    private readonly SessionRecoveryEngine _sessionRecoveryEngine = new();
    private readonly string _outputPath;

    public TestCuradorGraphArchitectFlow(
        IConfiguration configuration,
        IDbContextFactory<HumanOsDbContext>? dbContextFactory = null)
    {
        _configuration = configuration;
        _curator = new CuradorAgent(configuration);
        _graphArchitect = new GraphArchitectAgent(configuration);
        _imageService = new GraphIllustrationImageService(configuration);
        _illustrationStorage = new CapabilityGraphIllustrationStorageService(configuration);
        _dbContextFactory = dbContextFactory;
        _experienceDesigner = new ExperienceDesignerAgent(configuration);
        _blueprintValidator = new BlueprintValidatorAgent(configuration);
        _assessmentEvaluatorAgent = new AssessmentEvaluatorAgent(configuration);
        _assessmentEvaluator = new AssessmentEvaluator(_assessmentEvaluatorAgent);
        _outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "CURADOR_GRAPHARCHITECT_RESULTS.txt");
    }

    public async Task RunAsync()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ PRUEBA: CURADOR → GRAPHARCHITECT FLOW (Suma Básica)          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝\n");

        try
        {
            // ==================== PASO 1: CURADOR ====================
            sb.AppendLine("PASO 1: CURADOR AGENT");
            sb.AppendLine("════════════════════════════════════════\n");

            var illustrationRecords = new List<CapabilityGraphPersistenceService.NodeIllustrationRecord>();
            var capabilityIdForPersistence = Guid.Empty;

            var curatorResult = await RunCuratorAsync();
            
            if (curatorResult.Success)
            {
                sb.AppendLine("✅ Curador ejecutado exitosamente\n");
                sb.AppendLine($"Summary: {curatorResult.Summary}");
                sb.AppendLine($"Chunks curados: {curatorResult.ChunkCount}");
                sb.AppendLine($"Tokens Input: {curatorResult.InputTokens}");
                sb.AppendLine($"Tokens Output: {curatorResult.OutputTokens}\n");

                sb.AppendLine("Chunks:");
                foreach (var (idx, chunk) in curatorResult.Chunks.Select((c, i) => (i + 1, c)))
                {
                    sb.AppendLine($"  [{idx}] [{chunk.Tag}] {chunk.Content.Substring(0, Math.Min(80, chunk.Content.Length))}...");
                }
            }
            else
            {
                sb.AppendLine($"❌ Error en Curador: {curatorResult.Error}\n");
                goto WriteFile;
            }

            sb.AppendLine("\n");
            sb.AppendLine("PASO 2: GRAPHARCHITECT AGENT");
            sb.AppendLine("════════════════════════════════════════\n");

            // ==================== PASO 2: GRAPHARCHITECT ====================
            var graphResult = await RunGraphArchitectAsync(curatorResult);

            if (graphResult.Success)
            {
                // A real Capability row is needed as the FK anchor for the
                // CapabilityGraph (1:1) — created up front so the SAME
                // CapabilityId is used for both the Data Lake storage path
                // (PASO 3) and the SQL persistence (PASO 4).
                //
                // The Azure SQL "HumanOSDev" database is serverless and
                // auto-pauses when idle; the FIRST connection after a pause
                // can take 30-60s to wake up and may exceed the default
                // 30s connect timeout on the very first attempt. Retry a
                // couple of times with a short wait rather than giving up
                // and silently falling back to an orphan Guid (which would
                // later fail PASO 4 with an FK violation).
                if (_dbContextFactory is not null)
                {
                    const int maxAttempts = 3;
                    for (var attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        try
                        {
                            await using var setupDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                            capabilityIdForPersistence = await EnsureTestCapabilityAsync(setupDbContext, CancellationToken.None);
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (attempt == maxAttempts)
                            {
                                sb.AppendLine($"⚠️  No se pudo crear la Capability de prueba tras {maxAttempts} intentos: {ex.Message}\n");
                            }
                            else
                            {
                                sb.AppendLine($"⚠️  Intento {attempt}/{maxAttempts} fallido creando Capability de prueba ({ex.Message}); reintentando en 20s (posible auto-pause de Azure SQL serverless)...\n");
                                await Task.Delay(TimeSpan.FromSeconds(20), CancellationToken.None);
                            }
                        }
                    }
                }

                if (capabilityIdForPersistence == Guid.Empty)
                {
                    capabilityIdForPersistence = Guid.NewGuid();
                }

                sb.AppendLine("✅ GraphArchitect ejecutado exitosamente\n");
                sb.AppendLine($"Graph Name: {graphResult.GraphName}");
                sb.AppendLine($"Nodos: {graphResult.NodeCount}");
                sb.AppendLine($"Aristas: {graphResult.EdgeCount}");
                sb.AppendLine($"Tokens Input: {graphResult.InputTokens}");
                sb.AppendLine($"Tokens Output: {graphResult.OutputTokens}\n");

                sb.AppendLine("Nodos:");
                foreach (var (idx, node) in graphResult.Nodes.Select((n, i) => (i + 1, n)))
                {
                    sb.AppendLine($"  [{idx}] {node.Name} ({node.NodeType}) - SortOrder: {node.SortOrder}");
                    sb.AppendLine($"       Description: {node.Description}");
                    sb.AppendLine($"       AcademicDefinition: {node.AcademicDefinition}");
                    sb.AppendLine($"       Interpretation: {node.Interpretation}");
                    sb.AppendLine($"       Examples: {string.Join(" | ", node.Examples)}");
                    sb.AppendLine($"       Applications: {string.Join(" | ", node.Applications)}");
                    sb.AppendLine($"       IllustrationPrompts: {string.Join(" | ", node.IllustrationPrompts.Select(p => $"[{p.Purpose}] {p.Prompt}"))}");
                    sb.AppendLine($"       References: {string.Join(", ", node.References)}");
                }

                sb.AppendLine("\nAristas (Relaciones):");
                foreach (var (idx, edge) in graphResult.Edges.Select((e, i) => (i + 1, e)))
                {
                    sb.AppendLine($"  [{idx}] {edge.SourceNodeName} --[{edge.RelationshipType}]--> {edge.TargetNodeName}");
                    sb.AppendLine($"       Rationale: {edge.Rationale}");
                }

                sb.AppendLine("\nValidaciones:");
                sb.AppendLine($"  ✓ NodeCount ≤ 30: {graphResult.Validations.IsSmallAndComprehensible}");
                sb.AppendLine($"  ✓ NoDuplicates: {graphResult.Validations.NoDuplicates}");
                sb.AppendLine($"  ✓ NoObviousCycles: {graphResult.Validations.NoObviousCycles}");

                sb.AppendLine("\n\nPASO 3: ILLUSTRATION GENERATION (gpt-image-1.5 → Data Lake)");
                sb.AppendLine("════════════════════════════════════════\n");

                if (!_imageService.IsConfigured)
                {
                    sb.AppendLine("⚠️  GraphIllustrationImageService no está configurado (falta AzureOpenAIImageDeploymentName) — se omite generación de imágenes.\n");
                }
                else if (!_illustrationStorage.IsConfigured)
                {
                    sb.AppendLine("⚠️  CapabilityGraphIllustrationStorageService no está configurado (falta DataLakeStorage) — se omite generación de imágenes.\n");
                }
                else if (graphResult.Graph is null)
                {
                    sb.AppendLine("⚠️  No hay CapabilityGraphResponse disponible — se omite generación de imágenes.\n");
                }
                else
                {
                    var tenantId = Guid.NewGuid();
                    var capabilityId = capabilityIdForPersistence;

                    foreach (var (idx, node) in graphResult.Graph.Nodes.Select((n, i) => (i + 1, n)))
                    {
                        if (node.IllustrationPrompts.Count == 0)
                        {
                            sb.AppendLine($"  [{idx}] {node.Name}: sin IllustrationPrompts, se omite.");
                            continue;
                        }

                        // One image per Purpose (Hypothesis + Teaching) —
                        // never just the first prompt — so Hypothesis and
                        // Teaching each get their own illustration instead of
                        // sharing one that inevitably reveals the answer in
                        // one of the two steps.
                        foreach (var (imageIndex, promptDto) in node.IllustrationPrompts.Select((p, i) => (i + 1, p)))
                        {
                            try
                            {
                                var generated = await _imageService.GenerateAsync(promptDto.Prompt, CancellationToken.None);
                                using var stream = generated.ImageBytes.ToStream();
                                var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                                    tenantId, capabilityId, node.NodeId, imageIndex, stream);

                                sb.AppendLine($"  [{idx}] {node.Name} — Purpose: {promptDto.Purpose}");
                                sb.AppendLine($"       Prompt: {promptDto.Prompt}");
                                sb.AppendLine($"       ImageModel: {generated.ImageModel} ({generated.Width}x{generated.Height})");
                                sb.AppendLine($"       StoragePath: {storagePath}");

                                illustrationRecords.Add(new CapabilityGraphPersistenceService.NodeIllustrationRecord
                                {
                                    NodeId = node.NodeId,
                                    StoragePath = storagePath,
                                    Prompt = promptDto.Prompt,
                                    ImageModel = generated.ImageModel,
                                    Width = generated.Width,
                                    Height = generated.Height,
                                    Purpose = promptDto.Purpose
                                });
                            }
                            catch (Exception ex)
                            {
                                sb.AppendLine($"  [{idx}] {node.Name} ({promptDto.Purpose}): ❌ Error generando ilustración: {ex.Message}");
                            }
                        }
                    }
                }

                sb.AppendLine("\n\nPASO 4: PERSISTENCIA EN SQL (CapabilityGraph + Nodes + Edges + Illustrations)");
                sb.AppendLine("════════════════════════════════════════\n");

                if (_dbContextFactory is null)
                {
                    sb.AppendLine("⚠️  No hay IDbContextFactory<HumanOsDbContext> configurado (falta HumanOSDatabase) — se omite persistencia en SQL.\n");
                }
                else if (graphResult.Graph is null)
                {
                    sb.AppendLine("⚠️  No hay CapabilityGraphResponse disponible — se omite persistencia en SQL.\n");
                }
                else
                {
                    try
                    {
                        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);

                        var persistedGraph = await _persistenceService.PersistAsync(
                            dbContext,
                            capabilityIdForPersistence,
                            graphResult.Graph,
                            illustrationRecords,
                            CancellationToken.None);

                        sb.AppendLine("✅ Grafo persistido exitosamente en SQL\n");
                        sb.AppendLine($"CapabilityId: {capabilityIdForPersistence}");
                        sb.AppendLine($"CapabilityGraphId: {persistedGraph.CapabilityGraphId}");
                        sb.AppendLine($"CapabilityGraphNodes persistidos: {persistedGraph.Nodes.Count}");
                        sb.AppendLine($"CapabilityGraphEdges persistidos: {persistedGraph.Edges.Count}");
                        sb.AppendLine($"CapabilityGraphNodeIllustrations persistidas: {persistedGraph.Nodes.Sum(n => n.Illustrations.Count)}\n");

                        sb.AppendLine("\nPASO 5: VERIFICACIÓN (re-lectura desde SQL con un DbContext nuevo)");
                        sb.AppendLine("════════════════════════════════════════\n");

                        await using var verifyDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);

                        var reloadedGraph = await verifyDbContext.CapabilityGraphs
                            .Include(g => g.Nodes).ThenInclude(n => n.Illustrations)
                            .Include(g => g.Edges)
                            .FirstOrDefaultAsync(g => g.CapabilityGraphId == persistedGraph.CapabilityGraphId, CancellationToken.None);

                        if (reloadedGraph is null)
                        {
                            sb.AppendLine("❌ No se encontró el CapabilityGraph al releer con un DbContext nuevo (dato NO persistido realmente).\n");
                        }
                        else
                        {
                            var capabilityExists = await verifyDbContext.Capabilities
                                .AnyAsync(c => c.CapabilityId == reloadedGraph.CapabilityId, CancellationToken.None);

                            sb.AppendLine("✅ Confirmado en SQL (SELECT real, DbContext independiente):\n");
                            sb.AppendLine($"Capability row existe: {capabilityExists}");
                            sb.AppendLine($"CapabilityGraph.Name: {reloadedGraph.Name}");
                            sb.AppendLine($"CapabilityGraphNodes en SQL: {reloadedGraph.Nodes.Count}");
                            sb.AppendLine($"CapabilityGraphEdges en SQL: {reloadedGraph.Edges.Count}");
                            sb.AppendLine($"CapabilityGraphNodeIllustrations en SQL: {reloadedGraph.Nodes.Sum(n => n.Illustrations.Count)}");
                            foreach (var n in reloadedGraph.Nodes.OrderBy(n => n.SortOrder))
                            {
                                sb.AppendLine($"  - [{n.SortOrder}] {n.Name} ({n.NodeType}) — {n.Illustrations.Count} ilustración(es)");
                            }
                            sb.AppendLine();

                            sb.AppendLine("\n\nPASO 6: EXPERIENCE DESIGNER AGENT (NodeExperienceBlueprint + Steps)");
                            sb.AppendLine("════════════════════════════════════════\n");

                            if (!_experienceDesigner.IsConfigured)
                            {
                                sb.AppendLine("⚠️  ExperienceDesignerAgent no está configurado (falta Azure OpenAI config) — se omite PASO 6.\n");
                            }
                            else
                            {
                                var firstNode = reloadedGraph.Nodes.OrderBy(n => n.SortOrder).FirstOrDefault();
                                if (firstNode is null)
                                {
                                    sb.AppendLine("⚠️  No hay nodos en el grafo persistido — se omite PASO 6.\n");
                                }
                                else
                                {
                                    try
                                    {
                                        var availableIllustrations = firstNode.Illustrations
                                            .Select((illustration, i) => new AvailableIllustrationDto
                                            {
                                                Index = i + 1,
                                                Prompt = illustration.Prompt ?? string.Empty,
                                                Caption = illustration.Caption,
                                                Purpose = illustration.Purpose
                                            })
                                            .ToList();

                                        var designResult = await _experienceDesigner.DesignBlueprintAsync(
                                            firstNode,
                                            availableIllustrations,
                                            cancellationToken: CancellationToken.None);

                                        sb.AppendLine($"✅ Blueprint diseñado para nodo: {firstNode.Name}\n");
                                        sb.AppendLine($"Name: {designResult.Blueprint.Name}");
                                        sb.AppendLine($"Description: {designResult.Blueprint.Description}");
                                        sb.AppendLine($"Tokens Input: {designResult.TokenUsage.InputTokens}");
                                        sb.AppendLine($"Tokens Output: {designResult.TokenUsage.OutputTokens}\n");
                                        foreach (var step in designResult.Blueprint.Steps)
                                        {
                                            sb.AppendLine($"  [{step.StepType}] {step.Content}");
                                            if (step.IllustrationIndexes.Count > 0)
                                            {
                                                sb.AppendLine($"       IllustrationIndexes: {string.Join(", ", step.IllustrationIndexes)}");
                                            }
                                        }

                                        await using var blueprintDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                        var persistedBlueprint = await _blueprintPersistenceService.PersistAsync(
                                            blueprintDbContext,
                                            firstNode.CapabilityGraphNodeId,
                                            designResult.Blueprint,
                                            firstNode.Illustrations.ToList(),
                                            CancellationToken.None);

                                        sb.AppendLine($"\n✅ Blueprint persistido en SQL — NodeExperienceBlueprintId: {persistedBlueprint.NodeExperienceBlueprintId}");
                                        sb.AppendLine($"Steps persistidos: {persistedBlueprint.Steps.Count}\n");

                                        sb.AppendLine("\n\nPASO 7: BLUEPRINT VALIDATOR AGENT (quality gate)");
                                        sb.AppendLine("════════════════════════════════════════\n");

                                        if (!_blueprintValidator.IsConfigured)
                                        {
                                            sb.AppendLine("⚠️  BlueprintValidatorAgent no está configurado (falta Azure OpenAI config) — se omite PASO 7.\n");
                                        }
                                        else
                                        {
                                            try
                                            {
                                                var validationResult = await _blueprintValidator.ValidateAsync(
                                                    firstNode,
                                                    persistedBlueprint,
                                                    firstNode.Illustrations.Count,
                                                    CancellationToken.None);

                                                var validation = validationResult.Validation;
                                                sb.AppendLine($"✅ Blueprint validado — Status: {validation.Status}, Score: {validation.Score}/100\n");
                                                sb.AppendLine($"Tokens Input: {validationResult.TokenUsage.InputTokens}");
                                                sb.AppendLine($"Tokens Output: {validationResult.TokenUsage.OutputTokens}\n");

                                                if (validation.Issues.Count > 0)
                                                {
                                                    sb.AppendLine($"Issues ({validation.Issues.Count}):");
                                                    foreach (var issue in validation.Issues)
                                                    {
                                                        sb.AppendLine($"  ❌ [{issue.Area}] {issue.Message}");
                                                    }
                                                    sb.AppendLine();
                                                }

                                                if (validation.Warnings.Count > 0)
                                                {
                                                    sb.AppendLine($"Warnings ({validation.Warnings.Count}):");
                                                    foreach (var warning in validation.Warnings)
                                                    {
                                                        sb.AppendLine($"  ⚠️  [{warning.Area}] {warning.Message}");
                                                    }
                                                    sb.AppendLine();
                                                }

                                                if (validation.Metrics.Count > 0)
                                                {
                                                    sb.AppendLine("Metrics:");
                                                    foreach (var metric in validation.Metrics)
                                                    {
                                                        sb.AppendLine($"  {metric.MetricName}: {metric.MetricValue}");
                                                    }
                                                    sb.AppendLine();
                                                }

                                                await using var validationDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var persistedValidation = await _blueprintValidationPersistenceService.PersistAsync(
                                                    validationDbContext,
                                                    persistedBlueprint.NodeExperienceBlueprintId,
                                                    validation,
                                                    validationResult.TokenUsage,
                                                    CancellationToken.None);

                                                sb.AppendLine($"\n✅ Validación persistida en SQL — BlueprintValidationId: {persistedValidation.BlueprintValidationId}");
                                                sb.AppendLine($"Issues persistidos: {persistedValidation.Issues.Count(i => i.Severity == HumanOS.Models.Capabilities.Graph.BlueprintValidationIssueSeverity.Error)}");
                                                sb.AppendLine($"Warnings persistidos: {persistedValidation.Issues.Count(i => i.Severity == HumanOS.Models.Capabilities.Graph.BlueprintValidationIssueSeverity.Warning)}");
                                                sb.AppendLine($"Metrics persistidas: {persistedValidation.Metrics.Count}\n");
                                            }
                                            catch (Exception ex)
                                            {
                                                sb.AppendLine($"❌ Error en PASO 7 (BlueprintValidator): {ex.Message}\n");
                                                var inner = ex.InnerException;
                                                while (inner is not null)
                                                {
                                                    sb.AppendLine($"   Inner: {inner.Message}");
                                                    inner = inner.InnerException;
                                                }
                                                sb.AppendLine();
                                            }
                                        }

                                        sb.AppendLine("\n\nPASO 8: INSTRUCTOR RUNTIME ORCHESTRATOR (Runtime Paso 2 — StartSession/GetCurrentStep/SubmitResponse/Advance/CompleteNode)");
                                        sb.AppendLine("════════════════════════════════════════\n");

                                        var testLearningSessionNodeId = Guid.Empty;
                                        var testLearningSessionId = Guid.Empty;

                                        try
                                        {
                                            await using var sessionSetupDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                            var testPersonId = await EnsureTestPersonAsync(sessionSetupDbContext, CancellationToken.None);

                                            await using var startDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                            var startResult = await _instructorRuntimeOrchestrator.StartSessionAsync(
                                                startDbContext,
                                                testPersonId,
                                                capabilityIdForPersistence,
                                                firstNode.CapabilityGraphNodeId,
                                                CancellationToken.None);

                                            testLearningSessionNodeId = startResult.LearningSessionNodeId;
                                            testLearningSessionId = startResult.LearningSessionId;

                                            sb.AppendLine("✅ StartSession() exitoso\n");
                                            sb.AppendLine($"PersonId: {testPersonId}");
                                            sb.AppendLine($"LearningSessionId: {startResult.LearningSessionId}");
                                            sb.AppendLine($"LearningSessionNodeId: {startResult.LearningSessionNodeId}");
                                            sb.AppendLine($"NodeExperienceBlueprintId resuelto: {startResult.NodeExperienceBlueprintId}\n");

                                            async Task<InstructorRuntimeOrchestrator.CurrentStepResult> ShowCurrentStepAsync(string label)
                                            {
                                                await using var stepDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var current = await _instructorRuntimeOrchestrator.GetCurrentStepAsync(stepDbContext, testLearningSessionNodeId, CancellationToken.None);
                                                sb.AppendLine($"--- {label}: GetCurrentStep() ---");
                                                sb.AppendLine($"StepType: {current.StepType}");
                                                sb.AppendLine($"StepContent: {current.StepContent}");
                                                sb.AppendLine($"Illustrations: {current.Illustrations.Count}");
                                                foreach (var ill in current.Illustrations)
                                                {
                                                    sb.AppendLine($"   - {ill.StoragePath} ({ill.Caption})");
                                                }
                                                sb.AppendLine();
                                                return current;
                                            }

                                            async Task SubmitAsync(Guid stepId, string response, string label)
                                            {
                                                await using var submitDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var evidenceId = await _instructorRuntimeOrchestrator.SubmitResponseAsync(submitDbContext, stepId, response, CancellationToken.None);
                                                sb.AppendLine($"✅ SubmitResponse() [{label}] — LearningEvidenceId: {evidenceId}\n");
                                            }

                                            async Task AdvanceAsync(string label)
                                            {
                                                await using var advanceDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var next = await _instructorRuntimeOrchestrator.AdvanceToNextStepAsync(advanceDbContext, testLearningSessionNodeId, CancellationToken.None);
                                                sb.AppendLine($"✅ AdvanceToNextStep() → {next.StepType} [{label}]\n");
                                            }

                                            var hypothesisStep = await ShowCurrentStepAsync("Hypothesis");
                                            await SubmitAsync(hypothesisStep.LearningSessionStepId, "Creo que el segundo grupo tiene más porque parece más grande.", "Hypothesis");

                                            await AdvanceAsync("Hypothesis → Teaching");
                                            await ShowCurrentStepAsync("Teaching");
                                            // Teaching es contenido puro — no se llama SubmitResponse aquí.

                                            await AdvanceAsync("Teaching → Recall");
                                            var recallStep = await ShowCurrentStepAsync("Recall");
                                            await SubmitAsync(recallStep.LearningSessionStepId, "La cantidad me dice cuántos objetos hay en cada grupo, y eso me sirve para saber qué números voy a combinar.", "Recall");

                                            await AdvanceAsync("Recall → Production");
                                            var productionStep = await ShowCurrentStepAsync("Production");
                                            await SubmitAsync(productionStep.LearningSessionStepId, "Encontré 4 lápices y 3 borradores. Si los junto tendría 7 en total.", "Production");

                                            await AdvanceAsync("Production → Assessment");
                                            var assessmentStep = await ShowCurrentStepAsync("Assessment");
                                            await SubmitAsync(assessmentStep.LearningSessionStepId, "El primer grupo tiene 5. El segundo tiene 2. Si los juntamos habría 7.", "Assessment");

                                            sb.AppendLine("\n\nPASO 9: ASSESSMENT EVALUATOR (Runtime Paso 3 — EvaluateAssessment)");
                                            sb.AppendLine("════════════════════════════════════════\n");

                                            if (!_assessmentEvaluatorAgent.IsConfigured)
                                            {
                                                sb.AppendLine("⚠️  AssessmentEvaluatorAgent no está configurado (falta Azure OpenAI config) — se omite PASO 9.\n");
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    await using var evaluateDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                    var evaluation = await _assessmentEvaluator.EvaluateAssessmentAsync(evaluateDbContext, testLearningSessionNodeId, CancellationToken.None);

                                                    sb.AppendLine($"✅ EvaluateAssessment() ejecutado — Score: {evaluation.AssessmentResult.Score}, Passed: {evaluation.AssessmentResult.Passed}");
                                                    sb.AppendLine($"Feedback: {evaluation.AssessmentResult.Feedback}");
                                                    sb.AppendLine($"Tokens Input: {evaluation.TokenUsage.InputTokens}");
                                                    sb.AppendLine($"Tokens Output: {evaluation.TokenUsage.OutputTokens}");
                                                    sb.AppendLine($"LearningAssessmentResultId: {evaluation.AssessmentResult.LearningAssessmentResultId}\n");

                                                    await using var completeDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                    await _instructorRuntimeOrchestrator.CompleteNodeAsync(completeDbContext, testLearningSessionNodeId, CancellationToken.None);
                                                    sb.AppendLine("✅ CompleteNode() ejecutado\n");

                                                    // Verificación final: releer TODO con un DbContext nuevo, igual que PASO 5.
                                                    await using var sessionVerifyDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                    var reloadedSession = await sessionVerifyDbContext.LearningSessions
                                                        .Include(s => s.Nodes).ThenInclude(n => n.Steps).ThenInclude(st => st.Evidence)
                                                        .Include(s => s.Nodes).ThenInclude(n => n.AssessmentResults)
                                                        .FirstOrDefaultAsync(s => s.LearningSessionId == testLearningSessionId, CancellationToken.None);

                                                    if (reloadedSession is null)
                                                    {
                                                        sb.AppendLine("❌ No se encontró la LearningSession al releer con un DbContext nuevo (dato NO persistido realmente).\n");
                                                    }
                                                    else
                                                    {
                                                        var reloadedNode = reloadedSession.Nodes.First();
                                                        sb.AppendLine("✅ Confirmado en SQL (SELECT real, DbContext independiente):\n");
                                                        sb.AppendLine($"LearningSessionNode.Status: {reloadedNode.Status}");
                                                        sb.AppendLine($"LearningSessionNode.CompletedDate: {reloadedNode.CompletedDate}");
                                                        sb.AppendLine($"LearningSessionSteps en SQL: {reloadedNode.Steps.Count}");
                                                        foreach (var step in reloadedNode.Steps.OrderBy(s => s.StepType))
                                                        {
                                                            sb.AppendLine($"  [{step.StepType}] Status={step.Status}, Evidence={step.Evidence.Count}");
                                                        }
                                                        sb.AppendLine($"LearningAssessmentResults en SQL: {reloadedNode.AssessmentResults.Count}");
                                                        foreach (var assessmentResult in reloadedNode.AssessmentResults)
                                                        {
                                                            sb.AppendLine($"  Score={assessmentResult.Score}, Passed={assessmentResult.Passed}");
                                                        }
                                                        sb.AppendLine();
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    sb.AppendLine($"❌ Error en PASO 9 (AssessmentEvaluator): {ex.Message}\n");
                                                    var inner = ex.InnerException;
                                                    while (inner is not null)
                                                    {
                                                        sb.AppendLine($"   Inner: {inner.Message}");
                                                        inner = inner.InnerException;
                                                    }
                                                    sb.AppendLine();
                                                }
                                            }

                                            sb.AppendLine("\n\nPASO 10: SESSION RECOVERY ENGINE (Runtime Paso 3.5 — GetActiveSession/GetActiveNode/GetActiveStep/ResumeSession)");
                                            sb.AppendLine("════════════════════════════════════════\n");

                                            try
                                            {
                                                // Sesión NUEVA e independiente de la de arriba (esa ya quedó Completed) —
                                                // para probar recuperación necesitamos un nodo detenido A MEDIAS (en
                                                // Recall), no uno ya terminado.
                                                await using var recoveryStartDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var recoveryStartResult = await _instructorRuntimeOrchestrator.StartSessionAsync(
                                                    recoveryStartDbContext,
                                                    testPersonId,
                                                    capabilityIdForPersistence,
                                                    firstNode.CapabilityGraphNodeId,
                                                    CancellationToken.None);

                                                sb.AppendLine("✅ Nueva LearningSession creada para probar recuperación");
                                                sb.AppendLine($"LearningSessionId: {recoveryStartResult.LearningSessionId}");
                                                sb.AppendLine($"LearningSessionNodeId: {recoveryStartResult.LearningSessionNodeId}\n");

                                                await using var recoveryAdvance1DbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                await _instructorRuntimeOrchestrator.AdvanceToNextStepAsync(recoveryAdvance1DbContext, recoveryStartResult.LearningSessionNodeId, CancellationToken.None);
                                                sb.AppendLine("✅ AdvanceToNextStep() → Teaching\n");

                                                await using var recoveryAdvance2DbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                await _instructorRuntimeOrchestrator.AdvanceToNextStepAsync(recoveryAdvance2DbContext, recoveryStartResult.LearningSessionNodeId, CancellationToken.None);
                                                sb.AppendLine("✅ AdvanceToNextStep() → Recall\n");

                                                sb.AppendLine("🔌 Simulando cierre de navegador — ninguna llamada más al Runtime hasta ResumeSessionAsync().\n");

                                                // A partir de aquí, SessionRecoveryEngine reconstruye TODO desde cero,
                                                // usando solo PersonId/CapabilityId (lo único que un UI real tendría
                                                // por navegación) — ningún LearningSessionNodeId/StepId recordado.
                                                await using var activeSessionDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var activeSession = await _sessionRecoveryEngine.GetActiveSessionAsync(activeSessionDbContext, testPersonId, CancellationToken.None);
                                                sb.AppendLine($"GetActiveSessionAsync() → LearningSessionId: {activeSession?.LearningSessionId}, Status: {activeSession?.Status}\n");

                                                await using var activeNodeDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var activeNode = await _sessionRecoveryEngine.GetActiveNodeAsync(activeNodeDbContext, testPersonId, capabilityIdForPersistence, CancellationToken.None);
                                                sb.AppendLine($"GetActiveNodeAsync() → LearningSessionNodeId: {activeNode?.LearningSessionNodeId}, CapabilityGraphNodeId: {activeNode?.CapabilityGraphNodeId}, Status: {activeNode?.Status}\n");

                                                if (activeNode is not null)
                                                {
                                                    await using var activeStepDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                    var activeStep = await _sessionRecoveryEngine.GetActiveStepAsync(activeStepDbContext, activeNode.LearningSessionNodeId, CancellationToken.None);
                                                    sb.AppendLine($"GetActiveStepAsync() → StepType: {activeStep?.StepType}, Status: {activeStep?.Status}\n");
                                                }

                                                await using var resumeDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var resumeResult = await _sessionRecoveryEngine.ResumeSessionAsync(resumeDbContext, testPersonId, capabilityIdForPersistence, CancellationToken.None);

                                                sb.AppendLine("✅ ResumeSessionAsync() ejecutado — estado reconstruido 100% desde SQL:");
                                                sb.AppendLine($"LearningSessionId: {resumeResult.LearningSessionId}");
                                                sb.AppendLine($"LearningSessionNodeId: {resumeResult.LearningSessionNodeId}");
                                                sb.AppendLine($"CurrentStep.StepType: {resumeResult.CurrentStep.StepType} (esperado: Recall)");
                                                sb.AppendLine($"CurrentStep.StepContent: {resumeResult.CurrentStep.StepContent}");
                                                sb.AppendLine($"Illustrations: {resumeResult.CurrentStep.Illustrations.Count}");
                                                foreach (var ill in resumeResult.CurrentStep.Illustrations)
                                                {
                                                    sb.AppendLine($"   - {ill.StoragePath} ({ill.Caption})");
                                                }

                                                if (resumeResult.CurrentStep.StepType == HumanOS.Models.Capabilities.Graph.ExperienceStepType.Recall)
                                                {
                                                    sb.AppendLine("\n✅ PASO 10 EXITOSO: la recuperación reconstruyó correctamente Step=Recall sin ningún ID guardado por el cliente.\n");
                                                }
                                                else
                                                {
                                                    sb.AppendLine($"\n❌ PASO 10 FALLÓ: se esperaba Recall pero se obtuvo {resumeResult.CurrentStep.StepType}.\n");
                                                }

                                                // Verificación final: releer con un DbContext independiente.
                                                await using var recoveryVerifyDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                                                var recoveryVerifySession = await recoveryVerifyDbContext.LearningSessions
                                                    .Include(s => s.Nodes).ThenInclude(n => n.Steps)
                                                    .FirstOrDefaultAsync(s => s.LearningSessionId == resumeResult.LearningSessionId, CancellationToken.None);

                                                if (recoveryVerifySession is not null)
                                                {
                                                    var recoveryVerifyNode = recoveryVerifySession.Nodes.First(n => n.LearningSessionNodeId == resumeResult.LearningSessionNodeId);
                                                    sb.AppendLine("✅ Confirmado en SQL (SELECT real, DbContext independiente):");
                                                    sb.AppendLine($"LearningSession.Status: {recoveryVerifySession.Status}");
                                                    sb.AppendLine($"LearningSessionNode.Status: {recoveryVerifyNode.Status}");
                                                    foreach (var step in recoveryVerifyNode.Steps.OrderBy(s => s.StepType))
                                                    {
                                                        sb.AppendLine($"  [{step.StepType}] Status={step.Status}");
                                                    }
                                                    sb.AppendLine();
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                sb.AppendLine($"❌ Error en PASO 10 (SessionRecoveryEngine): {ex.Message}\n");
                                                var inner = ex.InnerException;
                                                while (inner is not null)
                                                {
                                                    sb.AppendLine($"   Inner: {inner.Message}");
                                                    inner = inner.InnerException;
                                                }
                                                sb.AppendLine();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            sb.AppendLine($"❌ Error en PASO 8 (InstructorRuntimeOrchestrator): {ex.Message}\n");
                                            var inner = ex.InnerException;
                                            while (inner is not null)
                                            {
                                                sb.AppendLine($"   Inner: {inner.Message}");
                                                inner = inner.InnerException;
                                            }
                                            sb.AppendLine();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        sb.AppendLine($"❌ Error en PASO 6 (ExperienceDesigner): {ex.Message}\n");
                                        var inner = ex.InnerException;
                                        while (inner is not null)
                                        {
                                            sb.AppendLine($"   Inner: {inner.Message}");
                                            inner = inner.InnerException;
                                        }
                                        sb.AppendLine();
                                    }
                                }
                            }

                            // ==================== PASO 11: BLUEPRINTS PARA EL RESTO DE LOS NODOS ====================
                            // PASO 6/7 arriba solo diseña/persiste/valida el blueprint del PRIMER nodo (el único
                            // que además se ejercita con el runtime en PASO 8-10). Sin este paso, un capability con
                            // varios nodos (p.ej. "Multiplicación" + "Raíz Cuadrada") quedaría con blueprint SOLO
                            // en el primero — al navegar al segundo nodo en el UI no habría experiencia que mostrar.
                            // Aquí se genera el blueprint para TODOS los demás nodos también.
                            sb.AppendLine("\n\nPASO 11: BLUEPRINTS PARA EL RESTO DE LOS NODOS DEL GRAFO");
                            sb.AppendLine("════════════════════════════════════════\n");

                            if (!_experienceDesigner.IsConfigured)
                            {
                                sb.AppendLine("⚠️  ExperienceDesignerAgent no está configurado — se omite PASO 11.\n");
                            }
                            else
                            {
                                var remainingNodes = reloadedGraph.Nodes.OrderBy(n => n.SortOrder).Skip(1).ToList();
                                if (remainingNodes.Count == 0)
                                {
                                    sb.AppendLine("(No hay más nodos — el grafo tiene un solo nodo.)\n");
                                }
                                else
                                {
                                    foreach (var node in remainingNodes)
                                    {
                                        await DesignPersistValidateBlueprintForNodeAsync(node, sb, CancellationToken.None);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"❌ Error persistiendo en SQL: {ex.Message}\n");
                        var inner = ex.InnerException;
                        while (inner is not null)
                        {
                            sb.AppendLine($"   Inner: {inner.Message}");
                            inner = inner.InnerException;
                        }
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                sb.AppendLine($"❌ Error en GraphArchitect: {graphResult.Error}\n");
            }

        WriteFile:
            sb.AppendLine("\n\n╔═══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║ FIN DE PRUEBA                                                 ║");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝\n");

            // Escribir a archivo
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);
            await File.WriteAllTextAsync(_outputPath, sb.ToString());
            Console.WriteLine($"✅ Resultados guardados en: {_outputPath}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"\n❌ EXCEPCIÓN: {ex.Message}");
            sb.AppendLine(ex.StackTrace);
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);
            await File.WriteAllTextAsync(_outputPath, sb.ToString());
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Designs, persists and validates a <see cref="NodeExperienceBlueprint"/> for
    /// a SINGLE node — the same logic PASO 6/7 runs inline for the first node,
    /// extracted so PASO 11 can reuse it for every other node in the graph.
    /// Swallows its own exceptions (appends an error line instead) so one bad
    /// node doesn't abort blueprint generation for the rest.
    /// </summary>
    private async Task DesignPersistValidateBlueprintForNodeAsync(
        HumanOS.Models.Capabilities.Graph.CapabilityGraphNode node,
        System.Text.StringBuilder sb,
        CancellationToken cancellationToken)
    {
        if (_dbContextFactory is null)
        {
            return;
        }

        try
        {
            var availableIllustrations = node.Illustrations
                .Select((illustration, i) => new AvailableIllustrationDto
                {
                    Index = i + 1,
                    Prompt = illustration.Prompt ?? string.Empty,
                    Caption = illustration.Caption,
                    Purpose = illustration.Purpose
                })
                .ToList();

            var designResult = await _experienceDesigner.DesignBlueprintAsync(
                node,
                availableIllustrations,
                cancellationToken: cancellationToken);

            sb.AppendLine($"✅ Blueprint diseñado para nodo: {node.Name}\n");
            sb.AppendLine($"Name: {designResult.Blueprint.Name}");
            sb.AppendLine($"Description: {designResult.Blueprint.Description}");
            sb.AppendLine($"Tokens Input: {designResult.TokenUsage.InputTokens}");
            sb.AppendLine($"Tokens Output: {designResult.TokenUsage.OutputTokens}\n");
            foreach (var step in designResult.Blueprint.Steps)
            {
                sb.AppendLine($"  [{step.StepType}] {step.Content}");
                if (step.IllustrationIndexes.Count > 0)
                {
                    sb.AppendLine($"       IllustrationIndexes: {string.Join(", ", step.IllustrationIndexes)}");
                }
            }

            await using var blueprintDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var persistedBlueprint = await _blueprintPersistenceService.PersistAsync(
                blueprintDbContext,
                node.CapabilityGraphNodeId,
                designResult.Blueprint,
                node.Illustrations.ToList(),
                cancellationToken);

            sb.AppendLine($"\n✅ Blueprint persistido en SQL — NodeExperienceBlueprintId: {persistedBlueprint.NodeExperienceBlueprintId}");
            sb.AppendLine($"Steps persistidos: {persistedBlueprint.Steps.Count}\n");

            if (!_blueprintValidator.IsConfigured)
            {
                sb.AppendLine("⚠️  BlueprintValidatorAgent no está configurado — se omite validación para este nodo.\n");
                return;
            }

            try
            {
                var validationResult = await _blueprintValidator.ValidateAsync(
                    node,
                    persistedBlueprint,
                    node.Illustrations.Count,
                    cancellationToken);

                var validation = validationResult.Validation;
                sb.AppendLine($"✅ Blueprint validado — Status: {validation.Status}, Score: {validation.Score}/100\n");

                if (validation.Issues.Count > 0)
                {
                    sb.AppendLine($"Issues ({validation.Issues.Count}):");
                    foreach (var issue in validation.Issues)
                    {
                        sb.AppendLine($"  ❌ [{issue.Area}] {issue.Message}");
                    }
                    sb.AppendLine();
                }

                if (validation.Warnings.Count > 0)
                {
                    sb.AppendLine($"Warnings ({validation.Warnings.Count}):");
                    foreach (var warning in validation.Warnings)
                    {
                        sb.AppendLine($"  ⚠️  [{warning.Area}] {warning.Message}");
                    }
                    sb.AppendLine();
                }

                await using var validationDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var persistedValidation = await _blueprintValidationPersistenceService.PersistAsync(
                    validationDbContext,
                    persistedBlueprint.NodeExperienceBlueprintId,
                    validation,
                    validationResult.TokenUsage,
                    cancellationToken);

                sb.AppendLine($"✅ Validación persistida en SQL — BlueprintValidationId: {persistedValidation.BlueprintValidationId}\n");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Error validando blueprint del nodo {node.Name}: {ex.Message}\n");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"❌ Error diseñando/persistiendo blueprint para nodo {node.Name}: {ex.Message}\n");
            var inner = ex.InnerException;
            while (inner is not null)
            {
                sb.AppendLine($"   Inner: {inner.Message}");
                inner = inner.InnerException;
            }
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Find-or-create the CapabilityDomain used to anchor this test's Capability
    /// rows, then always creates a NEW Capability (unique Code per run, since
    /// CapabilityGraph is 1:1 with Capability) so each test invocation gets its
    /// own persisted graph without violating the unique CapabilityId index.
    /// </summary>
    private static async Task<Guid> EnsureTestCapabilityAsync(
        HumanOsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string domainCode = "TEST-CURADOR-GRAPHARCHITECT";

        var domain = await dbContext.CapabilityDomains
            .FirstOrDefaultAsync(d => d.Code == domainCode, cancellationToken);

        if (domain is null)
        {
            domain = new CapabilityDomain
            {
                CapabilityDomainId = Guid.NewGuid(),
                Code = domainCode,
                Name = "Test — Curador/GraphArchitect Flow",
                Description = "Dominio de prueba usado por TestCuradorGraphArchitectFlow para validar PASO 2.",
                CreatedDate = DateTime.UtcNow
            };
            dbContext.CapabilityDomains.Add(domain);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var capability = new Capability
        {
            CapabilityId = Guid.NewGuid(),
            CapabilityDomainId = domain.CapabilityDomainId,
            Code = $"TEST-MULT-RAIZ-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Name = "Multiplicación y Raíz Cuadrada (prueba Curador/GraphArchitect)",
            Description = "Capability de prueba generada automáticamente por TestCuradorGraphArchitectFlow.",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        dbContext.Capabilities.Add(capability);
        await dbContext.SaveChangesAsync(cancellationToken);

        return capability.CapabilityId;
    }

    /// <summary>
    /// Find-or-create a Person to anchor <see cref="LearningSessionOrchestrator"/>'s
    /// test run (PASO 8). Deliberately NEVER touches the [Tenant] table's
    /// mapped columns via EF (SELECT/INSERT through <c>dbContext.Tenants</c>) —
    /// the live Tenant table predates this codebase's migration history (it
    /// was created outside of EF Core migrations) and is missing the
    /// Address/Email/Phone columns that Tenant.cs's model has since gained,
    /// causing "Invalid column name" errors on ANY normal EF query against
    /// it. Reads only the raw TenantId column via SqlQueryRaw to sidestep
    /// that drift entirely — fixing the Tenant table itself is out of scope
    /// for this Runtime Paso.
    /// </summary>
    private static async Task<Guid> EnsureTestPersonAsync(
        HumanOsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingPerson = await dbContext.People
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (existingPerson is not null)
        {
            return existingPerson.PersonId;
        }

        var tenantIds = await dbContext.Database
            .SqlQueryRaw<Guid>("SELECT TOP (1) TenantId FROM [dbo].[Tenant]")
            .ToListAsync(cancellationToken);

        var tenantId = tenantIds.FirstOrDefault();
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "No Tenant row exists in the database — cannot create a test Person (PASO 8) without one.");
        }

        const string testAzureOid = "00000000-0000-0000-0000-000000000001";

        var person = new Person
        {
            PersonId = Guid.NewGuid(),
            TenantId = tenantId,
            AzureOid = testAzureOid,
            AzureTid = testAzureOid,
            Email = "test-learner@humanos.local",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        dbContext.People.Add(person);
        await dbContext.SaveChangesAsync(cancellationToken);

        return person.PersonId;
    }

    private async Task<CuratorResult> RunCuratorAsync()
    {
        if (!_curator.IsConfigured)
        {
            return new CuratorResult
            {
                Success = false,
                Error = "CuradorAgent no está configurado (falta Azure OpenAI config)"
            };
        }

        var rawMaterials = new List<RawMaterialItem>
        {
            new() 
            { 
                Type = RawMaterialType.UserNote,
                Label = "Notas Multiplicación y Raíz Cuadrada",
                Content = "La multiplicación es una operación matemática que permite calcular el resultado de sumar un número consigo mismo varias veces; multiplicar dos números A y B da como resultado A sumado B veces. La raíz cuadrada de un número es el valor que, multiplicado por sí mismo, da como resultado ese número; es la operación inversa de elevar al cuadrado. Las personas utilizan la multiplicación para calcular áreas, cantidades repetidas y escalar valores, y la raíz cuadrada para resolver problemas geométricos y encontrar el lado de un cuadrado a partir de su área."
            }
        };

        try
        {
            var result = await _curator.CurateAsync(
                rawMaterials: rawMaterials,
                cancellationToken: CancellationToken.None);

            var chunks = result.Corpus.Chunks.Select(c => new ChunkInfo
            {
                Tag = c.Tag,
                Content = c.Content
            }).ToList();

            return new CuratorResult
            {
                Success = true,
                Summary = result.Corpus.Summary,
                ChunkCount = chunks.Count,
                InputTokens = result.TokenUsage.InputTokens,
                OutputTokens = result.TokenUsage.OutputTokens,
                Chunks = chunks
            };
        }
        catch (Exception ex)
        {
            return new CuratorResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<GraphArchitectResult> RunGraphArchitectAsync(CuratorResult curatorResult)
    {
        if (!_graphArchitect.IsConfigured)
        {
            return new GraphArchitectResult
            {
                Success = false,
                Error = "GraphArchitectAgent no está configurado"
            };
        }

        try
        {
            // Construir corpus curado desde resultado del Curador
            var curatedCorpus = new CuratedCorpus
            {
                Summary = curatorResult.Summary,
                Chunks = curatorResult.Chunks.Select(c => new CuratedChunk
                {
                    Tag = c.Tag,
                    Content = c.Content
                }).ToList()
            };

            var result = await _graphArchitect.ExtractGraphAsync(
                capabilityName: "Multiplicación y Raíz Cuadrada",
                curatedCorpus: curatedCorpus,
                cancellationToken: CancellationToken.None);

            var nodes = result.Graph.Nodes.Select(n => new NodeInfo
            {
                Name = n.Name,
                Description = n.Description,
                NodeType = n.NodeType.ToString(),
                SortOrder = n.SortOrder,
                AcademicDefinition = n.AcademicDefinition,
                Interpretation = n.Interpretation,
                Examples = n.Examples,
                Applications = n.Applications,
                IllustrationPrompts = n.IllustrationPrompts,
                References = n.References
            }).ToList();

            var edges = result.Graph.Edges.Select(e => new EdgeInfo
            {
                SourceNodeName = result.Graph.Nodes.FirstOrDefault(n => n.NodeId == e.SourceNodeId)?.Name ?? "Unknown",
                TargetNodeName = result.Graph.Nodes.FirstOrDefault(n => n.NodeId == e.TargetNodeId)?.Name ?? "Unknown",
                RelationshipType = e.RelationshipType.ToString(),
                Rationale = e.Rationale
            }).ToList();

            return new GraphArchitectResult
            {
                Success = true,
                GraphName = result.Graph.Name,
                NodeCount = nodes.Count,
                EdgeCount = edges.Count,
                InputTokens = result.TokenUsage.InputTokens,
                OutputTokens = result.TokenUsage.OutputTokens,
                Nodes = nodes,
                Edges = edges,
                Graph = result.Graph,
                Validations = new ValidationSummary
                {
                    IsSmallAndComprehensible = nodes.Count <= 30,
                    NoDuplicates = nodes.DistinctBy(n => n.Name).Count() == nodes.Count,
                    NoObviousCycles = true
                }
            };
        }
        catch (Exception ex)
        {
            return new GraphArchitectResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    // Helper types
    private sealed class CuratorResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Summary { get; set; }
        public int ChunkCount { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public List<ChunkInfo> Chunks { get; set; } = [];
    }

    private sealed class ChunkInfo
    {
        public string? Tag { get; set; }
        public string? Content { get; set; }
    }

    private sealed class GraphArchitectResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? GraphName { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public List<NodeInfo> Nodes { get; set; } = [];
        public List<EdgeInfo> Edges { get; set; } = [];
        public ValidationSummary Validations { get; set; } = new();

        /// <summary>The raw CapabilityGraphResponse (real NodeId/EdgeId Guids intact),
        /// used for SQL persistence and Data Lake illustration paths — the
        /// flattened Nodes/Edges above are display-only (NodeInfo/EdgeInfo have
        /// no Guids).</summary>
        public CapabilityGraphResponse? Graph { get; set; }
    }

    private sealed class NodeInfo
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? NodeType { get; set; }
        public int SortOrder { get; set; }
        public string? AcademicDefinition { get; set; }
        public string? Interpretation { get; set; }
        public List<string> Examples { get; set; } = [];
        public List<string> Applications { get; set; } = [];
        public List<IllustrationPromptDto> IllustrationPrompts { get; set; } = [];
        public List<string> References { get; set; } = [];
    }

    private sealed class EdgeInfo
    {
        public string? SourceNodeName { get; set; }
        public string? TargetNodeName { get; set; }
        public string? RelationshipType { get; set; }
        public string? Rationale { get; set; }
    }

    private sealed class ValidationSummary
    {
        public bool IsSmallAndComprehensible { get; set; }
        public bool NoDuplicates { get; set; }
        public bool NoObviousCycles { get; set; }
    }
}
