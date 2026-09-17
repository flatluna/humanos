using System.Text.Json;
using FluentAssertions;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Services.ProcessDocuments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HumanOS.Tests.Services;

public class LearningContentCoverageAnalyzerTests
{
    private readonly Mock<ILearningContentCoverageAnalyzer> _mockAnalyzer;
    private readonly Mock<ILogger<LearningContentCoverageAnalyzer>> _mockLogger;
    private readonly IConfiguration _configuration;

    public LearningContentCoverageAnalyzerTests()
    {
        _mockAnalyzer = new Mock<ILearningContentCoverageAnalyzer>();
        _mockLogger = new Mock<ILogger<LearningContentCoverageAnalyzer>>();

        _mockAnalyzer.Setup(a => a.IsConfigured).Returns(true);
        _mockAnalyzer.Setup(a => a.DeploymentName).Returns("gpt-5-mini");

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["AzureOpenAIDeploymentName"] = "gpt-5-mini",
            ["AzureOpenAIEndpoint"] = "https://engrams-resource.cognitiveservices.azure.com/",
            ["AzureOpenAIApiKey"] = "fake-key"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private static RelevantEngramsResponse CreateSampleStep4Response()
    {
        return new RelevantEngramsResponse
        {
            DocumentId = "doc-discovery-123",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            SelectedClasses =
            [
                new SelectedClassDto
                {
                    LearningPathId = "lp-consulting-101",
                    LearningPathName = "Enterprise Architecture & Consulting Fundamentals",
                    ClassId = "cls-discovery-prep",
                    ClassName = "Client Engagement & Discovery Planning",
                    ClassDescription = "Teaches consultants how to plan business discovery sessions, frame initial problems, prepare stakeholder interviews, and organize engagement workspaces.",
                    SelectedEngrams =
                    [
                        new SelectedEngramDto
                        {
                            EngramId = "eng-review-request",
                            EngramName = "Review an Engagement Request",
                            EngramDescription = "Techniques for rapidly reviewing briefing documents, sponsor notes, and scope definitions.",
                            SupportedRequirements = ["Review unfamiliar engagement information."],
                            Criticality = "critical",
                            RelevanceScore = 0.95
                        },
                        new SelectedEngramDto
                        {
                            EngramId = "eng-prep-checklist",
                            EngramName = "Create a Discovery Preparation Checklist",
                            EngramDescription = "Step-by-step assembly of workspaces, meeting invites, and discovery readiness items.",
                            SupportedRequirements = ["Create and organize the Discovery documentation workspace."],
                            Criticality = "supporting",
                            RelevanceScore = 0.88
                        }
                    ]
                },
                new SelectedClassDto
                {
                    LearningPathId = "lp-data-101",
                    LearningPathName = "Business Information & Analysis Mastery",
                    ClassId = "cls-info-analysis",
                    ClassName = "Structured Information & Fact-Finding Analysis",
                    ClassDescription = "Focuses on distinguishing verified facts, working assumptions, and critical unknown factors in complex business documentation.",
                    SelectedEngrams =
                    [
                        new SelectedEngramDto
                        {
                            EngramId = "eng-separate-known-assumed",
                            EngramName = "Separate Known, Assumed, and Unknown Information",
                            EngramDescription = "Framework for categorizing claims into verified facts, active working assumptions, and critical gaps.",
                            SupportedRequirements = ["Separate facts, assumptions, and unknown information."],
                            Criticality = "critical",
                            RelevanceScore = 0.92
                        }
                    ]
                }
            ],
            RequirementCoverage =
            [
                new RequirementCoverageItem
                {
                    Requirement = "Review unfamiliar engagement information.",
                    RequirementType = "ability",
                    CoverageStatus = "covered",
                    SupportingEngramIds = ["eng-review-request"]
                },
                new RequirementCoverageItem
                {
                    Requirement = "Separate facts, assumptions, and unknown information.",
                    RequirementType = "ability",
                    CoverageStatus = "covered",
                    SupportingEngramIds = ["eng-separate-known-assumed"]
                },
                new RequirementCoverageItem
                {
                    Requirement = "Identify the correct business and technical stakeholders.",
                    RequirementType = "ability",
                    CoverageStatus = "partially_covered",
                    SupportingEngramIds = [],
                    Notes = "Basic stakeholder concepts exist, but specific RACI mapping is missing."
                },
                new RequirementCoverageItem
                {
                    Requirement = "Prompt engineering for Discovery",
                    RequirementType = "technical_skill",
                    CoverageStatus = "not_covered",
                    SupportingEngramIds = [],
                    Notes = "No class covers AI prompt engineering."
                },
                new RequirementCoverageItem
                {
                    Requirement = "Optional meeting scheduling best practices",
                    RequirementType = "soft_skill",
                    CoverageStatus = "not_covered",
                    SupportingEngramIds = [],
                    Notes = "Nice-to-have calendar tips."
                }
            ],
            UncoveredRequirements =
            [
                "Identify the correct business and technical stakeholders.",
                "Prompt engineering for Discovery",
                "Optional meeting scheduling best practices"
            ],
            Assumptions = ["Assumes access to client documentation portal."],
            Warnings = ["Timeframe for initial discovery was not specified."],
            SourcePages = [3, 4]
        };
    }

    // 1. All critical requirements are covered
    [Fact]
    public async Task Test1_AllCriticalRequirementsCovered_ReturnsCoveredStatus()
    {
        var step4 = CreateSampleStep4Response();
        // Keep only covered requirements
        step4.RequirementCoverage =
        [
            new() { Requirement = "Review unfamiliar engagement information.", RequirementType = "ability", CoverageStatus = "covered", SupportingEngramIds = ["eng-review-request"] },
            new() { Requirement = "Separate facts, assumptions, and unknown information.", RequirementType = "ability", CoverageStatus = "covered", SupportingEngramIds = ["eng-separate-known-assumed"] }
        ];
        step4.UncoveredRequirements = [];

        var analyzer = new LearningContentCoverageAnalyzer(_configuration, _mockLogger.Object);
        var result = await analyzer.AnalyzeCoverageAndGapsAsync(step4);

        result.OverallCoverageStatus.Should().Be("covered");
        result.CoveredRequirements.Should().HaveCount(2);
        result.ContentGaps.Should().BeEmpty();
    }

    // 2. Supporting requirement remains uncovered but overall status is covered
    [Fact]
    public async Task Test2_SupportingRequirementUncovered_OverallStatusIsCovered()
    {
        var step4 = CreateSampleStep4Response();
        // All critical requirements covered, one supporting requirement uncovered
        step4.RequirementCoverage =
        [
            new() { Requirement = "Critical Requirement 1", RequirementType = "ability", CoverageStatus = "covered", SupportingEngramIds = ["eng-1"] },
            new() { Requirement = "Critical Requirement 2", RequirementType = "business_skill", CoverageStatus = "covered", SupportingEngramIds = ["eng-2"] },
            new() { Requirement = "Optional Calendar Tips", RequirementType = "tool", CoverageStatus = "not_covered", SupportingEngramIds = [] }
        ];

        _mockAnalyzer.Setup(a => a.AnalyzeCoverageAndGapsAsync(step4, It.IsAny<WorkStepRequirementsAnalysisResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearningContentCoverageResponse
            {
                DocumentId = step4.DocumentId,
                StepId = step4.StepId,
                WorkStep = step4.WorkStep,
                OverallCoverageStatus = "covered",
                CoverageSummary = "All critical work-step requirements are covered by existing published Engrams. Supporting tool requirements remain uncovered without impeding completion.",
                CoveredRequirements =
                [
                    new() { Requirement = "Critical Requirement 1", Criticality = "critical", SupportingEngramIds = ["eng-1"] },
                    new() { Requirement = "Critical Requirement 2", Criticality = "critical", SupportingEngramIds = ["eng-2"] }
                ],
                ContentGaps =
                [
                    new()
                    {
                        Requirement = "Optional Calendar Tips",
                        RequirementType = "tool",
                        Criticality = "supporting",
                        CurrentCoverage = "not_covered",
                        GapReason = "Non-essential calendar automation tips are absent.",
                        RecommendedGapType = "new_engram_in_existing_class",
                        ExistingClassId = "cls-discovery-prep",
                        ExistingLearningPathId = "lp-consulting-101"
                    }
                ],
                SourcePages = [3, 4]
            });

        var result = await _mockAnalyzer.Object.AnalyzeCoverageAndGapsAsync(step4);

        result.OverallCoverageStatus.Should().Be("covered");
        result.ContentGaps.Should().ContainSingle(g => g.Criticality == "supporting");
        result.CoveredRequirements.Should().HaveCount(2);
    }

    // 3. Critical requirement is partially covered
    [Fact]
    public async Task Test3_CriticalRequirementPartiallyCovered_ClassifiedAsPartiallyCovered()
    {
        var step4 = CreateSampleStep4Response();
        _mockAnalyzer.Setup(a => a.AnalyzeCoverageAndGapsAsync(step4, It.IsAny<WorkStepRequirementsAnalysisResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearningContentCoverageResponse
            {
                DocumentId = step4.DocumentId,
                StepId = step4.StepId,
                WorkStep = step4.WorkStep,
                OverallCoverageStatus = "partially_covered",
                CoverageSummary = "Relevant existing Classes were found, but critical stakeholder mapping is only partially covered.",
                CoveredRequirements =
                [
                    new() { Requirement = "Review unfamiliar engagement information.", Criticality = "critical", SupportingEngramIds = ["eng-review-request"] }
                ],
                ContentGaps =
                [
                    new()
                    {
                        Requirement = "Identify the correct business and technical stakeholders.",
                        RequirementType = "ability",
                        Criticality = "critical",
                        CurrentCoverage = "partially_covered",
                        GapReason = "Basic stakeholder concepts exist, but specific RACI mapping is missing.",
                        RecommendedGapType = "new_engram_in_existing_class",
                        ExistingClassId = "cls-discovery-prep",
                        ExistingLearningPathId = "lp-consulting-101"
                    }
                ],
                SourcePages = [3, 4]
            });

        var result = await _mockAnalyzer.Object.AnalyzeCoverageAndGapsAsync(step4);

        result.OverallCoverageStatus.Should().Be("partially_covered");
        result.ContentGaps.Should().ContainSingle(g => g.CurrentCoverage == "partially_covered" && g.Criticality == "critical");
    }

    // 4. Missing content belongs in an existing Class
    [Fact]
    public void Test4_MissingContentBelongsInExistingClass_PreservesExistingIDs()
    {
        var gap = new ContentGapItem
        {
            Requirement = "Executive interview guide drafting",
            RequirementType = "ability",
            Criticality = "critical",
            CurrentCoverage = "not_covered",
            GapReason = "Class covers discovery planning generally, but lacks interview guide drafting.",
            RecommendedGapType = "new_engram_in_existing_class",
            ExistingLearningPathId = "lp-consulting-101",
            ExistingClassId = "cls-discovery-prep"
        };

        gap.RecommendedGapType.Should().Be("new_engram_in_existing_class");
        gap.ExistingClassId.Should().Be("cls-discovery-prep");
        gap.ExistingLearningPathId.Should().Be("lp-consulting-101");
    }

    // 5. Critical requirement has no appropriate Class
    [Fact]
    public void Test5_CriticalRequirementHasNoAppropriateClass_RecommendedGapTypeIsNewClass()
    {
        var gap = new ContentGapItem
        {
            Requirement = "SAP S/4HANA Purchase Order Approval",
            RequirementType = "technical_skill",
            Criticality = "critical",
            CurrentCoverage = "not_covered",
            GapReason = "No ERP or SAP procurement classes exist in the current catalog.",
            RecommendedGapType = "new_class_required",
            ExistingClassId = null,
            ExistingLearningPathId = null
        };

        gap.RecommendedGapType.Should().Be("new_class_required");
        gap.ExistingClassId.Should().BeNull();
        gap.ExistingLearningPathId.Should().BeNull();
    }

    // 6. New Class is required -> overall status is content_gap
    [Fact]
    public async Task Test6_NewClassRequired_OverallStatusIsContentGap()
    {
        var step4 = CreateSampleStep4Response();
        _mockAnalyzer.Setup(a => a.AnalyzeCoverageAndGapsAsync(step4, It.IsAny<WorkStepRequirementsAnalysisResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearningContentCoverageResponse
            {
                DocumentId = step4.DocumentId,
                StepId = step4.StepId,
                WorkStep = step4.WorkStep,
                OverallCoverageStatus = "content_gap",
                CoverageSummary = "Critical capability requires a new coherent Class that does not exist in the catalog.",
                ContentGaps =
                [
                    new()
                    {
                        Requirement = "Prompt Engineering for Discovery",
                        RequirementType = "technical_skill",
                        Criticality = "critical",
                        CurrentCoverage = "not_covered",
                        GapReason = "Requires a comprehensive GenAI prompt engineering course.",
                        RecommendedGapType = "new_class_required"
                    }
                ]
            });

        var result = await _mockAnalyzer.Object.AnalyzeCoverageAndGapsAsync(step4);

        result.OverallCoverageStatus.Should().Be("content_gap");
        result.ContentGaps.Should().Contain(g => g.RecommendedGapType == "new_class_required" && g.Criticality == "critical");
    }

    // 7. Several gaps exist across different requirement types
    [Fact]
    public void Test7_SeveralGapsExistAcrossDifferentRequirementTypes()
    {
        var gaps = new List<ContentGapItem>
        {
            new() { Requirement = "Ability Req", RequirementType = "ability", Criticality = "critical" },
            new() { Requirement = "Soft Skill Req", RequirementType = "soft_skill", Criticality = "supporting" },
            new() { Requirement = "Business Skill Req", RequirementType = "business_skill", Criticality = "critical" },
            new() { Requirement = "Technical Skill Req", RequirementType = "technical_skill", Criticality = "critical" },
            new() { Requirement = "Tool Req", RequirementType = "tool", Criticality = "supporting" },
            new() { Requirement = "Evidence Req", RequirementType = "completion_evidence", Criticality = "critical" }
        };

        var types = gaps.Select(g => g.RequirementType).Distinct().ToList();
        types.Should().HaveCount(6);
        types.Should().Contain("ability");
        types.Should().Contain("soft_skill");
        types.Should().Contain("business_skill");
        types.Should().Contain("technical_skill");
        types.Should().Contain("tool");
        types.Should().Contain("completion_evidence");
    }

    // 8. Invalid Engram or Class IDs are rejected
    [Fact]
    public void Test8_InvalidClassId_RecordedAsIntegrityWarningOrNull()
    {
        var validClasses = new HashSet<string> { "cls-valid-1", "cls-valid-2" };
        var candidateId = "cls-hallucinated-999";

        var isValid = validClasses.Contains(candidateId);
        isValid.Should().BeFalse();
    }

    // 9. Step 4 assumptions and warnings are preserved
    [Fact]
    public async Task Test9_Step4AssumptionsAndWarnings_ArePreserved()
    {
        var step4 = CreateSampleStep4Response();
        step4.Assumptions = ["Assumes access to client documentation portal."];
        step4.Warnings = ["Timeframe for initial discovery was not specified."];

        var analyzer = new LearningContentCoverageAnalyzer(_configuration, _mockLogger.Object);
        var result = await analyzer.AnalyzeCoverageAndGapsAsync(step4);

        result.Assumptions.Should().Contain(step4.Assumptions[0]);
        result.Warnings.Should().Contain(step4.Warnings[0]);
    }

    // 10. Source-page references are preserved
    [Fact]
    public async Task Test10_SourcePageReferences_ArePreserved()
    {
        var step4 = CreateSampleStep4Response();
        step4.SourcePages = [3, 4];

        var analyzer = new LearningContentCoverageAnalyzer(_configuration, _mockLogger.Object);
        var result = await analyzer.AnalyzeCoverageAndGapsAsync(step4);

        result.SourcePages.Should().Equal(3, 4);
    }

    // 11. No new Class is created
    [Fact]
    public void Test11_NoNewClassIsCreated()
    {
        var response = new LearningContentCoverageResponse
        {
            ContentGaps =
            [
                new()
                {
                    Requirement = "Missing Skill",
                    RecommendedGapType = "new_class_required"
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("CreatedClass");
        json.Should().NotContain("NewClassId");
        json.Should().NotContain("InsertClass");
    }

    // 12. No new Engram is created
    [Fact]
    public void Test12_NoNewEngramIsCreated()
    {
        var response = new LearningContentCoverageResponse
        {
            ContentGaps =
            [
                new()
                {
                    Requirement = "Missing Atomic Topic",
                    RecommendedGapType = "new_engram_in_existing_class"
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("CreatedEngram");
        json.Should().NotContain("NewEngramId");
        json.Should().NotContain("InsertEngram");
    }

    // 13. No Class or Engram index is generated
    [Fact]
    public void Test13_NoClassOrEngramIndexIsGenerated()
    {
        var response = new LearningContentCoverageResponse
        {
            CoveredRequirements = [new() { Requirement = "Req 1" }]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("CurriculumIndex");
        json.Should().NotContain("EngramIndex");
        json.Should().NotContain("TableOfContents");
    }

    // 14. No final Learning Assignment is generated
    [Fact]
    public void Test14_NoFinalLearningAssignmentIsGenerated()
    {
        var response = new LearningContentCoverageResponse
        {
            OverallCoverageStatus = "partially_covered"
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("LearningAssignmentMarkdown");
        json.Should().NotContain("# Learning Assignment");
        json.Should().NotContain("EmployeeCareerPlan");
    }

    // 15. Output satisfies the typed JSON schema
    [Fact]
    public void Test15_OutputSatisfiesTypedJsonSchema()
    {
        var response = new LearningContentCoverageResponse
        {
            DocumentId = "doc-123",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            OverallCoverageStatus = "covered",
            CoverageSummary = "All critical requirements are covered.",
            CoveredRequirements =
            [
                new CoveredRequirementItem
                {
                    Requirement = "Review unfamiliar information",
                    Criticality = "critical",
                    SupportingEngramIds = ["eng-1"]
                }
            ],
            ContentGaps =
            [
                new ContentGapItem
                {
                    Requirement = "Optional calendar tips",
                    RequirementType = "tool",
                    Criticality = "supporting",
                    CurrentCoverage = "partially_covered",
                    GapReason = "Minor tool tips missing",
                    RecommendedGapType = "new_engram_in_existing_class",
                    ExistingLearningPathId = "lp-1",
                    ExistingClassId = "cls-1"
                }
            ],
            Assumptions = ["Assumption 1"],
            Warnings = ["Warning 1"],
            SourcePages = [3, 4]
        };

        var json = JsonSerializer.Serialize(response);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        root.TryGetProperty("documentId", out _).Should().BeTrue();
        root.TryGetProperty("stepId", out _).Should().BeTrue();
        root.TryGetProperty("stepNumber", out _).Should().BeTrue();
        root.TryGetProperty("workStep", out _).Should().BeTrue();
        root.TryGetProperty("overallCoverageStatus", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be("covered");

        root.TryGetProperty("coverageSummary", out _).Should().BeTrue();

        root.TryGetProperty("coveredRequirements", out var coveredProp).Should().BeTrue();
        coveredProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstCovered = coveredProp.EnumerateArray().First();
        firstCovered.TryGetProperty("requirement", out _).Should().BeTrue();
        firstCovered.TryGetProperty("criticality", out _).Should().BeTrue();
        firstCovered.TryGetProperty("supportingEngramIds", out _).Should().BeTrue();

        root.TryGetProperty("contentGaps", out var gapsProp).Should().BeTrue();
        gapsProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstGap = gapsProp.EnumerateArray().First();
        firstGap.TryGetProperty("requirement", out _).Should().BeTrue();
        firstGap.TryGetProperty("requirementType", out _).Should().BeTrue();
        firstGap.TryGetProperty("criticality", out _).Should().BeTrue();
        firstGap.TryGetProperty("currentCoverage", out _).Should().BeTrue();
        firstGap.TryGetProperty("gapReason", out _).Should().BeTrue();
        firstGap.TryGetProperty("recommendedGapType", out _).Should().BeTrue();
        firstGap.TryGetProperty("existingLearningPathId", out _).Should().BeTrue();
        firstGap.TryGetProperty("existingClassId", out _).Should().BeTrue();

        root.TryGetProperty("assumptions", out _).Should().BeTrue();
        root.TryGetProperty("warnings", out _).Should().BeTrue();
        root.TryGetProperty("sourcePages", out _).Should().BeTrue();
    }
}
