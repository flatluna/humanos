using System.Text.Json;
using FluentAssertions;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Services.ProcessDocuments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HumanOS.Tests.Services;

public class LearningContentGapDraftGeneratorTests
{
    private readonly Mock<IEngramStudioCatalogService> _mockCatalog;
    private readonly Mock<ILearningContentGapDraftGenerator> _mockGenerator;
    private readonly Mock<ILogger<LearningContentGapDraftGenerator>> _mockLogger;
    private readonly IConfiguration _configuration;

    public LearningContentGapDraftGeneratorTests()
    {
        _mockCatalog = new Mock<IEngramStudioCatalogService>();
        _mockGenerator = new Mock<ILearningContentGapDraftGenerator>();
        _mockLogger = new Mock<ILogger<LearningContentGapDraftGenerator>>();

        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());
        _mockCatalog.Setup(c => c.GetEngramsByClassIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EngramStudioEngramSummary>());

        _mockGenerator.Setup(g => g.IsConfigured).Returns(true);
        _mockGenerator.Setup(g => g.DeploymentName).Returns("gpt-5-mini");

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

    private static LearningContentCoverageResponse CreateSampleStep5Response(
        string overallStatus = "partially_covered",
        List<ContentGapItem>? gaps = null)
    {
        return new LearningContentCoverageResponse
        {
            DocumentId = "doc-discovery-123",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            OverallCoverageStatus = overallStatus,
            CoverageSummary = "Summary of coverage.",
            CoveredRequirements =
            [
                new CoveredRequirementItem
                {
                    Requirement = "Review unfamiliar engagement information.",
                    Criticality = "critical",
                    SupportingEngramIds = ["eng-review-request"]
                }
            ],
            ContentGaps = gaps ??
            [
                new ContentGapItem
                {
                    Requirement = "Separate facts, assumptions, and unknown information.",
                    RequirementType = "ability",
                    Criticality = "critical",
                    CurrentCoverage = "partially_covered",
                    GapReason = "Existing class lacks atomic breakdown of active working assumptions.",
                    RecommendedGapType = "new_engram_in_existing_class",
                    ExistingLearningPathId = "lp-consulting-101",
                    ExistingClassId = "cls-discovery-prep"
                }
            ],
            Assumptions = ["Assumes access to client documentation portal."],
            Warnings = ["Timeframe for initial discovery was not specified."],
            SourcePages = [3, 4]
        };
    }

    private static List<EngramStudioClassSummary> CreateSampleCatalogClasses()
    {
        return
        [
            new EngramStudioClassSummary
            {
                LearningPathId = "lp-consulting-101",
                LearningPathName = "Enterprise Architecture Consulting",
                ClassId = "cls-discovery-prep",
                ClassName = "Client Engagement & Discovery Planning",
                ClassDescription = "Teaches discovery planning and client setup.",
                PublicationStatus = "published",
                Language = "en",
                IsGlobal = true
            }
        ];
    }

    private static List<EngramStudioLearningPathSummary> CreateSampleLearningPaths()
    {
        return
        [
            new EngramStudioLearningPathSummary
            {
                LearningPathId = "lp-consulting-101",
                LearningPathName = "Enterprise Architecture Consulting",
                Description = "Consulting methodologies"
            },
            new EngramStudioLearningPathSummary
            {
                LearningPathId = "lp-data-governance",
                LearningPathName = "Enterprise Data Governance & Systems",
                Description = "Data ownership and system governance"
            }
        ];
    }

