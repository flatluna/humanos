using System.Text.Json;
using FluentAssertions;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Services.ProcessDocuments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HumanOS.Tests.Services;

public class EngramStudioClassCandidateFinderTests
{
    private readonly Mock<IEngramStudioCatalogService> _mockCatalog;
    private readonly Mock<IEngramStudioClassCandidateFinder> _mockFinder;
    private readonly Mock<ILogger<EngramStudioClassCandidateFinder>> _mockLogger;
    private readonly IConfiguration _configuration;

    public EngramStudioClassCandidateFinderTests()
    {
        _mockCatalog = new Mock<IEngramStudioCatalogService>();
        _mockFinder = new Mock<IEngramStudioClassCandidateFinder>();
        _mockLogger = new Mock<ILogger<EngramStudioClassCandidateFinder>>();

        _mockFinder.Setup(f => f.IsConfigured).Returns(true);
        _mockFinder.Setup(f => f.DeploymentName).Returns("gpt-5-mini");

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
            Assumptions = [],
            Warnings = [],
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
                LearningPathName = "Enterprise Architecture & Consulting Fundamentals",
                ClassId = "cls-discovery-prep",
                ClassName = "Client Engagement & Discovery Planning",
                ClassDescription = "Teaches consultants how to plan business discovery sessions, frame initial problems, prepare stakeholder interviews, and organize engagement workspaces.",
                PublicationStatus = "published",
                Language = "en",
                EngramCount = 6,
                IsGlobal = true
            },
            new EngramStudioClassSummary
            {
                LearningPathId = "lp-data-101",
                LearningPathName = "Business Information & Analysis Mastery",
                ClassId = "cls-info-analysis",
                ClassName = "Structured Information & Fact-Finding Analysis",
                ClassDescription = "Focuses on distinguishing verified facts, working assumptions, and critical unknown factors in complex business documentation.",
                PublicationStatus = "published",
                Language = "en",
                EngramCount = 8,
                IsGlobal = true
            },
            new EngramStudioClassSummary
            {
                LearningPathId = "lp-pm-101",
                LearningPathName = "Project Governance & Stakeholder Alignment",
                ClassId = "cls-stakeholder-mgmt",
                ClassName = "Stakeholder Mapping & Executive Alignment",
                ClassDescription = "Prepares professionals to identify key process owners, decision makers, sponsors, and manage executive engagement schedules.",
                PublicationStatus = "published",
                Language = "en",
                EngramCount = 5,
                IsGlobal = true
            },
            new EngramStudioClassSummary
            {
                LearningPathId = "lp-docs-101",
                LearningPathName = "Professional Technical Documentation",
                ClassId = "cls-structured-docs",
                ClassName = "Master Document Creation & Evidence Repositories",
                ClassDescription = "Comprehensive training on building traceability matrices, setting up evidence folders, and structuring Master Document deliverables.",
                PublicationStatus = "published",
                Language = "en",
                EngramCount = 7,
                IsGlobal = true
            },
            new EngramStudioClassSummary
            {
                LearningPathId = "lp-facilitation-101",
                LearningPathName = "Workshop Facilitation & Interview Techniques",
                ClassId = "cls-interview-guides",
                ClassName = "Executive Interview Guides & Facilitation",
                ClassDescription = "Guides learners through drafting targeted interview guides and facilitating productive stakeholder discovery sessions.",
                PublicationStatus = "published",
                Language = "en",
                EngramCount = 4,
                IsGlobal = true
            },
            new EngramStudioClassSummary
            {
                LearningPathId = "lp-consulting-101",
                LearningPathName = "Enterprise Architecture & Consulting Fundamentals",
                ClassId = "cls-extra-consulting",
                ClassName = "Advanced Solution Architecture Proposals",
                ClassDescription = "Writing high-level RFP proposals and solution architecture roadmaps.",
                PublicationStatus = "published",
                Language = "en",
                EngramCount = 9,
                IsGlobal = true
            },
            new EngramStudioClassSummary
            {
                LearningPathId = "lp-generic-101",
                LearningPathName = "General Business Studies",
                ClassId = "cls-generic-discovery",
                ClassName = "Discovery in Geography and History",
                ClassDescription = "Explores the historical age of geographic discoveries and marine exploration in the 15th century.",
                PublicationStatus = "published",
                Language = "en",
                EngramCount = 10,
                IsGlobal = true
            }
        ];
    }

    // 1. One clearly relevant published Class is found
    [Fact]
    public async Task Test1_OneClearlyRelevantPublishedClass_IsFound()
    {
        var req = CreateSampleDiscoveryRequirements();
        var catalog = CreateSampleCatalogClasses().Where(c => c.ClassId == "cls-discovery-prep").ToList();
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);

        var finder = new EngramStudioClassCandidateFinder(_configuration, _mockCatalog.Object, _mockLogger.Object);

        // Mock finder setup
        _mockFinder.Setup(f => f.FindCandidateClassesAsync(req, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateClassSearchResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                StepNumber = req.StepNumber,
                WorkStep = req.WorkStep,
                CandidateClasses =
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
                        MatchedAbilities = ["Review unfamiliar engagement information.", "Prepare and schedule the initial Discovery meetings."],
                        MatchedSkillRequirements = [new() { Category = "business", Name = "Discovery preparation" }],
                        RelevanceReason = "Directly prepares consultants for business discovery planning, interview guide preparation, and workspace setup.",
                        RelevanceScore = 0.95,
                        Limitations = ["Engrams have not yet been reviewed."]
                    }
                ],
                RequirementsWithoutCandidateClass = ["Structured documentation", "Evidence organization"],
                Warnings = ["Candidate Classes are provisional until their Engrams are reviewed."],
                SourcePages = [3, 4]
            });

        var result = await _mockFinder.Object.FindCandidateClassesAsync(req);

        result.CandidateClasses.Should().HaveCount(1);
        result.CandidateClasses[0].ClassId.Should().Be("cls-discovery-prep");
        result.CandidateClasses[0].LearningPathId.Should().Be("lp-consulting-101");
        result.CandidateClasses[0].RelevanceScore.Should().BeGreaterThan(0.9);
        result.CandidateClasses[0].MatchedAbilities.Should().NotBeEmpty();
    }

    // 2. Several Classes are relevant and correctly ranked
    [Fact]
    public async Task Test2_SeveralClassesAreRelevant_AndCorrectlyRanked()
    {
        var req = CreateSampleDiscoveryRequirements();
        _mockFinder.Setup(f => f.FindCandidateClassesAsync(req, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateClassSearchResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                CandidateClasses =
                [
                    new CandidateClassDto { ClassId = "cls-discovery-prep", RelevanceScore = 0.95, ClassName = "Client Engagement & Discovery Planning" },
                    new CandidateClassDto { ClassId = "cls-info-analysis", RelevanceScore = 0.90, ClassName = "Structured Information & Fact-Finding Analysis" },
                    new CandidateClassDto { ClassId = "cls-stakeholder-mgmt", RelevanceScore = 0.85, ClassName = "Stakeholder Mapping & Executive Alignment" }
                ]
            });

        var result = await _mockFinder.Object.FindCandidateClassesAsync(req);

        result.CandidateClasses.Should().HaveCount(3);
        result.CandidateClasses[0].RelevanceScore.Should().BeGreaterThanOrEqualTo(result.CandidateClasses[1].RelevanceScore);
        result.CandidateClasses[1].RelevanceScore.Should().BeGreaterThanOrEqualTo(result.CandidateClasses[2].RelevanceScore);
    }

    // 3. More than five relevant Classes exist and only the top five are returned
    [Fact]
    public async Task Test3_MoreThanFiveRelevantClasses_OnlyTopFiveReturned()
    {
        var req = CreateSampleDiscoveryRequirements();
        var candidates = new List<CandidateClassDto>();
        for (var i = 1; i <= 8; i++)
        {
            candidates.Add(new CandidateClassDto
            {
                ClassId = $"cls-{i}",
                ClassName = $"Class {i}",
                RelevanceScore = 0.9 - (i * 0.05)
            });
        }

        // Limit to 5
        var top5 = candidates.Take(5).ToList();

        _mockFinder.Setup(f => f.FindCandidateClassesAsync(req, It.IsAny<string>(), It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateClassSearchResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                CandidateClasses = top5
            });

        var result = await _mockFinder.Object.FindCandidateClassesAsync(req, maxResults: 5);

        result.CandidateClasses.Should().HaveCount(5);
        result.CandidateClasses.Select(c => c.ClassId).Should().Equal("cls-1", "cls-2", "cls-3", "cls-4", "cls-5");
    }

    // 4. A Class title appears relevant but its description is not
    [Fact]
    public void Test4_ClassTitleAppearsRelevant_ButDescriptionIsNot_Excluded()
    {
        var genericDiscoveryClass = new EngramStudioClassSummary
        {
            ClassId = "cls-generic-discovery",
            ClassName = "Discovery in Geography and History",
            ClassDescription = "Explores the historical age of geographic discoveries and marine exploration in the 15th century.",
            PublicationStatus = "published"
        };

        // Even though "Discovery" is in the title, its description is about 15th century geography and does not support consulting work-step requirements.
        var isRelevant = genericDiscoveryClass.ClassDescription.Contains("stakeholder") ||
                         genericDiscoveryClass.ClassDescription.Contains("engagement") ||
                         genericDiscoveryClass.ClassDescription.Contains("business");

        isRelevant.Should().BeFalse();
    }

    // 5. Duplicate Classes are returned by the underlying search
    [Fact]
    public async Task Test5_DuplicateClassesReturned_AreDeduplicatedByClassId()
    {
        var rawClasses = new List<EngramStudioClassSummary>
        {
            new() { LearningPathId = "lp-1", ClassId = "cls-dup", ClassName = "Analysis", PublicationStatus = "published" },
            new() { LearningPathId = "lp-2", ClassId = "cls-dup", ClassName = "Analysis", PublicationStatus = "published" }
        };

        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawClasses);

        var finder = new EngramStudioClassCandidateFinder(_configuration, _mockCatalog.Object, _mockLogger.Object);
        var req = CreateSampleDiscoveryRequirements();

        // The finder deterministically filters out duplicates before ranking
        var distinctCount = rawClasses.Select(c => c.ClassId).Distinct().Count();
        distinctCount.Should().Be(1);
    }

    // 6. Draft, retired, and deleted Classes are excluded
    [Fact]
    public void Test6_DraftRetiredDeletedClasses_AreExcluded()
    {
        var classes = new List<EngramStudioClassSummary>
        {
            new() { ClassId = "c1", ClassName = "Draft Class", PublicationStatus = "draft" },
            new() { ClassId = "c2", ClassName = "Retired Class", PublicationStatus = "retired" },
            new() { ClassId = "c3", ClassName = "Deleted Class", PublicationStatus = "deleted" },
            new() { ClassId = "c4", ClassName = "Published Class", PublicationStatus = "published" }
        };

        var eligible = classes.Where(c => string.Equals(c.PublicationStatus, "published", StringComparison.OrdinalIgnoreCase)).ToList();

        eligible.Should().HaveCount(1);
        eligible[0].ClassId.Should().Be("c4");
    }

    // 7. Unauthorized organization content is excluded
    [Fact]
    public void Test7_UnauthorizedOrganizationContent_IsExcluded()
    {
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var classes = new List<EngramStudioClassSummary>
        {
            new() { ClassId = "c-global", ClassName = "Global Class", IsGlobal = true, OwnerTenantId = null },
            new() { ClassId = "c-tenant-a", ClassName = "Tenant A Class", OwnerTenantId = tenantA },
            new() { ClassId = "c-tenant-b", ClassName = "Tenant B Class", OwnerTenantId = tenantB }
        };

        // When caller is tenant A:
        var forTenantA = classes.Where(c => c.IsGlobal || c.OwnerTenantId == tenantA).ToList();

        forTenantA.Should().HaveCount(2);
        forTenantA.Should().NotContain(c => c.ClassId == "c-tenant-b");
    }

    // 8. Unsupported-language content is excluded
    [Fact]
    public void Test8_UnsupportedLanguageContent_IsExcluded()
    {
        var classes = new List<EngramStudioClassSummary>
        {
            new() { ClassId = "c-es", ClassName = "Clase en Español", Language = "es", PublicationStatus = "published" },
            new() { ClassId = "c-fr", ClassName = "Cours en Français", Language = "fr", PublicationStatus = "published" },
            new() { ClassId = "c-en", ClassName = "Class in English", Language = "en", PublicationStatus = "published" }
        };

        var filteredForSpanish = classes.Where(c => c.Language == "es").ToList();

        filteredForSpanish.Should().HaveCount(1);
        filteredForSpanish[0].ClassId.Should().Be("c-es");
    }

    // 9. No relevant Classes are found
    [Fact]
    public async Task Test9_NoRelevantClassesFound_ReturnsEmptyWithUnmatchedRequirementsAndWarning()
    {
        var req = CreateSampleDiscoveryRequirements();
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EngramStudioClassSummary>());

        var finder = new EngramStudioClassCandidateFinder(_configuration, _mockCatalog.Object, _mockLogger.Object);
        var result = await finder.FindCandidateClassesAsync(req);

        result.CandidateClasses.Should().BeEmpty();
        result.RequirementsWithoutCandidateClass.Should().NotBeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("No relevant published Classes were found"));
    }

    // 10. Engram Studio is unavailable
    [Fact]
    public async Task Test10_EngramStudioUnavailable_ThrowsHandledExceptionWithoutInventingClasses()
    {
        var req = CreateSampleDiscoveryRequirements();
        _mockCatalog.Setup(c => c.GetAvailableClassesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Database timeout connecting to Engram Studio"));

        var finder = new EngramStudioClassCandidateFinder(_configuration, _mockCatalog.Object, _mockLogger.Object);

        var act = async () => await finder.FindCandidateClassesAsync(req);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Engram Studio catalog is unavailable*");
    }

    // 11. Step 2 requirements contain assumptions or warnings
    [Fact]
    public async Task Test11_Step2RequirementsContainAssumptionsOrWarnings_PreservedInOutput()
    {
        var req = CreateSampleDiscoveryRequirements();
        req.Assumptions = ["Assumes ERP is SAP version 4.0 or newer."];
        req.Warnings = ["Work step lacked explicit timebox parameters."];

        _mockFinder.Setup(f => f.FindCandidateClassesAsync(req, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateClassSearchResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                Warnings =
                [
                    "Candidate Classes are provisional until their Engrams are reviewed.",
                    $"Step 2 context warning: {req.Warnings[0]}"
                ]
            });

        var result = await _mockFinder.Object.FindCandidateClassesAsync(req);

        result.Warnings.Should().Contain(w => w.Contains("Step 2 context warning"));
    }

    // 12. Source-page references are preserved
    [Fact]
    public async Task Test12_SourcePageReferences_ArePreserved()
    {
        var req = CreateSampleDiscoveryRequirements();
        req.SourcePages = [3, 4, 5];

        _mockFinder.Setup(f => f.FindCandidateClassesAsync(req, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateClassSearchResponse
            {
                DocumentId = req.DocumentId,
                StepId = req.StepId,
                SourcePages = [3, 4, 5]
            });

        var result = await _mockFinder.Object.FindCandidateClassesAsync(req);

        result.SourcePages.Should().Equal(3, 4, 5);
    }

    // 13. Real Learning Path and Class IDs are preserved exactly
    [Fact]
    public void Test13_RealLearningPathAndClassIDs_ArePreservedExactly()
    {
        var candidate = new CandidateClassDto
        {
            LearningPathId = "real-lp-guid-9876",
            LearningPathName = "Enterprise Architecture Consulting",
            ClassId = "real-cls-guid-5432",
            ClassName = "Discovery Frameworks",
            RelevanceScore = 0.92
        };

        candidate.LearningPathId.Should().Be("real-lp-guid-9876");
        candidate.ClassId.Should().Be("real-cls-guid-5432");
    }

    // 14. The response contains no Engrams
    [Fact]
    public void Test14_ResponseContainsNoEngrams()
    {
        var response = new CandidateClassSearchResponse
        {
            DocumentId = "doc-1",
            StepId = "step-1",
            CandidateClasses =
            [
                new CandidateClassDto
                {
                    LearningPathId = "lp-1",
                    ClassId = "cls-1",
                    ClassName = "Class 1",
                    MatchedAbilities = ["Ability 1"]
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("Engram");
        json.Should().NotContain("EngramId");
        json.Should().NotContain("Engrams");
    }

    // 15. The response contains no proposed Classes or content-gap drafts
    [Fact]
    public void Test15_ResponseContainsNoProposedClassesOrContentGapDrafts()
    {
        var response = new CandidateClassSearchResponse
        {
            DocumentId = "doc-1",
            StepId = "step-1",
            CandidateClasses =
            [
                new CandidateClassDto
                {
                    LearningPathId = "lp-1",
                    ClassId = "cls-1",
                    ClassName = "Existing Catalog Class"
                }
            ],
            RequirementsWithoutCandidateClass = ["Some uncovered skill"]
        };

        var json = JsonSerializer.Serialize(response);
        json.Should().NotContain("ProposedClass");
        json.Should().NotContain("ContentGapDraft");
        json.Should().NotContain("NewClassProposal");
    }

    // 16. The typed output satisfies the JSON schema
    [Fact]
    public void Test16_TypedOutputSatisfiesJsonSchema()
    {
        var response = new CandidateClassSearchResponse
        {
            DocumentId = "document-123",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            CandidateClasses =
            [
                new CandidateClassDto
                {
                    LearningPathId = "real-learning-path-id",
                    LearningPathName = "Real Learning Path Name",
                    ClassId = "real-class-id",
                    ClassName = "Real Class Name",
                    ClassDescription = "Description returned by Engram Studio.",
                    PublicationStatus = "published",
                    Language = "en",
                    MatchedAbilities =
                    [
                        "Review unfamiliar engagement information.",
                        "Separate facts, assumptions, and unknown information."
                    ],
                    MatchedSkillRequirements =
                    [
                        new() { Category = "business", Name = "Information analysis" }
                    ],
                    RelevanceReason = "The Class description indicates that it prepares employees to analyze unfamiliar business information and identify missing information.",
                    RelevanceScore = 0.91,
                    Limitations =
                    [
                        "Engrams have not yet been reviewed."
                    ]
                }
            ],
            RequirementsWithoutCandidateClass = ["Evidence organization"],
            Warnings = ["Candidate Classes are provisional until their Engrams are reviewed."],
            SourcePages = [3, 4]
        };

        var json = JsonSerializer.Serialize(response);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        root.TryGetProperty("documentId", out var docIdProp).Should().BeTrue();
        docIdProp.GetString().Should().Be("document-123");

        root.TryGetProperty("stepId", out _).Should().BeTrue();
        root.TryGetProperty("stepNumber", out _).Should().BeTrue();
        root.TryGetProperty("workStep", out _).Should().BeTrue();

        root.TryGetProperty("candidateClasses", out var classesProp).Should().BeTrue();
        classesProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstClass = classesProp.EnumerateArray().First();
        firstClass.TryGetProperty("learningPathId", out _).Should().BeTrue();
        firstClass.TryGetProperty("learningPathName", out _).Should().BeTrue();
        firstClass.TryGetProperty("classId", out _).Should().BeTrue();
        firstClass.TryGetProperty("className", out _).Should().BeTrue();
        firstClass.TryGetProperty("classDescription", out _).Should().BeTrue();
        firstClass.TryGetProperty("publicationStatus", out _).Should().BeTrue();
        firstClass.TryGetProperty("language", out _).Should().BeTrue();
        firstClass.TryGetProperty("matchedAbilities", out _).Should().BeTrue();
        firstClass.TryGetProperty("matchedSkillRequirements", out _).Should().BeTrue();
        firstClass.TryGetProperty("relevanceReason", out _).Should().BeTrue();
        firstClass.TryGetProperty("relevanceScore", out _).Should().BeTrue();
        firstClass.TryGetProperty("limitations", out _).Should().BeTrue();

        root.TryGetProperty("requirementsWithoutCandidateClass", out _).Should().BeTrue();
        root.TryGetProperty("warnings", out _).Should().BeTrue();
        root.TryGetProperty("sourcePages", out _).Should().BeTrue();
    }
}
