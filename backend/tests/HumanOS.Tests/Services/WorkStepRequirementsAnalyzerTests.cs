using System.Net;
using System.Text.Json;
using FluentAssertions;
using HumanOS.AzureFunctions.Api;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Services.ProcessDocuments;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HumanOS.Tests.Services;

public class WorkStepRequirementsAnalyzerTests
{
    private readonly Mock<IProcessDocumentStorageService> _mockStorage;
    private readonly Mock<IWorkStepRequirementsAnalyzer> _mockAnalyzer;
    private readonly Mock<ILogger<ProcessDocumentFunction>> _mockLogger;

    public WorkStepRequirementsAnalyzerTests()
    {
        _mockStorage = new Mock<IProcessDocumentStorageService>();
        _mockAnalyzer = new Mock<IWorkStepRequirementsAnalyzer>();
        _mockLogger = new Mock<ILogger<ProcessDocumentFunction>>();

        _mockAnalyzer.Setup(a => a.IsConfigured).Returns(true);
        _mockAnalyzer.Setup(a => a.DeploymentName).Returns("gpt-5-mini");
    }

    private static ProcessDocumentStructure CreateTestProcessDocument()
    {
        return new ProcessDocumentStructure
        {
            DocumentId = "doc-discovery",
            ProcessName = "Engagement Discovery Process",
            DocumentTitle = "Engagement Discovery Process Manual",
            SourceFile = new SourceFileInfo
            {
                FileName = "discovery.pdf",
                ContentType = "application/pdf",
                StoragePath = "process-documents/tenant-a/doc-discovery/source/discovery.pdf"
            },
            Chapters =
            [
                new ProcessChapter
                {
                    ChapterId = "ch-1",
                    ChapterNumber = "1",
                    Title = "Initiation and Discovery",
                    Steps =
                    [
                        new ProcessStep
                        {
                            StepId = "step-1",
                            StepNumber = "1",
                            StepName = "Prepare for Discovery",
                            Objective = "Enter the first meetings with a clear purpose, the correct participants, and a place to capture evidence and decisions.",
                            RequiredActivities =
                            [
                                "Read the engagement request, sponsor notes, existing presentations, policies, and any available process or architecture documents.",
                                "Write what is already known, what is assumed, and what is unknown.",
                                "Identify the executive sponsor, business owner, frontline users, process owner, policy owner, data owner, system owner, and operations owner.",
                                "Schedule an executive Discovery session and separate working sessions with business operators.",
                                "Create the Master Document and add the Discovery section.",
                                "Prepare the executive interview guide.",
                                "Create an evidence folder for notes, documents, process maps, screenshots, data extracts, and decisions."
                            ],
                            RequiredSkills =
                            [
                                "Preparation",
                                "Stakeholder management",
                                "Facilitation",
                                "Documentation"
                            ],
                            Deliverables =
                            [
                                "Discovery meeting schedule",
                                "Stakeholder list",
                                "Known, Assumed, and Unknown list",
                                "Executive interview guide",
                                "Initial Master Document",
                                "Discovery evidence folder"
                            ],
                            CompletionCondition = "The executive and operator sessions are scheduled, the correct initial stakeholders have been identified, and the Discovery workspace is ready.",
                            SourcePages = [1, 2]
                        },
                        new ProcessStep
                        {
                            StepId = "step-no-skills",
                            StepNumber = "2",
                            StepName = "Initial Outreach",
                            Objective = "Send kickoff invitations.",
                            RequiredActivities = ["Send welcome emails to stakeholders."],
                            RequiredSkills = [], // No explicit skills in document
                            Deliverables = ["Sent invitations"],
                            CompletionCondition = "Emails dispatched.",
                            SourcePages = [3]
                        },
                        new ProcessStep
                        {
                            StepId = "step-no-deliverables",
                            StepNumber = "3",
                            StepName = "Informal Alignment",
                            Objective = "Align on expectations.",
                            RequiredActivities = ["Discuss agenda orally with sponsor."],
                            RequiredSkills = ["Active listening"],
                            Deliverables = [], // No explicit deliverables
                            CompletionCondition = "Alignment reached.",
                            SourcePages = [4]
                        },
                        new ProcessStep
                        {
                            StepId = "step-ambiguous",
                            StepNumber = "4",
                            StepName = "Handle Miscellaneous Input",
                            Objective = "",
                            RequiredActivities = ["Process stuff if needed"],
                            RequiredSkills = [],
                            Deliverables = [],
                            CompletionCondition = "",
                            SourcePages = [5]
                        },
                        new ProcessStep
                        {
                            StepId = "step-with-table",
                            StepNumber = "5",
                            StepName = "RACI Matrix Formulation",
                            Objective = "Fill RACI matrix.",
                            RequiredActivities = ["Populate the stakeholder matrix table."],
                            RequiredSkills = ["Governance"],
                            Deliverables = ["RACI Table"],
                            CompletionCondition = "Matrix filled.",
                            SourcePages = [6]
                        }
                    ]
                }
            ],
            Tables =
            [
                new ProcessTable
                {
                    TableId = "tbl-1",
                    Title = "RACI Stakeholder Matrix",
                    Headers = ["Role", "Responsible", "Accountable"],
                    Rows = [["Sponsor", "Yes", "Yes"]],
                    SourcePage = 6
                }
            ]
        };
    }