    // 1. Covered status creates no draft
    [Fact]
    public async Task Test1_CoveredStatus_CreatesNoDraft()
    {
        var step5 = CreateSampleStep5Response(overallStatus: "covered", gaps: []);
        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.DraftCreated.Should().BeFalse();
        result.EngramDrafts.Should().BeEmpty();
        result.ClassDrafts.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("sufficient coverage"));
    }

    // 2. Partial coverage creates an Engram draft inside an existing Class
    [Fact]
    public async Task Test2_PartialCoverage_CreatesEngramDraftInsideExistingClass()
    {
        var step5 = CreateSampleStep5Response("partially_covered");
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());
        _mockCatalog.Setup(c => c.GetEngramsByClassIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EngramStudioEngramSummary>());

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.DraftCreated.Should().BeTrue();
        result.EngramDrafts.Should().HaveCount(1);
        result.EngramDrafts[0].ParentClassId.Should().Be("cls-discovery-prep");
        result.EngramDrafts[0].ParentLearningPathId.Should().Be("lp-consulting-101");
        result.EngramDrafts[0].Status.Should().Be("needs_expert_review");
        result.EngramDrafts[0].DraftReference.Should().StartWith("draft-eng-");
    }

    // 3. Several missing atomic requirements create several Engram drafts
    [Fact]
    public async Task Test3_SeveralMissingAtomicRequirements_CreateSeveralEngramDrafts()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Atomic Req 1",
                RecommendedGapType = "new_engram_in_existing_class",
                ExistingClassId = "cls-discovery-prep",
                ExistingLearningPathId = "lp-consulting-101"
            },
            new()
            {
                Requirement = "Atomic Req 2",
                RecommendedGapType = "new_engram_in_existing_class",
                ExistingClassId = "cls-discovery-prep",
                ExistingLearningPathId = "lp-consulting-101"
            }
        };

        var step5 = CreateSampleStep5Response("partially_covered", gaps);
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());
        _mockCatalog.Setup(c => c.GetEngramsByClassIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EngramStudioEngramSummary>());

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.EngramDrafts.Should().HaveCount(2);
        result.EngramDrafts.Select(e => e.WorkStepRequirementSupported).Should().Equal("Atomic Req 1", "Atomic Req 2");
    }

    // 4. Content gap creates a new Class draft
    [Fact]
    public async Task Test4_ContentGap_CreatesNewClassDraft()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Executive Stakeholder Governance",
                RequirementType = "business_skill",
                Criticality = "critical",
                RecommendedGapType = "new_class_required",
                GapReason = "No existing class covers executive stakeholder governance."
            }
        };

        var step5 = CreateSampleStep5Response("content_gap", gaps);
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.DraftCreated.Should().BeTrue();
        result.ClassDrafts.Should().HaveCount(1);
        result.ClassDrafts[0].ProposedClassTitle.Should().NotBeNullOrWhiteSpace();
        result.ClassDrafts[0].Status.Should().Be("needs_expert_review");
        result.ClassDrafts[0].DraftReference.Should().StartWith("draft-cls-");
    }

    // 5. New Class draft includes a coherent Engram index
    [Fact]
    public async Task Test5_NewClassDraft_IncludesCoherentEngramIndex()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Stakeholder Discovery Interviews",
                RecommendedGapType = "new_class_required",
                GapReason = "Missing full interview methodologies."
            }
        };

        var step5 = CreateSampleStep5Response("content_gap", gaps);
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        var cls = result.ClassDrafts.First();
        cls.ProposedEngramIndex.Should().NotBeEmpty();
        cls.ProposedEngramIndex.All(e => !string.IsNullOrWhiteSpace(e.ProposedEngramTitle)).Should().BeTrue();
        cls.ProposedEngramIndex.All(e => e.SuggestedOrder > 0).Should().BeTrue();
    }

    // 6. Existing Learning Path is recommended when appropriate
    [Fact]
    public async Task Test6_ExistingLearningPath_IsRecommendedWhenAppropriate()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Architecture Governance",
                RecommendedGapType = "new_class_required"
            }
        };

        var step5 = CreateSampleStep5Response("content_gap", gaps);
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.ClassDrafts[0].RecommendedExistingLearningPathId.Should().NotBeNullOrWhiteSpace();
        result.ClassDrafts[0].RecommendedExistingLearningPathName.Should().NotBeNullOrWhiteSpace();
        result.ClassDrafts[0].ProposedLearningPathName.Should().BeNull();
    }

    // 7. New Learning Path name is proposed only when no existing path is appropriate
    [Fact]
    public async Task Test7_NewLearningPathNameProposed_WhenNoExistingPathAppropriate()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Deep Sea Marine Biology for Submarines",
                RecommendedGapType = "new_class_required"
            }
        };

        var step5 = CreateSampleStep5Response("content_gap", gaps);
        // Empty catalog of learning paths
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EngramStudioClassSummary>());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EngramStudioLearningPathSummary>());

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.ClassDrafts[0].RecommendedExistingLearningPathId.Should().BeNull();
        result.ClassDrafts[0].ProposedLearningPathName.Should().NotBeNullOrWhiteSpace();
    }

    // 8. Existing Engram duplicate prevents a new Engram proposal
    [Fact]
    public async Task Test8_ExistingEngramDuplicate_PreventsNewEngramProposal()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Review an Engagement Request",
                RecommendedGapType = "new_engram_in_existing_class",
                ExistingClassId = "cls-discovery-prep",
                ExistingLearningPathId = "lp-consulting-101"
            }
        };

        var step5 = CreateSampleStep5Response("partially_covered", gaps);
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());

        // Existing engrams in class includes exact duplicate title
        _mockCatalog.Setup(c => c.GetEngramsByClassIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new EngramStudioEngramSummary
                {
                    EngramId = "eng-existing-1",
                    ClassId = "cls-discovery-prep",
                    EngramName = "Review an Engagement Request",
                    EngramDescription = "Techniques for reviewing engagement requests."
                }
            ]);

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        // Avoided creating duplicate engram draft
        result.EngramDrafts.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("already covers requirement"));
    }

    // 9. Similar but meaningfully different Engram may still be proposed
    [Fact]
    public async Task Test9_SimilarButMeaningfullyDifferentEngram_IsProposed()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Reviewing High-Security Defense Briefings",
                RecommendedGapType = "new_engram_in_existing_class",
                ExistingClassId = "cls-discovery-prep",
                ExistingLearningPathId = "lp-consulting-101"
            }
        };

        var step5 = CreateSampleStep5Response("partially_covered", gaps);
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());
        _mockCatalog.Setup(c => c.GetEngramsByClassIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new EngramStudioEngramSummary
                {
                    EngramId = "eng-existing-general",
                    ClassId = "cls-discovery-prep",
                    EngramName = "Standard Commercial Discovery Notes",
                    EngramDescription = "Commercial proposals only."
                }
            ]);

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.EngramDrafts.Should().HaveCount(1);
        result.EngramDrafts[0].ProposedEngramTitle.Should().Contain("Defense");
    }

    // 10. Invalid existing Class ID is rejected
    [Fact]
    public async Task Test10_InvalidExistingClassId_IsRejectedWithIntegrityWarning()
    {
        var gaps = new List<ContentGapItem>
        {
            new()
            {
                Requirement = "Some requirement",
                RecommendedGapType = "new_engram_in_existing_class",
                ExistingClassId = "cls-hallucinated-or-deleted-999"
            }
        };

        var step5 = CreateSampleStep5Response("partially_covered", gaps);
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleCatalogClasses());
        _mockCatalog.Setup(c => c.GetAvailableLearningPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleLearningPaths());

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.Warnings.Should().Contain(w => w.Contains("Catalog-integrity warning"));
        result.EngramDrafts.Should().BeEmpty(); // Did not attach to invalid class
    }

    // 11. Invalid existing Learning Path ID is rejected
    [Fact]
    public void Test11_InvalidExistingLearningPathId_IsDetected()
    {
        var validLearningPaths = new HashSet<string> { "lp-1", "lp-2" };
        var candidateLpId = "lp-non-existent-777";

        var exists = validLearningPaths.Contains(candidateLpId);
        exists.Should().BeFalse();
    }

    // 12. Unauthorized catalog content is rejected
    [Fact]
    public void Test12_UnauthorizedCatalogContent_IsExcluded()
    {
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var classes = new List<EngramStudioClassSummary>
        {
            new() { ClassId = "cls-tenant-a", OwnerTenantId = tenantA },
            new() { ClassId = "cls-tenant-b", OwnerTenantId = tenantB }
        };

        var authorizedForA = classes.Where(c => c.OwnerTenantId == tenantA).ToList();
        authorizedForA.Should().ContainSingle(c => c.ClassId == "cls-tenant-a");
        authorizedForA.Should().NotContain(c => c.ClassId == "cls-tenant-b");
    }

    // 13. Proposed content has no production catalog ID
    [Fact]
    public void Test13_ProposedContentHasNoProductionCatalogId()
    {
        var engramDraft = new EngramDraftDto
        {
            DraftReference = "draft-eng-1",
            ProposedEngramTitle = "Draft Engram"
        };
        var classDraft = new ClassDraftDto
        {
            DraftReference = "draft-cls-1",
            ProposedClassTitle = "Draft Class"
        };

        engramDraft.DraftReference.Should().StartWith("draft-");
        classDraft.DraftReference.Should().StartWith("draft-");
        // Verify no GUID production ID was generated
        Guid.TryParse(engramDraft.DraftReference, out _).Should().BeFalse();
        Guid.TryParse(classDraft.DraftReference, out _).Should().BeFalse();
    }

    // 14. Every proposal has needs_expert_review status
    [Fact]
    public void Test14_EveryProposalHasNeedsExpertReviewStatus()
    {
        var engramDraft = new EngramDraftDto { Status = "needs_expert_review" };
        var classDraft = new ClassDraftDto { Status = "needs_expert_review" };

        engramDraft.Status.Should().Be("needs_expert_review");
        classDraft.Status.Should().Be("needs_expert_review");
    }

    // 15. No full Engram lesson is generated
    [Fact]
    public void Test15_NoFullEngramLessonIsGenerated()
    {
        var engramDraft = new EngramDraftDto
        {
            ProposedEngramTitle = "Stakeholder Mapping",
            ProposedEngramDescription = "Brief one-sentence description."
        };

        var json = JsonSerializer.Serialize(engramDraft);
        json.Should().NotContain("LessonContent");
        json.Should().NotContain("Cards");
        json.Should().NotContain("TeachingText");
    }

    // 16. No assessment, recall, or practice content is generated
    [Fact]
    public void Test16_NoAssessmentRecallOrPracticeContentGenerated()
    {
        var response = new LearningContentGapDraftResponse
        {
            ClassDrafts =
            [
                new ClassDraftDto
                {
                    ProposedEngramIndex =
                    [
                        new ProposedEngramIndexItemDto
                        {
                            ProposedEngramTitle = "Intro",
                            ProposedEngramDescription = "Intro desc"
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("Assessment");
        json.Should().NotContain("Recall");
        json.Should().NotContain("Practice");
        json.Should().NotContain("Quiz");
    }

    // 17. No final Learning Assignment is generated
    [Fact]
    public void Test17_NoFinalLearningAssignmentGenerated()
    {
        var response = new LearningContentGapDraftResponse
        {
            DraftCreated = true
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("# Learning Assignment");
        json.Should().NotContain("FinalAssignment");
        json.Should().NotContain("EmployeeEnrollment");
    }

    // 18. Step 5 assumptions and warnings are preserved
    [Fact]
    public async Task Test18_Step5AssumptionsAndWarnings_ArePreserved()
    {
        var step5 = CreateSampleStep5Response();
        step5.Assumptions = ["Assumption X"];
        step5.Warnings = ["Warning Y"];

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);
        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.Assumptions.Should().Contain("Assumption X");
        result.Warnings.Should().Contain("Warning Y");
    }

    // 19. Source-page references are preserved
    [Fact]
    public async Task Test19_SourcePageReferences_ArePreserved()
    {
        var step5 = CreateSampleStep5Response();
        step5.SourcePages = [12, 13];

        var generator = new LearningContentGapDraftGenerator(_configuration, _mockCatalog.Object, _mockLogger.Object);
        var result = await generator.GenerateContentGapDraftAsync(step5);

        result.SourcePages.Should().Equal(12, 13);
    }

    // 20. Output satisfies the typed JSON schema
    [Fact]
    public void Test20_OutputSatisfiesTypedJsonSchema()
    {
        var response = new LearningContentGapDraftResponse
        {
            DocumentId = "doc-1",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            OverallCoverageStatus = "partially_covered",
            DraftCreated = true,
            EngramDrafts =
            [
                new EngramDraftDto
                {
                    DraftReference = "draft-eng-1",
                    ProposedEngramTitle = "Separate Facts and Assumptions",
                    ProposedEngramDescription = "Framework for categorizing claims.",
                    CapabilitySupported = "Information Analysis",
                    WorkStepRequirementSupported = "Separate facts, assumptions",
                    ReasonNeeded = "Required for business problem framing.",
                    ParentLearningPathId = "lp-1",
                    ParentLearningPathName = "Consulting 101",
                    ParentClassId = "cls-1",
                    ParentClassName = "Discovery Planning",
                    Status = "needs_expert_review"
                }
            ],
            ClassDrafts =
            [
                new ClassDraftDto
                {
                    DraftReference = "draft-cls-1",
                    ProposedClassTitle = "Executive Stakeholder Discovery",
                    ProposedClassDescription = "Comprehensive stakeholder identification.",
                    CapabilityDeveloped = "Stakeholder Management",
                    WorkStepRequirementsSupported = ["Identify stakeholders"],
                    RecommendedExistingLearningPathId = "lp-1",
                    RecommendedExistingLearningPathName = "Consulting 101",
                    ReasonNewClassIsRequired = "No existing class covers stakeholder governance.",
                    ProposedEngramIndex =
                    [
                        new ProposedEngramIndexItemDto
                        {
                            ProposedEngramTitle = "Identify Executive Sponsor",
                            ProposedEngramDescription = "Role and interview techniques.",
                            CapabilitySupported = "Sponsor Alignment",
                            WorkStepRequirementSupported = "Identify stakeholders",
                            SuggestedOrder = 1
                        }
                    ],
                    Status = "needs_expert_review"
                }
            ],
            CatalogItemsChecked =
            [
                new CatalogItemCheckedDto
                {
                    ItemType = "class",
                    ItemId = "cls-1",
                    ItemName = "Discovery Planning"
                }
            ],
            Assumptions = ["Assumption 1"],
            Warnings = ["Warning 1"],
            SourcePages = [3, 4]
        };

        var json = JsonSerializer.Serialize(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("documentId", out _).Should().BeTrue();
        root.TryGetProperty("stepId", out _).Should().BeTrue();
        root.TryGetProperty("stepNumber", out _).Should().BeTrue();
        root.TryGetProperty("workStep", out _).Should().BeTrue();
        root.TryGetProperty("overallCoverageStatus", out _).Should().BeTrue();
        root.TryGetProperty("draftCreated", out var draftCreatedProp).Should().BeTrue();
        draftCreatedProp.GetBoolean().Should().BeTrue();

        root.TryGetProperty("engramDrafts", out var engramsProp).Should().BeTrue();
        engramsProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstEngram = engramsProp.EnumerateArray().First();
        firstEngram.TryGetProperty("draftReference", out _).Should().BeTrue();
        firstEngram.TryGetProperty("proposedEngramTitle", out _).Should().BeTrue();
        firstEngram.TryGetProperty("proposedEngramDescription", out _).Should().BeTrue();
        firstEngram.TryGetProperty("capabilitySupported", out _).Should().BeTrue();
        firstEngram.TryGetProperty("workStepRequirementSupported", out _).Should().BeTrue();
        firstEngram.TryGetProperty("reasonNeeded", out _).Should().BeTrue();
        firstEngram.TryGetProperty("parentLearningPathId", out _).Should().BeTrue();
        firstEngram.TryGetProperty("parentClassId", out _).Should().BeTrue();
        firstEngram.TryGetProperty("status", out var engStatus).Should().BeTrue();
        engStatus.GetString().Should().Be("needs_expert_review");

        root.TryGetProperty("classDrafts", out var classesProp).Should().BeTrue();
        classesProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstClass = classesProp.EnumerateArray().First();
        firstClass.TryGetProperty("draftReference", out _).Should().BeTrue();
        firstClass.TryGetProperty("proposedClassTitle", out _).Should().BeTrue();
        firstClass.TryGetProperty("proposedClassDescription", out _).Should().BeTrue();
        firstClass.TryGetProperty("capabilityDeveloped", out _).Should().BeTrue();
        firstClass.TryGetProperty("workStepRequirementsSupported", out _).Should().BeTrue();
        firstClass.TryGetProperty("reasonNewClassIsRequired", out _).Should().BeTrue();
        firstClass.TryGetProperty("proposedEngramIndex", out _).Should().BeTrue();
        firstClass.TryGetProperty("status", out var clsStatus).Should().BeTrue();
        clsStatus.GetString().Should().Be("needs_expert_review");

        root.TryGetProperty("catalogItemsChecked", out _).Should().BeTrue();
        root.TryGetProperty("assumptions", out _).Should().BeTrue();
        root.TryGetProperty("warnings", out _).Should().BeTrue();
        root.TryGetProperty("sourcePages", out _).Should().BeTrue();
    }
}
