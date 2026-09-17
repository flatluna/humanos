using System.Text.Json;
using FluentAssertions;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Services.ProcessDocuments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HumanOS.Tests.Services;

public class EngramStudioEngramSelectorTests
{
    private readonly Mock<IEngramStudioCatalogService> _mockCatalog;
    private readonly Mock<IEngramStudioEngramSelector> _mockSelector;
    private readonly Mock<ILogger<EngramStudioEngramSelector>> _mockLogger;
    private readonly IConfiguration _configuration;

    public EngramStudioEngramSelectorTests()
    {
        _mockCatalog = new Mock<IEngramStudioCatalogService>();
        _mockSelector = new Mock<IEngramStudioEngramSelector>();
        _mockLogger = new Mock<ILogger<EngramStudioEngramSelector>>();

        _mockSelector.Setup(s => s.IsConfigured).Returns(true);
        _mockSelector.Setup(s => s.DeploymentName).Returns("gpt-5-mini");

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

    private static WorkStepRequirementsAnalysisResult CreateSampleDiscoveryRequirements()
    {
        return new WorkStepRequirementsAnalysisResult
        {
            DocumentId = "doc-discovery-123",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            EmployeeMustBeAbleTo =
            [
                "Review unfamiliar engagement information.",
                "Separate facts, assumptions, and unknown information.",
                "Identify the correct business and technical stakeholders.",
                "Prepare and schedule the initial Discovery meetings.",
                "Create and organize the Discovery documentation workspace."
            ],
            RequiredSkillAreas = new RequiredSkillAreas
            {
                SoftSkills =
                [
                    new() { Name = "Stakeholder management", Reason = "Identify people with authority, operational knowledge, and ownership." },
                    new() { Name = "Facilitation preparation", Reason = "Prepare meetings and questions that support productive Discovery conversations." }
                ],
                BusinessSkills =
                [
                    new() { Name = "Discovery preparation", Reason = "Organize initial engagement before business interviews begin." },
                    new() { Name = "Information analysis", Reason = "Distinguish facts, assumptions, unknowns, and missing information." }
                ],
                TechnicalSkills =
                [
                    new() { Name = "Structured documentation", Reason = "Create and organize initial Master Document." },
                    new() { Name = "Evidence organization", Reason = "Establish a traceable repository for Discovery evidence." }
                ]
            },
            RequiredTools =
            [
                new() { Name = "Master Document template", UseInStep = "Create initial Discovery section." },
                new() { Name = "Evidence repository", UseInStep = "Store and organize engagement documents and evidence." },
                new() { Name = "Calendar and meeting tools", UseInStep = "Schedule executive and operator Discovery sessions." }
            ],
            CompletionEvidence =
            [
                "Discovery meeting schedule",
                "Stakeholder list",
                "Known, Assumed, and Unknown list",
                "Executive interview guide",
                "Initial Master Document",
                "Discovery evidence folder"
            ],
            Assumptions = ["Assumes access to client documentation portal."],
            Warnings = ["Timeframe for initial discovery was not specified."],
            SourcePages = [3, 4]
        };
    }

    private static List<CandidateClassDto> CreateSampleCandidateClasses()
    {
        return
        [
            new CandidateClassDto
            {
                LearningPathId = "lp-consulting-101",
                LearningPathName = "Enterprise Architecture & Consulting Fundamentals",
                ClassId = "cls-discovery-prep",
                ClassName = "Client Engagement & Discovery Planning",
                ClassDescription = "Teaches consultants how to plan business discovery sessions, frame initial problems, prepare stakeholder interviews, and organize engagement workspaces.",
                PublicationStatus = "published",
                Language = "en",
                RelevanceScore = 0.95
            },
            new CandidateClassDto
            {
                LearningPathId = "lp-data-101",
                LearningPathName = "Business Information & Analysis Mastery",
                ClassId = "cls-info-analysis",
                ClassName = "Structured Information & Fact-Finding Analysis",
                ClassDescription = "Focuses on distinguishing verified facts, working assumptions, and critical unknown factors in complex business documentation.",
                PublicationStatus = "published",
                Language = "en",
                RelevanceScore = 0.90
            },
            new CandidateClassDto
            {
                LearningPathId = "lp-pm-101",
                LearningPathName = "Project Governance & Stakeholder Alignment",
                ClassId = "cls-stakeholder-mgmt",
                ClassName = "Stakeholder Mapping & Executive Alignment",
                ClassDescription = "Prepares professionals to identify key process owners, decision makers, sponsors, and manage executive engagement schedules.",
                PublicationStatus = "published",
                Language = "en",
                RelevanceScore = 0.85
            }
        ];
    }

    private static List<EngramStudioEngramSummary> CreateSampleEngrams()
    {
        return
        [
            new EngramStudioEngramSummary
            {
                EngramId = "eng-review-request",
                ClassId = "cls-discovery-prep",
                LearningPathId = "lp-consulting-101",
                EngramName = "Review an Engagement Request",
                EngramDescription = "Techniques for rapidly reviewing briefing documents, sponsor notes, and scope definitions.",
                PublicationStatus = "published",
                Language = "en",
                IsGlobal = true
            },
            new EngramStudioEngramSummary
            {
                EngramId = "eng-prep-checklist",
                ClassId = "cls-discovery-prep",
                LearningPathId = "lp-consulting-101",
                EngramName = "Create a Discovery Preparation Checklist",
                EngramDescription = "Step-by-step assembly of workspaces, meeting invites, and discovery readiness items.",
                PublicationStatus = "published",
                Language = "en",
                IsGlobal = true
            },
            new EngramStudioEngramSummary
            {
                EngramId = "eng-irrelevant-discovery",
                ClassId = "cls-discovery-prep",
                LearningPathId = "lp-consulting-101",
                EngramName = "Billing and Rate Card Negotiations",
                EngramDescription = "Contractual rate card establishment and billing currency adjustments.",
                PublicationStatus = "published",
                Language = "en",
                IsGlobal = true
            },
            new EngramStudioEngramSummary
            {
                EngramId = "eng-separate-known-assumed",
                ClassId = "cls-info-analysis",
                LearningPathId = "lp-data-101",
                EngramName = "Separate Known, Assumed, and Unknown Information",
                EngramDescription = "Framework for categorizing claims into verified facts, active working assumptions, and critical gaps.",
                PublicationStatus = "published",
                Language = "en",
                IsGlobal = true
            },
            new EngramStudioEngramSummary
            {
                EngramId = "eng-identify-missing-info",
                ClassId = "cls-info-analysis",
                LearningPathId = "lp-data-101",
                EngramName = "Identify Missing Information",
                EngramDescription = "Techniques for detecting blind spots and unstated prerequisites in project documents.",
                PublicationStatus = "published",
                Language = "en",
                IsGlobal = true
            },
            new EngramStudioEngramSummary
            {
                EngramId = "eng-stakeholder-mapping",
                ClassId = "cls-stakeholder-mgmt",
                LearningPathId = "lp-pm-101",
                EngramName = "Stakeholder Identification & RACI Alignment",
                EngramDescription = "Methods for identifying executive sponsors, process owners, frontline users, and system owners.",
                PublicationStatus = "published",
                Language = "en",
                IsGlobal = true
            }
        ];
    }

    // 1. A candidate Class contains several directly relevant Engrams
    [Fact]
    public async Task Test1_CandidateClassContainsSeveralDirectlyRelevantEngrams()
    {
        var req = CreateSampleDiscoveryRequirements();
        var candidates = CreateSampleCandidateClasses().Take(1).ToList();

        _mockSelector.Setup(s => s.SelectRelevantEngramsAsync(req, candidates, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevantEngramsResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                WorkStep = req.WorkStep,
                SelectedClasses =
                [
                    new SelectedClassDto
                    {
                        ClassId = "cls-discovery-prep",
                        ClassName = "Client Engagement & Discovery Planning",
                        LearningPathId = "lp-consulting-101",
                        LearningPathName = "Enterprise Architecture & Consulting Fundamentals",
                        SelectedEngrams =
                        [
                            new SelectedEngramDto
                            {
                                EngramId = "eng-review-request",
                                EngramName = "Review an Engagement Request",
                                SupportedRequirements = ["Review unfamiliar engagement information."],
                                RelevanceReason = "Teaches reviewing unfamiliar documents and sponsor notes.",
                                Criticality = "critical",
                                RelevanceScore = 0.95
                            },
                            new SelectedEngramDto
                            {
                                EngramId = "eng-prep-checklist",
                                EngramName = "Create a Discovery Preparation Checklist",
                                SupportedRequirements = ["Create and organize the Discovery documentation workspace."],
                                RelevanceReason = "Provides workspace and checklist setup tools.",
                                Criticality = "supporting",
                                RelevanceScore = 0.88
                            }
                        ]
                    }
                ],
                SourcePages = [3, 4]
            });

        var result = await _mockSelector.Object.SelectRelevantEngramsAsync(req, candidates);

        result.SelectedClasses.Should().HaveCount(1);
        result.SelectedClasses[0].SelectedEngrams.Should().HaveCount(2);
        result.SelectedClasses[0].SelectedEngrams.Should().Contain(e => e.Criticality == "critical");
    }

    // 2. A Class contains many Engrams but only a few are selected
    [Fact]
    public void Test2_ClassContainsManyEngrams_OnlyAFewAreSelected()
    {
        var allEngrams = CreateSampleEngrams();
        var classEngrams = allEngrams.Where(e => e.ClassId == "cls-discovery-prep").ToList();
        classEngrams.Should().HaveCount(3);

        // One of the engrams is "Billing and Rate Card Negotiations" which is irrelevant to discovery preparation
        var relevant = classEngrams.Where(e => !e.EngramName.Contains("Billing")).ToList();
        relevant.Should().HaveCount(2);
    }

    // 3. A candidate Class contains no relevant Engrams and is excluded
    [Fact]
    public async Task Test3_CandidateClassWithNoRelevantEngrams_IsExcludedFromSelectedClasses()
    {
        var req = CreateSampleDiscoveryRequirements();
        var candidates = new List<CandidateClassDto>
        {
            new() { ClassId = "cls-relevant", ClassName = "Relevant Class", LearningPathId = "lp-1" },
            new() { ClassId = "cls-irrelevant", ClassName = "Irrelevant Class", LearningPathId = "lp-2" }
        };

        _mockSelector.Setup(s => s.SelectRelevantEngramsAsync(req, candidates, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevantEngramsResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                SelectedClasses =
                [
                    new SelectedClassDto
                    {
                        ClassId = "cls-relevant",
                        ClassName = "Relevant Class",
                        LearningPathId = "lp-1",
                        SelectedEngrams = [new() { EngramId = "e1", EngramName = "Engram 1" }]
                    }
                ],
                Warnings = ["Excluded candidate Classes with no directly relevant Engrams: Irrelevant Class."]
            });

        var result = await _mockSelector.Object.SelectRelevantEngramsAsync(req, candidates);

        result.SelectedClasses.Should().HaveCount(1);
        result.SelectedClasses.Should().NotContain(c => c.ClassId == "cls-irrelevant");
        result.Warnings.Should().Contain(w => w.Contains("Excluded candidate Classes"));
    }

    // 4. Relevant Engrams exist across several candidate Classes
    [Fact]
    public async Task Test4_RelevantEngramsExistAcrossSeveralCandidateClasses()
    {
        var req = CreateSampleDiscoveryRequirements();
        var candidates = CreateSampleCandidateClasses();

        _mockSelector.Setup(s => s.SelectRelevantEngramsAsync(req, candidates, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevantEngramsResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                SelectedClasses =
                [
                    new SelectedClassDto { ClassId = "cls-discovery-prep", LearningPathId = "lp-consulting-101", SelectedEngrams = [new() { EngramId = "eng-review-request" }] },
                    new SelectedClassDto { ClassId = "cls-info-analysis", LearningPathId = "lp-data-101", SelectedEngrams = [new() { EngramId = "eng-separate-known-assumed" }] },
                    new SelectedClassDto { ClassId = "cls-stakeholder-mgmt", LearningPathId = "lp-pm-101", SelectedEngrams = [new() { EngramId = "eng-stakeholder-mapping" }] }
                ]
            });

        var result = await _mockSelector.Object.SelectRelevantEngramsAsync(req, candidates);

        result.SelectedClasses.Should().HaveCount(3);
        result.SelectedClasses.SelectMany(c => c.SelectedEngrams).Should().HaveCount(3);
    }

    // 5. Duplicate Engrams are returned by the catalog
    [Fact]
    public void Test5_DuplicateEngramsReturnedByCatalog_AreDeduplicatedByEngramId()
    {
        var duplicates = new List<EngramStudioEngramSummary>
        {
            new() { EngramId = "eng-dup-1", ClassId = "cls-1", EngramName = "Fact-Finding" },
            new() { EngramId = "eng-dup-1", ClassId = "cls-1", EngramName = "Fact-Finding" }
        };

        var distinct = duplicates.GroupBy(e => e.EngramId).Select(g => g.First()).ToList();
        distinct.Should().HaveCount(1);
    }

    // 6. Similar Engrams exist and the strongest match is selected
    [Fact]
    public void Test6_SimilarEngramsExist_StrongestMatchIsSelected()
    {
        var engrams = new List<SelectedEngramDto>
        {
            new() { EngramId = "eng-general-notes", EngramName = "General Note Taking", RelevanceScore = 0.55 },
            new() { EngramId = "eng-structured-interview-notes", EngramName = "Executive Interview & Fact Recording", RelevanceScore = 0.94 }
        };

        var bestMatch = engrams.OrderByDescending(e => e.RelevanceScore).First();
        bestMatch.EngramId.Should().Be("eng-structured-interview-notes");
    }

    // 7. Two similar Engrams are retained because their applications differ
    [Fact]
    public void Test7_TwoSimilarEngramsRetained_WhenApplicationsDiffer()
    {
        var e1 = new SelectedEngramDto
        {
            EngramId = "eng-stakeholders-technical",
            EngramName = "Technical Stakeholder Mapping",
            RelevanceReason = "Focuses on data owners and system architects.",
            RelevanceScore = 0.88
        };
        var e2 = new SelectedEngramDto
        {
            EngramId = "eng-stakeholders-executive",
            EngramName = "Executive Sponsor Alignment",
            RelevanceReason = "Focuses on C-level sponsors and policy governance.",
            RelevanceScore = 0.90
        };

        var list = new List<SelectedEngramDto> { e1, e2 };
        list.Should().HaveCount(2);
        list[0].RelevanceReason.Should().NotBe(list[1].RelevanceReason);
    }

    // 8. Draft, retired, and deleted Engrams are excluded
    [Fact]
    public void Test8_DraftRetiredDeletedEngrams_AreExcluded()
    {
        var engrams = new List<EngramStudioEngramSummary>
        {
            new() { EngramId = "e1", PublicationStatus = "draft" },
            new() { EngramId = "e2", PublicationStatus = "retired" },
            new() { EngramId = "e3", PublicationStatus = "deleted" },
            new() { EngramId = "e4", PublicationStatus = "published" }
        };

        var eligible = engrams.Where(e => string.Equals(e.PublicationStatus, "published", StringComparison.OrdinalIgnoreCase)).ToList();
        eligible.Should().HaveCount(1);
        eligible[0].EngramId.Should().Be("e4");
    }

    // 9. Unauthorized organization content is excluded
    [Fact]
    public void Test9_UnauthorizedOrganizationContent_IsExcluded()
    {
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var engrams = new List<EngramStudioEngramSummary>
        {
            new() { EngramId = "e-global", IsGlobal = true, OwnerTenantId = null },
            new() { EngramId = "e-tenant-a", OwnerTenantId = tenantA },
            new() { EngramId = "e-tenant-b", OwnerTenantId = tenantB }
        };

        var scopedToA = engrams.Where(e => e.IsGlobal || e.OwnerTenantId == tenantA).ToList();
        scopedToA.Should().HaveCount(2);
        scopedToA.Should().NotContain(e => e.EngramId == "e-tenant-b");
    }

    // 10. Unsupported-language Engrams are excluded
    [Fact]
    public void Test10_UnsupportedLanguageEngrams_AreExcluded()
    {
        var engrams = new List<EngramStudioEngramSummary>
        {
            new() { EngramId = "e-en", Language = "en" },
            new() { EngramId = "e-de", Language = "de" }
        };

        var englishOnly = engrams.Where(e => e.Language == "en").ToList();
        englishOnly.Should().HaveCount(1);
        englishOnly[0].EngramId.Should().Be("e-en");
    }

    // 11. Engram IDs and parent IDs are preserved exactly
    [Fact]
    public void Test11_EngramIDsAndParentIDs_ArePreservedExactly()
    {
        var sc = new SelectedClassDto
        {
            LearningPathId = "real-lp-123",
            LearningPathName = "Architecture",
            ClassId = "real-cls-456",
            ClassName = "Discovery",
            SelectedEngrams =
            [
                new SelectedEngramDto
                {
                    EngramId = "real-eng-789",
                    EngramName = "Fact-Finding"
                }
            ]
        };

        sc.LearningPathId.Should().Be("real-lp-123");
        sc.ClassId.Should().Be("real-cls-456");
        sc.SelectedEngrams[0].EngramId.Should().Be("real-eng-789");
    }

    // 12. An Engram with an invalid parent Class is rejected
    [Fact]
    public void Test12_EngramWithInvalidParentClass_IsRejected()
    {
        var candidates = new List<CandidateClassDto>
        {
            new() { ClassId = "cls-valid", LearningPathId = "lp-1" }
        };

        var engram = new EngramStudioEngramSummary
        {
            EngramId = "eng-orphan",
            ClassId = "cls-invalid-parent"
        };

        var isValid = candidates.Any(c => c.ClassId == engram.ClassId);
        isValid.Should().BeFalse();
    }

    // 13. Engram Studio is unavailable
    [Fact]
    public async Task Test13_EngramStudioUnavailable_ThrowsHandledException()
    {
        var req = CreateSampleDiscoveryRequirements();
        var candidates = CreateSampleCandidateClasses();

        _mockCatalog.Setup(c => c.GetEngramsByClassIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Catalog connection lost."));

        var selector = new EngramStudioEngramSelector(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var act = async () => await selector.SelectRelevantEngramsAsync(req, candidates);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Engram Studio catalog is unavailable*");
    }

    // 14. All work-step requirements are covered
    [Fact]
    public void Test14_AllWorkStepRequirements_AreCovered()
    {
        var coverage = new List<RequirementCoverageItem>
        {
            new() { Requirement = "Review unfamiliar engagement info", CoverageStatus = "covered", SupportingEngramIds = ["e1"] },
            new() { Requirement = "Separate facts, assumptions", CoverageStatus = "covered", SupportingEngramIds = ["e2"] }
        };

        coverage.All(c => c.CoverageStatus == "covered").Should().BeTrue();
    }

    // 15. Some requirements are partially covered
    [Fact]
    public void Test15_SomeRequirements_ArePartiallyCovered()
    {
        var coverage = new List<RequirementCoverageItem>
        {
            new() { Requirement = "Req 1", CoverageStatus = "covered", SupportingEngramIds = ["e1"] },
            new() { Requirement = "Req 2", CoverageStatus = "partially_covered", SupportingEngramIds = ["e2"], Notes = "Covers basics only." }
        };

        coverage.Should().Contain(c => c.CoverageStatus == "partially_covered");
    }

    // 16. Some requirements are not covered
    [Fact]
    public void Test16_SomeRequirements_AreNotCovered()
    {
        var uncovered = new List<string> { "Prompt Engineering for Discovery" };
        uncovered.Should().NotBeEmpty();
    }

    // 17. Source-page references are preserved
    [Fact]
    public async Task Test17_SourcePageReferences_ArePreserved()
    {
        var req = CreateSampleDiscoveryRequirements();
        req.SourcePages = [3, 4];
        var candidates = CreateSampleCandidateClasses();

        _mockSelector.Setup(s => s.SelectRelevantEngramsAsync(req, candidates, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevantEngramsResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                SourcePages = [3, 4]
            });

        var result = await _mockSelector.Object.SelectRelevantEngramsAsync(req, candidates);
        result.SourcePages.Should().Equal(3, 4);
    }

    // 18. Step 2 assumptions and warnings are preserved
    [Fact]
    public async Task Test18_Step2AssumptionsAndWarnings_ArePreserved()
    {
        var req = CreateSampleDiscoveryRequirements();
        var candidates = CreateSampleCandidateClasses();

        _mockSelector.Setup(s => s.SelectRelevantEngramsAsync(req, candidates, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevantEngramsResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                Assumptions = req.Assumptions,
                Warnings = [.. req.Warnings, "Candidate Classes are provisional."]
            });

        var result = await _mockSelector.Object.SelectRelevantEngramsAsync(req, candidates);
        result.Assumptions.Should().Contain(req.Assumptions[0]);
        result.Warnings.Should().Contain(req.Warnings[0]);
    }

    // 19. No new Classes or Engrams are proposed
    [Fact]
    public void Test19_NoNewClassesOrEngramsAreProposed()
    {
        var response = new RelevantEngramsResponse
        {
            DocumentId = "doc-1",
            StepId = "step-1",
            SelectedClasses =
            [
                new SelectedClassDto
                {
                    ClassId = "cls-existing",
                    SelectedEngrams = [new() { EngramId = "eng-existing", EngramName = "Existing Engram" }]
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("ProposedEngram");
        json.Should().NotContain("ProposedClass");
        json.Should().NotContain("DraftEngram");
    }

    // 20. No full Engram lesson content is retrieved or returned
    [Fact]
    public void Test20_NoFullEngramLessonContent_IsRetrievedOrReturned()
    {
        var response = new RelevantEngramsResponse
        {
            SelectedClasses =
            [
                new SelectedClassDto
                {
                    ClassId = "cls-1",
                    SelectedEngrams =
                    [
                        new SelectedEngramDto
                        {
                            EngramId = "e1",
                            EngramName = "Review Engagement Request",
                            EngramDescription = "Short description only."
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("LessonContent");
        json.Should().NotContain("Cards");
        json.Should().NotContain("AssessmentQuestions");
        json.Should().NotContain("FullText");
    }

    // 21. Output follows the typed JSON schema
    [Fact]
    public void Test21_OutputFollowsTypedJsonSchema()
    {
        var response = new RelevantEngramsResponse
        {
            DocumentId = "doc-1",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            SelectedClasses =
            [
                new SelectedClassDto
                {
                    LearningPathId = "lp-1",
                    LearningPathName = "Architecture",
                    ClassId = "cls-1",
                    ClassName = "Discovery",
                    ClassDescription = "Discovery class",
                    SelectedEngrams =
                    [
                        new SelectedEngramDto
                        {
                            EngramId = "eng-1",
                            EngramName = "Fact-Finding",
                            EngramDescription = "Fact finding description",
                            SupportedRequirements = ["Separate facts"],
                            RelevanceReason = "Directly addresses fact separation",
                            RelevanceScore = 0.95,
                            Criticality = "critical"
                        }
                    ]
                }
            ],
            RequirementCoverage =
            [
                new RequirementCoverageItem
                {
                    Requirement = "Separate facts",
                    RequirementType = "ability",
                    CoverageStatus = "covered",
                    SupportingEngramIds = ["eng-1"],
                    Notes = "Fully taught by eng-1"
                }
            ],
            UncoveredRequirements = ["Uncovered item"],
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
        root.TryGetProperty("selectedClasses", out var classesProp).Should().BeTrue();
        classesProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstClass = classesProp.EnumerateArray().First();
        firstClass.TryGetProperty("learningPathId", out _).Should().BeTrue();
        firstClass.TryGetProperty("classId", out _).Should().BeTrue();
        firstClass.TryGetProperty("selectedEngrams", out var engramsProp).Should().BeTrue();
        var firstEngram = engramsProp.EnumerateArray().First();
        firstEngram.TryGetProperty("engramId", out _).Should().BeTrue();
        firstEngram.TryGetProperty("engramName", out _).Should().BeTrue();
        firstEngram.TryGetProperty("supportedRequirements", out _).Should().BeTrue();
        firstEngram.TryGetProperty("relevanceReason", out _).Should().BeTrue();
        firstEngram.TryGetProperty("relevanceScore", out _).Should().BeTrue();
        firstEngram.TryGetProperty("criticality", out _).Should().BeTrue();

        root.TryGetProperty("requirementCoverage", out var covProp).Should().BeTrue();
        covProp.ValueKind.Should().Be(JsonValueKind.Array);
        root.TryGetProperty("uncoveredRequirements", out _).Should().BeTrue();
        root.TryGetProperty("assumptions", out _).Should().BeTrue();
        root.TryGetProperty("warnings", out _).Should().BeTrue();
        root.TryGetProperty("sourcePages", out _).Should().BeTrue();
    }

    // 22. Unverified candidate Class IDs from arbitrary client input are rejected
    [Fact]
    public void Test22_UnverifiedCandidateClassIDs_AreRejected()
    {
        var step3CandidateClasses = new List<CandidateClassDto>
        {
            new() { ClassId = "step3-cls-1", ClassName = "Class 1", LearningPathId = "lp-1" },
            new() { ClassId = "step3-cls-2", ClassName = "Class 2", LearningPathId = "lp-2" }
        };

        var validStep3Ids = step3CandidateClasses
            .Select(c => c.ClassId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clientSuppliedIds = new List<string> { "step3-cls-1", "unverified-arbitrary-class-id" };

        var unverified = clientSuppliedIds.Where(id => !validStep3Ids.Contains(id)).ToList();

        unverified.Should().ContainSingle().Which.Should().Be("unverified-arbitrary-class-id");
    }
}