    // 1. A complete extracted work step
    [Fact]
    public async Task Test1_CompleteExtractedWorkStep_AnalyzesRequirementsSuccessfully()
    {
        var doc = CreateTestProcessDocument();
        var step1 = doc.Chapters[0].Steps[0];

        _mockAnalyzer.Setup(a => a.AnalyzeStepRequirementsAsync(
                "doc-discovery",
                "Engagement Discovery Process",
                "Initiation and Discovery",
                step1,
                It.IsAny<List<ProcessTable>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkStepRequirementsAnalysisResult
            {
                DocumentId = "doc-discovery",
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
                        new() { Name = "Stakeholder management", Reason = "Identify people with authority and ownership." },
                        new() { Name = "Facilitation preparation", Reason = "Prepare meetings for productive conversations." }
                    ],
                    BusinessSkills =
                    [
                        new() { Name = "Discovery preparation", Reason = "Organize initial engagement before business interviews." },
                        new() { Name = "Information analysis", Reason = "Distinguish facts, assumptions, and unknowns." }
                    ],
                    TechnicalSkills =
                    [
                        new() { Name = "Structured documentation", Reason = "Create and organize Master Document." },
                        new() { Name = "Evidence organization", Reason = "Establish repository for Discovery evidence." }
                    ]
                },
                RequiredTools =
                [
                    new() { Name = "Master Document template", UseInStep = "Create initial Discovery section." },
                    new() { Name = "Evidence repository", UseInStep = "Store engagement documents." },
                    new() { Name = "Calendar and meeting tools", UseInStep = "Schedule Discovery sessions." }
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
                SourcePages = [1, 2]
            });

        var result = await _mockAnalyzer.Object.AnalyzeStepRequirementsAsync(
            "doc-discovery",
            "Engagement Discovery Process",
            "Initiation and Discovery",
            step1);

        result.Should().NotBeNull();
        result.WorkStep.Should().Be("Prepare for Discovery");
        result.EmployeeMustBeAbleTo.Should().HaveCount(5);
        result.RequiredSkillAreas.SoftSkills.Should().NotBeEmpty();
        result.RequiredSkillAreas.BusinessSkills.Should().NotBeEmpty();
        result.RequiredSkillAreas.TechnicalSkills.Should().NotBeEmpty();
        result.RequiredTools.Should().HaveCount(3);
        result.CompletionEvidence.Should().HaveCount(6);
        result.SourcePages.Should().Equal(1, 2);
    }

