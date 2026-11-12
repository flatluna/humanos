using System.Diagnostics;
using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Analyzes one process step's own verbatim source content and determines the business-process attributes
/// (role, systems, trigger, business rules, exceptions) for that step. Runs lazily, one step at a time, so
/// that Phase A (bulk document index extraction) stays fast regardless of document size — see
/// <see cref="ProcessDocumentExtractionService"/> for Phase A.
/// </summary>
public sealed class StepBusinessRequirementsAnalyzer : IStepBusinessRequirementsAnalyzer
{
    private const string Instructions = """
        You are an expert business-process analyst. Your ONLY job is to analyze ONE already-extracted work step
        from a business process document (given as its own verbatim source text) and derive its business-process
        attributes. Do not invent facts that are not supported by the provided source text.

        Produce, for this single step:
        - primaryResponsibleRole: the single role/job title accountable for performing the step.
        - supportingRoles: other roles involved (short list, may be empty).
        - systemsAndTools: applications/systems/channels used to perform the step (short list).
        - trigger: the one short phrase describing what starts this step.
        - requiredInputs: short list of inputs/records/information needed before the step can start.
        - decisionsAndHumanJudgment: short list of decisions or judgment calls a human must make in this step.
        - businessRulesAndControls: short list of business rules, controls, or policies that govern this step.
        - exceptionsAndEscalations: short list of exception conditions and how/to whom they escalate.
        - evidenceProducedOrRequired: short list of tangible records/evidence produced by completing the step.
        - completionCondition: a short OBSERVABLE business condition that marks the step as complete (what
          record/decision/output exists) — never a vague statement like "the step is complete".
        - missingInformation: only if something important for this analysis is genuinely absent from the
          source text, otherwise leave empty.
        - activities: break this step's sourceContent down into the individual units of work a person/system
          performs, in order. For EACH activity return: activitySequence (1, 2, 3...), activityName,
          activityDescription, activityType (one of HumanKnowledge, HumanJudgment, HumanBehavior, HumanDecision,
          HumanCommunication, HumanSystemOperation, HumanVerificationOfAI, SystemAutomated, ExternalSystemOperation),
          primaryResponsibleRole, systemsAndTools, requiredInputs, outputs, businessRules (rules/controls
          governing just this activity), humanJudgmentRequired, aiContribution (empty if AI does not
          participate), humanVerificationRequired, evidenceRequired, requiredCapabilities (written as
          Verb + object + performance context, e.g. "Classify a customer complaint using approved business
          rules"), learningRelevance (one short phrase on why/how this matters for training a new hire, or
          empty if purely a system automation with no human learning need), suggestedLearningMethod (one of
          Explanation, GuidedWalkthrough, WorkedExample, Scenario, Simulation, Recall, Assessment,
          PracticeAndRetry, ExternalProductTraining, CompanyWorkInstruction), readinessCriteria (short list
          of observable signs a person is ready to perform this activity unsupervised), sourcePage, sourceText
          (short verbatim excerpt this activity was derived from), missingInformation. Do NOT split a single
          activity into more than one entry, and do NOT merge two clearly distinct actions into one activity.

        ==================================================
        CATEGORIZED CLASSIFICATION (required — each is a numbered checklist for this step)
        ==================================================
        - ownerRole / ownerIdentified: the single role/job title accountable for performing this step. Set
          ownerIdentified=true ONLY if the source text explicitly names that role. If the source does not
          explicitly state who is responsible, set ownerRole to "Role to be identified" and ownerIdentified=false
          — never guess a role that isn't stated.
        - inputs: each distinct input/record/piece of information needed before the step can start, one per item.
        - systems: each distinct system/application/channel used to perform the step, one per item.
        - dependencies: each distinct external dependency (other teams, processes, or systems this step relies on
          but does not directly use/perform). If none are identified in the source, return exactly ONE item with
          label "None identified in the source".
        - businessRules: each distinct business rule, control, or policy governing this step, one per item.
        - exceptions: each distinct exception condition and how/to whom it escalates, one per item.
        - outputs: each distinct tangible record/output produced by completing the step, one per item.

        CRITICAL RULE FOR ALL SIX CATEGORIES ABOVE (inputs, systems, dependencies, businessRules, exceptions,
        outputs): only include items the source text actually supports — NEVER invent a system, rule, or
        dependency that isn't mentioned. If a category has nothing identifiable in the source text, return
        exactly ONE item for that category with label "None identified in the source" instead of an empty list.

        Keep every field SHORT (phrases or single short sentences, never a full paragraph or a verbatim quote
        of the whole source, except sourceText which should be a short supporting excerpt). Only state what
        the source text actually supports.
        """;

    private readonly AIAgent? _agent;
    private readonly ILogger<StepBusinessRequirementsAnalyzer>? _logger;

    public StepBusinessRequirementsAnalyzer(
        IConfiguration configuration,
        ILogger<StepBusinessRequirementsAnalyzer>? logger = null)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Follow repository pattern: economy tier gpt-5-mini, fallback to main deployment
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        DeploymentName = deploymentName;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "StepBusinessRequirementsAnalyzer");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<StepBusinessRequirementsAnalysisResult> AnalyzeStepAsync(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "StepBusinessRequirementsAnalyzer is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' configuration keys.");
        }

        if (step is null)
        {
            throw new ArgumentNullException(nameof(step));
        }

        var stopwatch = Stopwatch.StartNew();

        var sb = new StringBuilder();
        sb.AppendLine($"Document ID: {documentId}");
        sb.AppendLine($"Process Name: {processName}");
        sb.AppendLine($"Chapter / Phase: {chapterOrPhase}");
        sb.AppendLine($"Step Number: {step.StepNumber}");
        sb.AppendLine($"Step Name: {step.StepName}");
        sb.AppendLine("\n=== STEP SOURCE CONTENT (verbatim, from the original document) ===");
        sb.AppendLine(string.IsNullOrWhiteSpace(step.SourceContent) ? step.StepName : step.SourceContent);
        sb.AppendLine("=== END OF STEP SOURCE CONTENT ===");

        try
        {
            var agentResponse = await _agent.RunAsync<StepBusinessRequirementsAnalysisResult>(
                sb.ToString(),
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            var result = agentResponse.Result ?? throw new InvalidOperationException("The agent returned a null analysis result.");
            result.DocumentId = documentId;
            result.StepId = step.StepId;
            result.StepNumber = step.StepNumber;

            _logger?.LogInformation(
                "Analyzed business requirements for step {StepId} in {DurationMs}ms (Deployment: {Model})",
                step.StepId,
                stopwatch.Elapsed.TotalMilliseconds,
                DeploymentName ?? "unknown");

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger?.LogError(
                ex,
                "Failed to analyze business requirements for step {StepId} of document {DocumentId} after {DurationMs}ms",
                step.StepId,
                documentId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
