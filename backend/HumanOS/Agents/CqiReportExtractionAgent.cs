using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.People;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>
/// CQI Workspace (HermanScience) — extracts structured cognitive/behavioral
/// profiling insights (leading pattern/type, anchor foundation, strengths,
/// growth direction, etc.) from the raw text of an uploaded CQI assessment
/// report PDF. Purely extractive, no tools — same rationale as
/// <see cref="ResumeExtractionAgent"/>. Singleton (holds only a configured
/// AIAgent instance, no per-request state).
/// </summary>
public sealed class CqiReportExtractionAgent
{
    private const string Instructions = """
        You are an expert at reading CQI (cognitive/behavioral) assessment reports — the kind
        produced by HermanScience — and extracting their content into structured JSON fields for
        an employee "CQI Workspace" feature. The workspace helps a person understand how they
        currently think, learn, communicate, make decisions, and work.

        The source text usually contains two parts: a "Shareable Report Preview" (numbered
        sections 01-11: Profile Snapshot, How This Person Tends To Work, Thinking and Decisions,
        Communication and Collaboration, Learning and Working Preferences, Natural Strengths,
        Patterns Worth Noticing, Working Effectively With AI, What Helps Them Thrive, What Strain
        Can Look Like, Growth Direction) and an "Anchor foundation" section (the stable type
        brief: type name/number, description, what may draw attention, how choices may take
        shape, work and communication, what may support balance, potential strengths, patterns to
        watch under pressure).

        Extract EVERY field faithfully from the source text — do not invent or embellish content
        that is not present. Preserve the source language. For narrative fields (ProfileSnapshot,
        HowTheyTendToWork, ThinkingAndDecisions, CommunicationAndCollaboration,
        LearningAndWorkingPreferences, WorkingEffectivelyWithAi, WhatHelpsThemThrive,
        WhatStrainCanLookLike, GrowthDirection, AnchorDescription, WhatMayDrawAttention,
        HowChoicesMayTakeShape, WorkAndCommunication, WhatMaySupportBalance), copy the relevant
        paragraph(s) as-is (light cleanup of stray whitespace/line breaks is fine).

        For list fields (NaturalStrengths, PatternsWorthNoticing, PotentialStrengths,
        PatternsToWatchUnderPressure), split into ONE item per bullet/strength/pattern — never a
        single concatenated string.

        LeadingPatternType is the short code (e.g. "Type 6"); LeadingPatternName is the pattern's
        name (e.g. "The Verifier"). AnchorTypeName is the full anchor heading (e.g.
        "Type 6: The Verifier"). WingInfluence captures any wing-influence phrase mentioned (e.g.
        "balanced Type 5/Type 7 wing influence"); leave empty if not present.

        If a field truly has no corresponding content in the source text, return an empty string
        or empty list for it rather than guessing.
        """;

    private readonly AIAgent? _agent;

    public CqiReportExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
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
            .AsAIAgent(instructions: Instructions, name: "CqiReportExtractionAgent");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<CqiProfileData> ExtractAsync(string reportText, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The CQI report extraction agent is not configured. Set the 'AzureOpenAIEndpoint' " +
                "and 'AzureOpenAIDeploymentName' application settings once credentials are provided.");
        }

        var prompt = "Extract the structured CQI profile fields from the following report text:\n\n" + reportText;

        var response = await _agent.RunAsync<CqiProfileData>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