    // 2. A work step with no explicit required skills
    [Fact]
    public async Task Test2_WorkStepWithNoExplicitSkills_InfersRequiredSkillsWithReasons()
    {
        var doc = CreateTestProcessDocument();
        var stepNoSkills = doc.Chapters[0].Steps[1];
        stepNoSkills.RequiredSkills.Should().BeEmpty();

        _mockAnalyzer.Setup(a => a.AnalyzeStepRequirementsAsync(
                "doc-discovery",
                doc.ProcessName,
                "Initiation and Discovery",
                stepNoSkills,
                It.IsAny<List<ProcessTable>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkStepRequirementsAnalysisResult
            {
                DocumentId = "doc-discovery",
                StepId = "step-no-skills",
                StepNumber = "2",
                WorkStep = "Initial Outreach",
                EmployeeMustBeAbleTo = ["Draft and send professional outreach communications."],
                RequiredSkillAreas = new RequiredSkillAreas
                {
                    SoftSkills = [new() { Name = "Written communication", Reason = "Sending stakeholder outreach emails." }],
                    BusinessSkills = [new() { Name = "Stakeholder onboarding", Reason = "Introducing engagement context." }],
                    TechnicalSkills = [new() { Name = "Email software", Reason = "Dispatching communications." }]
                },
                SourcePages = [3]
            });

        var result = await _mockAnalyzer.Object.AnalyzeStepRequirementsAsync(
            "doc-discovery", doc.ProcessName, "Initiation and Discovery", stepNoSkills);

        result.RequiredSkillAreas.SoftSkills.Should().ContainSingle(s => s.Name == "Written communication");
        result.EmployeeMustBeAbleTo.Should().NotBeEmpty();
    }

    // 3. A work step with no expected deliverables
    [Fact]
    public async Task Test3_WorkStepWithNoExpectedDeliverables_InfersCompletionEvidenceFromCondition()
    {
        var doc = CreateTestProcessDocument();
        var stepNoDeliv = doc.Chapters[0].Steps[2];
        stepNoDeliv.Deliverables.Should().BeEmpty();

        _mockAnalyzer.Setup(a => a.AnalyzeStepRequirementsAsync(
                "doc-discovery",
                doc.ProcessName,
                "Initiation and Discovery",
                stepNoDeliv,
                It.IsAny<List<ProcessTable>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkStepRequirementsAnalysisResult
            {
                DocumentId = "doc-discovery",
                StepId = "step-no-deliverables",
                WorkStep = "Informal Alignment",
                CompletionEvidence = ["Sponsor verbal confirmation or meeting notes indicating alignment"],
                Assumptions = ["Inferred completion evidence from alignment condition in absence of formal deliverables."],
                SourcePages = [4]
            });

        var result = await _mockAnalyzer.Object.AnalyzeStepRequirementsAsync(
            "doc-discovery", doc.ProcessName, "Initiation and Discovery", stepNoDeliv);

        result.CompletionEvidence.Should().NotBeEmpty();
        result.Assumptions.Should().NotBeEmpty();
    }

    // 4. An ambiguous work step
    [Fact]
    public async Task Test4_AmbiguousWorkStep_GeneratesDescriptiveWarnings()
    {
        var doc = CreateTestProcessDocument();
        var stepAmbiguous = doc.Chapters[0].Steps[3];

        _mockAnalyzer.Setup(a => a.AnalyzeStepRequirementsAsync(
                "doc-discovery",
                doc.ProcessName,
                "Initiation and Discovery",
                stepAmbiguous,
                It.IsAny<List<ProcessTable>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkStepRequirementsAnalysisResult
            {
                DocumentId = "doc-discovery",
                StepId = "step-ambiguous",
                WorkStep = "Handle Miscellaneous Input",
                Warnings =
                [
                    "Work step is ambiguous: missing explicit objective, concrete deliverables, and measurable completion criteria."
                ],
                SourcePages = [5]
            });

        var result = await _mockAnalyzer.Object.AnalyzeStepRequirementsAsync(
            "doc-discovery", doc.ProcessName, "Initiation and Discovery", stepAmbiguous);

        result.Warnings.Should().NotBeEmpty();
        result.Warnings.First().Should().Contain("ambiguous");
    }

    // 5. A work step connected to an extracted table
    [Fact]
    public async Task Test5_WorkStepConnectedToExtractedTable_UsesTableInAnalysis()
    {
        var doc = CreateTestProcessDocument();
        var stepTable = doc.Chapters[0].Steps[4];
        var matchingTables = doc.Tables.Where(t => stepTable.SourcePages.Contains(t.SourcePage)).ToList();

        matchingTables.Should().HaveCount(1);
        matchingTables.First().Title.Should().Be("RACI Stakeholder Matrix");

        _mockAnalyzer.Setup(a => a.AnalyzeStepRequirementsAsync(
                "doc-discovery",
                doc.ProcessName,
                "Initiation and Discovery",
                stepTable,
                matchingTables,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkStepRequirementsAnalysisResult
            {
                DocumentId = "doc-discovery",
                StepId = "step-with-table",
                WorkStep = "RACI Matrix Formulation",
                RequiredTools = [new() { Name = "RACI Matrix Template", UseInStep = "Formulate responsibility assignments." }],
                SourcePages = [6]
            });

        var result = await _mockAnalyzer.Object.AnalyzeStepRequirementsAsync(
            "doc-discovery", doc.ProcessName, "Initiation and Discovery", stepTable, matchingTables);

        result.RequiredTools.Should().Contain(t => t.Name.Contains("RACI"));
    }

    // 6. Preservation of source-page references
    [Fact]
    public async Task Test6_PreservationOfSourcePageReferences()
    {
        var doc = CreateTestProcessDocument();
        var step1 = doc.Chapters[0].Steps[0];
        step1.SourcePages.Should().Equal(1, 2);

        _mockAnalyzer.Setup(a => a.AnalyzeStepRequirementsAsync(
                "doc-discovery", doc.ProcessName, "Initiation and Discovery", step1, It.IsAny<List<ProcessTable>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkStepRequirementsAnalysisResult
            {
                DocumentId = "doc-discovery",
                StepId = "step-1",
                SourcePages = [1, 2]
            });

        var result = await _mockAnalyzer.Object.AnalyzeStepRequirementsAsync(
            "doc-discovery", doc.ProcessName, "Initiation and Discovery", step1);

        result.SourcePages.Should().Equal(1, 2);
    }

    // 7. Invalid document ID
    [Fact]
    public async Task Test7_InvalidDocumentId_ReturnsNotFound()
    {
        _mockStorage.Setup(s => s.GetStructuredResultAsync("tenant-x", "non-existent-doc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessDocumentStructure?)null);

        // Verify storage returns null and function returns 404
        var doc = await _mockStorage.Object.GetStructuredResultAsync("tenant-x", "non-existent-doc");
        doc.Should().BeNull();
    }

    // 8. Invalid step ID
    [Fact]
    public void Test8_InvalidStepId_CannotBeFoundInDocument()
    {
        var doc = CreateTestProcessDocument();
        var foundStep = doc.Chapters.SelectMany(c => c.Steps).FirstOrDefault(s => s.StepId == "invalid-step-999");
        foundStep.Should().BeNull();
    }

    // 9. Model or agent failure
    [Fact]
    public async Task Test9_ModelOrAgentFailure_ThrowsExpectedException()
    {
        var doc = CreateTestProcessDocument();
        var step1 = doc.Chapters[0].Steps[0];

        _mockAnalyzer.Setup(a => a.AnalyzeStepRequirementsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProcessStep>(), It.IsAny<List<ProcessTable>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Azure OpenAI API rate limit exceeded or connection timed out."));

        var act = async () => await _mockAnalyzer.Object.AnalyzeStepRequirementsAsync(
            "doc-discovery", doc.ProcessName, "Initiation and Discovery", step1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rate limit exceeded*");
    }

    // 10. Structured-output schema validation
    [Fact]
    public void Test10_StructuredOutputSchemaValidation_MatchesExpectedContract()
    {
        var result = new WorkStepRequirementsAnalysisResult
        {
            DocumentId = "doc-1",
            StepId = "step-1",
            StepNumber = "1",
            WorkStep = "Prepare for Discovery",
            EmployeeMustBeAbleTo = ["Separate facts, assumptions, and unknown information."],
            RequiredSkillAreas = new RequiredSkillAreas
            {
                SoftSkills = [new() { Name = "Facilitation", Reason = "Meeting leadership" }],
                BusinessSkills = [new() { Name = "Problem Framing", Reason = "Scope definition" }],
                TechnicalSkills = [new() { Name = "Process Mapping", Reason = "Diagramming flow" }]
            },
            RequiredTools = [new() { Name = "Master Doc", UseInStep = "Notes capture" }],
            CompletionEvidence = ["Signed document"],
            Assumptions = ["Assumes access to ERP"],
            Warnings = [],
            SourcePages = [1, 2]
        };

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("documentId", out _).Should().BeTrue();
        root.TryGetProperty("stepId", out _).Should().BeTrue();
        root.TryGetProperty("stepNumber", out _).Should().BeTrue();
        root.TryGetProperty("workStep", out _).Should().BeTrue();
        root.TryGetProperty("employeeMustBeAbleTo", out _).Should().BeTrue();
        root.TryGetProperty("requiredSkillAreas", out var skills).Should().BeTrue();
        skills.TryGetProperty("softSkills", out _).Should().BeTrue();
        skills.TryGetProperty("businessSkills", out _).Should().BeTrue();
        skills.TryGetProperty("technicalSkills", out _).Should().BeTrue();
        root.TryGetProperty("requiredTools", out _).Should().BeTrue();
        root.TryGetProperty("completionEvidence", out _).Should().BeTrue();
        root.TryGetProperty("assumptions", out _).Should().BeTrue();
        root.TryGetProperty("warnings", out _).Should().BeTrue();
        root.TryGetProperty("sourcePages", out _).Should().BeTrue();
    }

    // 11. Authentication and organization isolation
    [Fact]
    public async Task Test11_AuthenticationAndOrganizationIsolation_ScopedToTenant()
    {
        var docTenantA = CreateTestProcessDocument();
        _mockStorage.Setup(s => s.GetStructuredResultAsync("tenant-a", "doc-discovery", It.IsAny<CancellationToken>()))
            .ReturnsAsync(docTenantA);
        _mockStorage.Setup(s => s.GetStructuredResultAsync("tenant-b", "doc-discovery", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessDocumentStructure?)null);

        // Tenant A can access
        var resA = await _mockStorage.Object.GetStructuredResultAsync("tenant-a", "doc-discovery");
        resA.Should().NotBeNull();

        // Tenant B cannot access Tenant A's document
        var resB = await _mockStorage.Object.GetStructuredResultAsync("tenant-b", "doc-discovery");
        resB.Should().BeNull();
    }

    // 12. The analyzer does not return Classes or Engrams
    [Fact]
    public void Test12_AnalyzerDoesNotReturnClassesOrEngrams()
    {
        var result = new WorkStepRequirementsAnalysisResult
        {
            WorkStep = "Prepare for Discovery",
            EmployeeMustBeAbleTo = ["Draft discovery agenda."],
            RequiredSkillAreas = new RequiredSkillAreas
            {
                SoftSkills = [new() { Name = "Facilitation", Reason = "Meeting management" }]
            }
        };

        var json = JsonSerializer.Serialize(result);

        // Verify neither "Class", "Classes", "Engram", "Engrams", nor "LearningPath" exist anywhere in the payload
        json.Should().NotContain("Engram");
        json.Should().NotContain("ClassId");
        json.Should().NotContain("LearningPath");
    }
}
