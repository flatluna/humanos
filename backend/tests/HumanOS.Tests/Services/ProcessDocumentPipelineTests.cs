using System.Text.Json;
using FluentAssertions;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Services.ProcessDocuments;
using HumanOS.Storage;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace HumanOS.Tests.Services;

public class ProcessDocumentPipelineTests
{
    private readonly Mock<IProcessDocumentStorageService> _mockStorage;
    private readonly ProcessDocumentExtractionService _realExtractionService;
    private readonly IConfiguration _configuration;

    public ProcessDocumentPipelineTests()
    {
        _mockStorage = new Mock<IProcessDocumentStorageService>();
        _mockStorage.Setup(s => s.IsConfigured).Returns(true);
        _mockStorage.Setup(s => s.UploadSourcePdfAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string org, string docId, string fn, Stream st, CancellationToken ct) =>
                $"process-documents/{org}/{docId}/source/{fn}");
        _mockStorage.Setup(s => s.SaveRawExtractionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RawDocumentExtractionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string org, string docId, RawDocumentExtractionResult raw, CancellationToken ct) =>
                $"process-documents/{org}/{docId}/extracted/document-intelligence-result.json");
        _mockStorage.Setup(s => s.SaveStructuredResultAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProcessDocumentStructure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string org, string docId, ProcessDocumentStructure st, CancellationToken ct) =>
                $"process-documents/{org}/{docId}/structured/process-structure.json");

        _realExtractionService = new ProcessDocumentExtractionService();

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ProcessDocument:MaxFileSizeBytes"] = "1048576" // 1 MB for testing
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private ProcessDocumentPipelineService CreatePipeline(
        IProcessDocumentStorageService? storage = null,
        IProcessDocumentExtractionService? extraction = null)
    {
        return new ProcessDocumentPipelineService(
            storage ?? _mockStorage.Object,
            extraction ?? _realExtractionService,
            _configuration);
    }

    // 1. Valid PDF upload
    [Fact]
    public async Task Test1_ValidPdfUpload_SucceedsWithCompletedStatus()
    {
        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf(
            "MANUAL DE PROCESO DE COMPRAS",
            "Capitulo 1: Adquisicion y Compras",
            "Paso 1: Recepcion de Requisicion");

        var pipeline = CreatePipeline();

        var response = await pipeline.ProcessAsync(
            pdfBytes,
            "proceso_compras.pdf",
            "application/pdf",
            userId: "user-123",
            organizationId: "org-abc",
            processName: "Proceso de Compras",
            processDescription: "Descripcion del flujo");

        response.Should().NotBeNull();
        response.Status.Should().Be("completed");
        response.DocumentId.Should().NotBeNullOrWhiteSpace();
        response.SourceStoragePath.Should().Contain("source/proceso_compras.pdf");
        response.StructuredResultPath.Should().Contain("structured/process-structure.json");
        response.ChaptersIdentified.Should().BeGreaterThanOrEqualTo(1);
        response.StepsIdentified.Should().BeGreaterThanOrEqualTo(1);
    }

    // 2. Non-PDF rejection
    [Fact]
    public async Task Test2_NonPdfFile_IsRejected()
    {
        var fakeWordBytes = "PK\x03\x04This is not a PDF file at all"u8.ToArray();
        var pipeline = CreatePipeline();

        var act = async () => await pipeline.ProcessAsync(
            fakeWordBytes,
            "document.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            userId: "user-123",
            organizationId: "org-abc");

        await act.Should().ThrowAsync<ProcessDocumentValidationException>()
            .Where(e => e.ErrorCode == "NonPdfFile" || e.ErrorCode == "InvalidPdfHeader");
    }

    // 3. Oversized file rejection
    [Fact]
    public async Task Test3_OversizedFile_IsRejected()
    {
        // 1MB is configured as max
        var oversizedBytes = new byte[1024 * 1024 + 100];
        // Set PDF header so it passes PDF validation first
        oversizedBytes[0] = 0x25; // %
        oversizedBytes[1] = 0x50; // P
        oversizedBytes[2] = 0x44; // D
        oversizedBytes[3] = 0x46; // F
        oversizedBytes[4] = 0x2D; // -

        var pipeline = CreatePipeline();

        var act = async () => await pipeline.ProcessAsync(
            oversizedBytes,
            "large_file.pdf",
            "application/pdf",
            userId: "user-123",
            organizationId: "org-abc");

        await act.Should().ThrowAsync<ProcessDocumentValidationException>()
            .Where(e => e.ErrorCode == "OversizedFile");
    }

    // 4. Unauthorized upload
    [Fact]
    public async Task Test4_UnauthorizedUpload_MissingUserIdOrOrgId_IsRejected()
    {
        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf("PROCESO", "Capitulo 1", "Paso 1");
        var pipeline = CreatePipeline();

        var actMissingUser = async () => await pipeline.ProcessAsync(
            pdfBytes, "file.pdf", "application/pdf", userId: null, organizationId: "org-abc");

        await actMissingUser.Should().ThrowAsync<ProcessDocumentUnauthorizedException>();

        var actMissingOrg = async () => await pipeline.ProcessAsync(
            pdfBytes, "file.pdf", "application/pdf", userId: "user-123", organizationId: null);

        await actMissingOrg.Should().ThrowAsync<ProcessDocumentUnauthorizedException>();
    }

    // 5. Successful storage in Azure Data Lake Storage
    [Fact]
    public async Task Test5_SuccessfulStorageInAzureDataLakeStorage()
    {
        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf(
            "PROCEDIMIENTO OPERATIVO ESTANDAR", "Capitulo 1: Operaciones", "Paso 1: Inicio");

        var pipeline = CreatePipeline();

        var response = await pipeline.ProcessAsync(
            pdfBytes,
            "sop.pdf",
            "application/pdf",
            userId: "user-1",
            organizationId: "org-77");

        _mockStorage.Verify(s => s.UploadSourcePdfAsync(
            "org-77",
            It.IsAny<string>(),
            "sop.pdf",
            It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockStorage.Verify(s => s.SaveRawExtractionAsync(
            "org-77",
            It.IsAny<string>(),
            It.IsAny<RawDocumentExtractionResult>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockStorage.Verify(s => s.SaveStructuredResultAsync(
            "org-77",
            It.IsAny<string>(),
            It.IsAny<ProcessDocumentStructure>(),
            It.IsAny<CancellationToken>()), Times.Once);

        response.SourceStoragePath.Should().StartWith("process-documents/org-77/");
        response.StructuredResultPath.Should().StartWith("process-documents/org-77/");
    }

    // 6. Text extraction
    [Fact]
    public async Task Test6_TextExtraction_ExtractsReadableTextAndParagraphs()
    {
        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf(
            "MANUAL DE PROCEDIMIENTOS FINANCIEROS",
            "Capitulo 1: Control de Gastos",
            "Paso 1: Auditoria de Facturas");

        var (raw, structured) = await _realExtractionService.ExtractAndStructureAsync(
            pdfBytes, "finanzas.pdf", "doc-1", null, null, "path/finanzas.pdf");

        raw.Should().NotBeNull();
        raw.PageCount.Should().BeGreaterThan(0);
        raw.Content.Should().Contain("MANUAL DE PROCEDIMIENTOS FINANCIEROS");
        raw.Content.Should().Contain("Auditoria de Facturas");
        raw.Paragraphs.Should().NotBeEmpty();
    }

    // 7. Table extraction
    [Fact]
    public async Task Test7_TableExtraction_ExtractsStructuredRowsAndHeaders()
    {
        const string tableText = """
            | Codigo | Descripcion | Responsable |
            | C-01   | Registro    | Juan Perez  |
            | C-02   | Aprobacion  | Maria Lopez |
            """;

        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf(
            "PROCESO CON TABLAS DE CONTROL",
            "Capitulo 1: Tabla de Matriz",
            "Paso 1: Llenado de Matriz",
            tableText);

        var (raw, structured) = await _realExtractionService.ExtractAndStructureAsync(
            pdfBytes, "tablas.pdf", "doc-2", null, null, "path/tablas.pdf");

        structured.Tables.Should().NotBeEmpty();
        var table = structured.Tables.First();
        table.Headers.Should().NotBeEmpty();
        table.Rows.Should().NotBeEmpty();
        table.SourcePage.Should().Be(1);
    }

    // 8. Chapter and process-step identification
    [Fact]
    public async Task Test8_ChapterAndProcessStepIdentification_WithAttributes()
    {
        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf(
            "MANUAL DE CONTRATACION",
            "Capitulo 2: Entrevistas",
            "Paso 3: Evaluacion Tecnica");

        var (raw, structured) = await _realExtractionService.ExtractAndStructureAsync(
            pdfBytes, "rrhh.pdf", "doc-3", null, null, "path/rrhh.pdf");

        structured.Chapters.Should().HaveCount(1);
        var chapter = structured.Chapters.First();
        chapter.Title.Should().Contain("Entrevistas");

        chapter.Steps.Should().HaveCount(1);
        var step = chapter.Steps.First();
        step.StepName.Should().Contain("Evaluacion Tecnica");
        step.Objective.Should().Contain("Garantizar la correcta recepcion");
        step.RequiredActivities.Should().Contain(a => a.Contains("Validar datos"));
        step.RequiredSkills.Should().Contain(s => s.Contains("SAP"));
        step.Deliverables.Should().Contain(d => d.Contains("Reporte de recepcion"));
        step.CompletionCondition.Should().Contain("APROBADA");
        step.Checklists.Should().Contain(c => c.Contains("Firma autorizada"));
        step.SourcePages.Should().Contain(1);
    }

    // 9. PDF with no clear process structure
    [Fact]
    public async Task Test9_PdfWithNoClearProcessStructure_RecordsWarning()
    {
        const string narrative = """
            La historia de la computacion moderna comenzo en el siglo XX.
            Diversos matematicos y cientificos teorizaron acerca de maquinas universales.
            Este documento describe consideraciones historicas y filosoficas sin ningun procedimiento.
            """;

        var pdfBytes = TestPdfGenerator.CreateNarrativePdf("HISTORIA DE LA COMPUTACION", narrative);

        var (raw, structured) = await _realExtractionService.ExtractAndStructureAsync(
            pdfBytes, "historia.pdf", "doc-4", null, null, "path/historia.pdf");

        structured.Warnings.Should().NotBeEmpty();
        structured.Warnings.Should().Contain(w => w.Contains("estructura"));
        structured.Chapters.Should().NotBeEmpty();
        structured.Chapters.First().Steps.Should().BeEmpty();
    }

    // 10. Document-processing failure
    [Fact]
    public async Task Test10_DocumentProcessingFailure_ThrowsHandledException()
    {
        var mockExtraction = new Mock<IProcessDocumentExtractionService>();
        mockExtraction.Setup(e => e.ExtractAndStructureAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("Corrupted PDF stream encountered."));

        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf("TITULO", "Capitulo 1", "Paso 1");
        var pipeline = CreatePipeline(extraction: mockExtraction.Object);

        var act = async () => await pipeline.ProcessAsync(
            pdfBytes, "doc.pdf", "application/pdf", userId: "user-1", organizationId: "org-1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Document-processing failure*");
    }

    // 11. Storage failure
    [Fact]
    public async Task Test11_StorageFailure_ThrowsHandledException()
    {
        var faultyStorage = new Mock<IProcessDocumentStorageService>();
        faultyStorage.Setup(s => s.UploadSourcePdfAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Azure Data Lake Storage connection timeout"));

        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf("TITULO", "Capitulo 1", "Paso 1");
        var pipeline = CreatePipeline(storage: faultyStorage.Object);

        var act = async () => await pipeline.ProcessAsync(
            pdfBytes, "doc.pdf", "application/pdf", userId: "user-1", organizationId: "org-1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Storage failure during PDF upload*");
    }

    // 12. Duplicate or retried request
    [Fact]
    public async Task Test12_DuplicateOrRetriedRequest_ReturnsCachedResultWithoutDuplicateStorage()
    {
        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf(
            "PROCESO DE PRUEBA DUPLICADOS", "Capitulo 1", "Paso 1");

        var pipeline = CreatePipeline();

        var firstResponse = await pipeline.ProcessAsync(
            pdfBytes, "duplicate.pdf", "application/pdf", userId: "user-1", organizationId: "org-dup");

        var secondResponse = await pipeline.ProcessAsync(
            pdfBytes, "duplicate.pdf", "application/pdf", userId: "user-1", organizationId: "org-dup");

        secondResponse.DocumentId.Should().Be(firstResponse.DocumentId);
        secondResponse.Status.Should().Be("completed");

        // Verify storage was only called for the initial upload
        _mockStorage.Verify(s => s.UploadSourcePdfAsync(
            "org-dup", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // 13. Structured JSON schema validation
    [Fact]
    public async Task Test13_StructuredJsonSchemaValidation_MatchesRequiredSchema()
    {
        var pdfBytes = TestPdfGenerator.CreateSampleProcessPdf(
            "PROCEDIMIENTO INTEGRAL DE VENTAS",
            "Capitulo 1: Prospeccion",
            "Paso 1: Calificacion de Leads");

        var (raw, structured) = await _realExtractionService.ExtractAndStructureAsync(
            pdfBytes, "ventas.pdf", "doc-schema", null, null, "path/ventas.pdf");

        var json = JsonSerializer.Serialize(structured);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // Verify all required root properties
        root.TryGetProperty("document_id", out var docIdProp).Should().BeTrue();
        root.TryGetProperty("process_name", out var procNameProp).Should().BeTrue();
        root.TryGetProperty("source_file", out var sourceFileProp).Should().BeTrue();
        sourceFileProp.TryGetProperty("file_name", out _).Should().BeTrue();
        sourceFileProp.TryGetProperty("content_type", out _).Should().BeTrue();
        sourceFileProp.TryGetProperty("storage_path", out _).Should().BeTrue();

        root.TryGetProperty("document_title", out var titleProp).Should().BeTrue();
        titleProp.GetString().Should().Be("PROCEDIMIENTO INTEGRAL DE VENTAS");

        root.TryGetProperty("chapters", out var chaptersProp).Should().BeTrue();
        chaptersProp.ValueKind.Should().Be(JsonValueKind.Array);
        var firstChapter = chaptersProp.EnumerateArray().First();
        firstChapter.TryGetProperty("chapter_id", out _).Should().BeTrue();
        firstChapter.TryGetProperty("chapter_number", out _).Should().BeTrue();
        firstChapter.TryGetProperty("title", out _).Should().BeTrue();
        firstChapter.TryGetProperty("modules", out _).Should().BeTrue();
        firstChapter.TryGetProperty("steps", out var stepsProp).Should().BeTrue();

        var firstStep = stepsProp.EnumerateArray().First();
        firstStep.TryGetProperty("step_id", out _).Should().BeTrue();
        firstStep.TryGetProperty("step_name", out _).Should().BeTrue();
        firstStep.TryGetProperty("objective", out _).Should().BeTrue();
        firstStep.TryGetProperty("required_activities", out _).Should().BeTrue();
        firstStep.TryGetProperty("required_skills", out _).Should().BeTrue();
        firstStep.TryGetProperty("deliverables", out _).Should().BeTrue();
        firstStep.TryGetProperty("completion_condition", out _).Should().BeTrue();
        firstStep.TryGetProperty("source_pages", out _).Should().BeTrue();

        root.TryGetProperty("tables", out var tablesProp).Should().BeTrue();
        tablesProp.ValueKind.Should().Be(JsonValueKind.Array);

        root.TryGetProperty("appendices", out var appendicesProp).Should().BeTrue();
        appendicesProp.ValueKind.Should().Be(JsonValueKind.Array);

        root.TryGetProperty("warnings", out var warningsProp).Should().BeTrue();
        warningsProp.ValueKind.Should().Be(JsonValueKind.Array);

        root.TryGetProperty("processing_status", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be("completed");
    }
}
