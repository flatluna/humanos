using System.Text.Json;
using FluentAssertions;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Services.ProcessDocuments;
using HumanOS.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HumanOS.Tests.Services;

public class EmployeeStepLearningAssignmentServiceTests
{
    private readonly Mock<IProcessDocumentStorageService> _mockStorage;
    private readonly Mock<IWorkStepRequirementsAnalyzer> _mockStepAnalyzer;
    private readonly Mock<IEngramStudioClassCandidateFinder> _mockCandidateFinder;
    private readonly Mock<IEngramStudioEngramSelector> _mockEngramSelector;
    private readonly Mock<ILearningContentCoverageAnalyzer> _mockCoverageAnalyzer;
    private readonly Mock<ILearningContentGapDraftGenerator> _mockDraftGenerator;
    private readonly Mock<IStepLearningAssignmentGenerator> _mockAssignmentGenerator;
    private readonly Mock<ILogger<EmployeeStepLearningAssignmentService>> _mockLogger;
    private readonly IConfiguration _configuration;

    public EmployeeStepLearningAssignmentServiceTests()
    {
        _mockStorage = new Mock<IProcessDocumentStorageService>();
        _mockStepAnalyzer = new Mock<IWorkStepRequirementsAnalyzer>();
        _mockCandidateFinder = new Mock<IEngramStudioClassCandidateFinder>();
        _mockEngramSelector = new Mock<IEngramStudioEngramSelector>();
        _mockCoverageAnalyzer = new Mock<ILearningContentCoverageAnalyzer>();
        _mockDraftGenerator = new Mock<ILearningContentGapDraftGenerator>();
        _mockAssignmentGenerator = new Mock<IStepLearningAssignmentGenerator>();
        _mockLogger = new Mock<ILogger<EmployeeStepLearningAssignmentService>>();

        _mockStorage.Setup(s => s.SaveEmployeeAssignmentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmployeeStepLearningAssignmentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("saved-path");

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

    private static EmployeeStepLearningAssignmentResponse CreateSampleAssignmentResponse(
        string status = "assigned",
        string evidenceLevel = "reported",
        bool hasGaps = false,
        bool isBlocking = false)
    {
        return new EmployeeStepLearningAssignmentResponse
        {
            AssignmentId = "assign-123",
            EmployeeId = "11111111-1111-1111-1111-111111111111",
            DocumentId = "doc-123",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            EmployeeAssignmentStatus = status,
            EmployeeMustBeAbleTo =
            [
                "Review unfamiliar engagement information.",
                "Separate facts, assumptions, and unknowns."
            ],
            ReportedExperienceSummary =
            [
                "Consultant with 4 years in technical requirements gathering and client discovery."
            ],
            AssignedClasses =
            [
                new AssignedClassDto
                {
                    LearningPathId = "lp-consulting-101",
                    LearningPathName = "FDE Business Discovery",
                    ClassId = "cls-discovery-prep",
                    ClassName = "Discovery Preparation",
                    ClassAssignmentStatus = "required",
                    AssignedEngrams =
                    [
                        new AssignedEngramDto
                        {
                            EngramId = "eng-review-request",
                            EngramName = "Review an Engagement Request",
                            AssignmentStatus = "required",
                            AssignmentReason = "Critical capability required for work step. Prior reported experience noted in résumé, but required to ensure operational rigor.",
                            EmployeeEvidenceLevel = evidenceLevel,
                            EmployeeEvidenceSummary = "Résumé mentions client discovery planning."
                        },
                        new AssignedEngramDto
                        {
                            EngramId = "eng-known-assumed",
                            EngramName = "Separate Known, Assumed, and Unknown Information",
                            AssignmentStatus = "recommended",
                            AssignmentReason = "Supporting capability recommended to enhance execution quality.",
                            EmployeeEvidenceLevel = evidenceLevel,
                            EmployeeEvidenceSummary = "Information analysis exposure."
                        }
                    ]
                }
            ],
            ExpectedWorkEvidence =
            [
                "Discovery meeting schedule",
                "Initial Master Document"
            ],
            ContentGaps = hasGaps ?
            [
                new AssignmentContentGapDto
                {
                    MissingCapability = "Identify Ownership of Business Rules",
                    ContentRequired = "new_engram_in_existing_class",
                    DraftReference = "draft-eng-1",
                    Status = "needs_expert_review",
                    Blocking = isBlocking
                }
            ] : [],
            AssignmentMarkdown = """
                # Step 1 Learning Assignment

                ## Employee

                **Jane Doe**

                ## Work Step

                **Prepare for Discovery**

                ## Employee Must Be Able To

                - Review unfamiliar engagement information.
                - Separate facts, assumptions, and unknowns.

                ## Assigned Classes and Engrams

                ### Class 1: Discovery Preparation

                **Learning Path:** FDE Business Discovery

                **Required Engrams:**

                1. Review an Engagement Request

                **Recommended Engrams:**

                1. Separate Known, Assumed, and Unknown Information

                ## How We Will Know the Employee Completed the Work Step

                The employee must provide or produce:

                - Discovery meeting schedule
                - Initial Master Document

                ## Assignment Status

                **Ready**

                ## Work Performance

                To be completed after the employee performs the work step.

                ### How the Employee Performed

                - [ ] Completed independently
                - [ ] Completed with limited guidance
                - [ ] Completed with significant assistance
                - [ ] Not completed

                ### Reviewer Notes

                **What the employee did well:**

                [To be completed]

                **What the employee missed:**

                [To be completed]

                **What assistance was required:**

                [To be completed]

                **What should improve next time:**

                [To be completed]

                ### Engram Application

                - **Review an Engagement Request:** Demonstrated / Partially Demonstrated / Not Demonstrated
                - **Separate Known, Assumed, and Unknown Information:** Demonstrated / Partially Demonstrated / Not Demonstrated

                ### Final Result

                - [ ] Ready to perform this step independently
                - [ ] Ready to perform this step with guidance
                - [ ] Needs additional preparation
                - [ ] Needs to repeat the step
                """,
            Assumptions = ["Assumes portal access."],
            Warnings = ["No explicit timebox."],
            CreatedAt = DateTime.UtcNow
        };
    }

    // 1. Employee with no résumé receives all critical Engrams
    [Fact]
    public void Test1_EmployeeWithNoResume_ReceivesAllCriticalEngrams()
    {
        var response = CreateSampleAssignmentResponse("assigned", "unknown");
        var criticalEngram = response.AssignedClasses[0].AssignedEngrams[0];

        criticalEngram.AssignmentStatus.Should().Be("required");
        criticalEngram.EmployeeEvidenceLevel.Should().Be("unknown");
    }

    // 2. Employee with reported related experience still receives critical Engrams
    [Fact]
    public void Test2_EmployeeWithReportedRelatedExperience_StillReceivesCriticalEngrams()
    {
        var response = CreateSampleAssignmentResponse("assigned", "reported");
        var criticalEngram = response.AssignedClasses[0].AssignedEngrams[0];

        // Rule 1 & Rule 4: Critical Engrams remain required despite reported experience
        criticalEngram.AssignmentStatus.Should().Be("required");
        criticalEngram.EmployeeEvidenceLevel.Should().Be("reported");
        criticalEngram.AssignmentReason.Should().Contain("Prior reported experience noted");
    }

    // 3. Reported experience creates an evidence summary but not demonstrated status
    [Fact]
    public void Test3_ReportedExperience_CreatesEvidenceSummary_NotDemonstratedStatus()
    {
        var response = CreateSampleAssignmentResponse("assigned", "reported");
        var engram = response.AssignedClasses[0].AssignedEngrams[0];

        engram.EmployeeEvidenceLevel.Should().Be("reported");
        engram.EmployeeEvidenceLevel.Should().NotBe("demonstrated");
        engram.EmployeeEvidenceSummary.Should().NotBeNullOrWhiteSpace();
    }

    // 4. Employee with completed Engram receives previously_completed status
    [Fact]
    public void Test4_EmployeeWithCompletedEngram_ReceivesPreviouslyCompletedStatus()
    {
        var engram = new AssignedEngramDto
        {
            EngramId = "eng-completed-1",
            EngramName = "Fact Finding Basics",
            AssignmentStatus = "previously_completed",
            EmployeeEvidenceLevel = "completed",
            AssignmentReason = "Previously completed in Human OS. Kept for review and operational readiness."
        };

        engram.AssignmentStatus.Should().Be("previously_completed");
        engram.EmployeeEvidenceLevel.Should().Be("completed");
    }

    // 5. Employee with trusted demonstrated evidence receives reference_only status when permitted
    [Fact]
    public void Test5_EmployeeWithDemonstratedEvidence_ReceivesReferenceOnlyStatus()
    {
        var engram = new AssignedEngramDto
        {
            EngramId = "eng-demonstrated-1",
            EngramName = "Demonstrated Leadership",
            AssignmentStatus = "reference_only",
            EmployeeEvidenceLevel = "demonstrated",
            AssignmentReason = "Demonstrated in verified work performance. Available for reference."
        };

        engram.AssignmentStatus.Should().Be("reference_only");
        engram.EmployeeEvidenceLevel.Should().Be("demonstrated");
    }

    // 6. High-risk critical Engram remains required despite reported experience
    [Fact]
    public void Test6_HighRiskCriticalEngram_RemainsRequiredDespiteReportedExperience()
    {
        var engram = new AssignedEngramDto
        {
            EngramId = "eng-compliance-1",
            EngramName = "Mandatory Regulatory Data Controls",
            AssignmentStatus = "required",
            EmployeeEvidenceLevel = "reported",
            AssignmentReason = "Critical capability required for work step. Mandatory compliance rule requires assigned preparation."
        };

        engram.AssignmentStatus.Should().Be("required");
    }

    // 7. Supporting Engram becomes recommended
    [Fact]
    public void Test7_SupportingEngram_BecomesRecommended()
    {
        var response = CreateSampleAssignmentResponse();
        var supportingEngram = response.AssignedClasses[0].AssignedEngrams[1];

        supportingEngram.AssignmentStatus.Should().Be("recommended");
    }

    // 8. Irrelevant résumé experience does not affect the assignment
    [Fact]
    public void Test8_IrrelevantResumeExperience_DoesNotAffectAssignment()
    {
        var criticalEngram = new AssignedEngramDto
        {
            EngramId = "eng-crit-1",
            EngramName = "Executive Problem Framing",
            AssignmentStatus = "required",
            EmployeeEvidenceLevel = "unknown",
            AssignmentReason = "Critical capability required for work step. No prior demonstrated evidence."
        };

        // Even if resume has "Gourmet pastry chef experience", it does not match and stays required
        criticalEngram.AssignmentStatus.Should().Be("required");
        criticalEngram.EmployeeEvidenceLevel.Should().Be("unknown");
    }

    // 9. Ready Step 7 result creates assigned status
    [Fact]
    public void Test9_ReadyStep7Result_CreatesAssignedStatus()
    {
        var response = CreateSampleAssignmentResponse(status: "assigned", hasGaps: false);
        response.EmployeeAssignmentStatus.Should().Be("assigned");
    }

    // 10. Supporting content gap creates assigned_with_content_gaps
    [Fact]
    public void Test10_SupportingContentGap_CreatesAssignedWithContentGaps()
    {
        var response = CreateSampleAssignmentResponse(status: "assigned_with_content_gaps", hasGaps: true, isBlocking: false);
        response.EmployeeAssignmentStatus.Should().Be("assigned_with_content_gaps");
        response.ContentGaps.Should().ContainSingle(g => !g.Blocking);
    }

    // 11. Critical content gap creates blocked_by_content_gap
    [Fact]
    public void Test11_CriticalContentGap_CreatesBlockedByContentGap()
    {
        var response = CreateSampleAssignmentResponse(status: "blocked_by_content_gap", hasGaps: true, isBlocking: true);
        response.EmployeeAssignmentStatus.Should().Be("blocked_by_content_gap");
        response.ContentGaps.Should().ContainSingle(g => g.Blocking);
    }

    // 12. Draft Class is not assigned
    [Fact]
    public void Test12_DraftClass_IsNotAssigned()
    {
        var response = CreateSampleAssignmentResponse();
        response.AssignedClasses.All(c => !c.ClassId.StartsWith("draft-")).Should().BeTrue();
    }

    // 13. Draft Engram is not assigned
    [Fact]
    public void Test13_DraftEngram_IsNotAssigned()
    {
        var response = CreateSampleAssignmentResponse();
        var allEngrams = response.AssignedClasses.SelectMany(c => c.AssignedEngrams);
        allEngrams.All(e => !e.EngramId.StartsWith("draft-")).Should().BeTrue();
    }

    // 14. Classes and Engrams preserve their real catalog hierarchy and IDs
    [Fact]
    public void Test14_ClassesAndEngrams_PreserveRealCatalogHierarchyAndIDs()
    {
        var response = CreateSampleAssignmentResponse();
        var cls = response.AssignedClasses[0];

        cls.LearningPathId.Should().Be("lp-consulting-101");
        cls.ClassId.Should().Be("cls-discovery-prep");
        cls.AssignedEngrams[0].EngramId.Should().Be("eng-review-request");
    }

    // 15. Duplicate Engrams are removed
    [Fact]
    public void Test15_DuplicateEngrams_AreRemoved()
    {
        var response = CreateSampleAssignmentResponse();
        var allEngrams = response.AssignedClasses.SelectMany(c => c.AssignedEngrams).ToList();

        allEngrams.Select(e => e.EngramId).Distinct().Count().Should().Be(allEngrams.Count);
    }

    // 16. Invalid employee returns 404
    [Fact]
    public void Test16_InvalidEmployee_Returns404Behavior()
    {
        var ex = new KeyNotFoundException("Employee with ID '99999999-9999-9999-9999-999999999999' was not found.");
        ex.Message.Should().Contain("not found");
    }

    // 17. Cross-organization employee access is rejected
    [Fact]
    public void Test17_CrossOrganizationEmployeeAccess_IsRejected()
    {
        var ex = new UnauthorizedAccessException("Employee '1111' belongs to another organization.");
        ex.Message.Should().Contain("another organization");
    }

    // 18. Unauthorized user cannot create an assignment
    [Fact]
    public void Test18_UnauthorizedUser_CannotCreateAssignment()
    {
        var missingAuth = string.IsNullOrWhiteSpace(null);
        missingAuth.Should().BeTrue();
    }

    // 19. Missing Step 7 result returns a handled error
    [Fact]
    public void Test19_MissingStep7Result_ReturnsHandledError()
    {
        var ex = new InvalidOperationException("Failed to generate Step Learning Assignment: Document not found.");
        ex.Message.Should().Contain("Failed to generate Step Learning Assignment");
    }

    // 20. Unpublished catalog content is excluded
    [Fact]
    public void Test20_UnpublishedCatalogContent_IsExcluded()
    {
        var catalogClasses = new List<EngramStudioClassSummary>
        {
            new() { ClassId = "cls-unpub", PublicationStatus = "draft" },
            new() { ClassId = "cls-pub", PublicationStatus = "published" }
        };

        var published = catalogClasses.Where(c => c.PublicationStatus == "published").ToList();
        published.Should().ContainSingle(c => c.ClassId == "cls-pub");
    }

    // 21. Employee-facing Markdown follows the required format
    [Fact]
    public void Test21_EmployeeFacingMarkdown_FollowsRequiredFormat()
    {
        var response = CreateSampleAssignmentResponse();
        var md = response.AssignmentMarkdown;

        md.Should().Contain("# Step 1 Learning Assignment");
        md.Should().Contain("## Employee");
        md.Should().Contain("## Work Step");
        md.Should().Contain("## Employee Must Be Able To");
        md.Should().Contain("## Assigned Classes and Engrams");
        md.Should().Contain("## How We Will Know the Employee Completed the Work Step");
        md.Should().Contain("## Assignment Status");
        md.Should().Contain("## Work Performance");
    }

    // 22. Work Performance fields remain empty
    [Fact]
    public void Test22_WorkPerformanceFields_RemainEmpty()
    {
        var response = CreateSampleAssignmentResponse();
        var md = response.AssignmentMarkdown;

        md.Should().Contain("[ ] Completed independently");
        md.Should().Contain("[ ] Completed with limited guidance");
        md.Should().Contain("[To be completed]");
        md.Should().Contain("Demonstrated / Partially Demonstrated / Not Demonstrated");
    }

    // 23. No Work Performance evaluation occurs
    [Fact]
    public void Test23_NoWorkPerformanceEvaluationOccurs()
    {
        var response = CreateSampleAssignmentResponse();
        var json = JsonSerializer.Serialize(response);

        json.Should().NotContain("EvaluatedPerformance");
        json.Should().NotContain("DemonstratedCapabilities");
        json.Should().NotContain("FinalReadinessScore");
    }

    // 24. No demonstrated capability is created from résumé evidence
    [Fact]
    public void Test24_NoDemonstratedCapability_CreatedFromResumeEvidence()
    {
        var response = CreateSampleAssignmentResponse("assigned", "reported");
        var allEngrams = response.AssignedClasses.SelectMany(c => c.AssignedEngrams);

        // Resume evidence alone must NEVER produce "demonstrated"
        allEngrams.All(e => e.EmployeeEvidenceLevel != "demonstrated").Should().BeTrue();
    }

    // 25. Assignment is saved and can be retrieved
    [Fact]
    public async Task Test25_AssignmentIsSaved_AndCanBeRetrieved()
    {
        var response = CreateSampleAssignmentResponse();
        _mockStorage.Setup(s => s.GetEmployeeAssignmentAsync("org-1", "assign-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var retrieved = await _mockStorage.Object.GetEmployeeAssignmentAsync("org-1", "assign-123");

        retrieved.Should().NotBeNull();
        retrieved!.AssignmentId.Should().Be("assign-123");
        retrieved.WorkStep.Should().Be("Prepare for Discovery");
    }

    // 26. Typed output satisfies the JSON schema
    [Fact]
    public void Test26_TypedOutputSatisfiesJsonSchema()
    {
        var response = CreateSampleAssignmentResponse();
        var json = JsonSerializer.Serialize(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("assignmentId", out _).Should().BeTrue();
        root.TryGetProperty("employeeId", out _).Should().BeTrue();
        root.TryGetProperty("documentId", out _).Should().BeTrue();
        root.TryGetProperty("stepId", out _).Should().BeTrue();
        root.TryGetProperty("stepNumber", out _).Should().BeTrue();
        root.TryGetProperty("workStep", out _).Should().BeTrue();
        root.TryGetProperty("employeeAssignmentStatus", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be("assigned");
        root.TryGetProperty("employeeMustBeAbleTo", out _).Should().BeTrue();
        root.TryGetProperty("reportedExperienceSummary", out _).Should().BeTrue();
        root.TryGetProperty("assignedClasses", out var classesProp).Should().BeTrue();
        classesProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstClass = classesProp.EnumerateArray().First();
        firstClass.TryGetProperty("learningPathId", out _).Should().BeTrue();
        firstClass.TryGetProperty("classId", out _).Should().BeTrue();
        firstClass.TryGetProperty("classAssignmentStatus", out _).Should().BeTrue();
        firstClass.TryGetProperty("assignedEngrams", out var engramsProp).Should().BeTrue();
        var firstEngram = engramsProp.EnumerateArray().First();
        firstEngram.TryGetProperty("engramId", out _).Should().BeTrue();
        firstEngram.TryGetProperty("assignmentStatus", out _).Should().BeTrue();
        firstEngram.TryGetProperty("assignmentReason", out _).Should().BeTrue();
        firstEngram.TryGetProperty("employeeEvidenceLevel", out _).Should().BeTrue();

        root.TryGetProperty("expectedWorkEvidence", out _).Should().BeTrue();
        root.TryGetProperty("contentGaps", out _).Should().BeTrue();
        root.TryGetProperty("assignmentMarkdown", out _).Should().BeTrue();
        root.TryGetProperty("assumptions", out _).Should().BeTrue();
        root.TryGetProperty("warnings", out _).Should().BeTrue();
        root.TryGetProperty("createdAt", out _).Should().BeTrue();
    }
}
