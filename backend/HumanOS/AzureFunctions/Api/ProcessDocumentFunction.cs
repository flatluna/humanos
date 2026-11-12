using System.Net;
using System.Text;
using System.Text.Json;
using HumanOS.Common;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Data;
using HumanOS.Services.ProcessDocuments;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// HTTP-triggered Azure Functions for Step 1 of the Work Step Learning Assignment feature:
/// Ingesting and extracting structured process documents (PDF) into Azure Data Lake Storage.
/// </summary>
public sealed class ProcessDocumentFunction
{
    private readonly ProcessDocumentPipelineService _pipeline;
    private readonly ProcessDocumentOrchestrator _orchestrator;
    private readonly IProcessDocumentStorageService _storageService;
    private readonly IWorkStepRequirementsAnalyzer _stepAnalyzer;
    private readonly IStepBusinessRequirementsAnalyzer _businessRequirementsAnalyzer;
    private readonly IProcessWideLearningRecommendationAnalyzer _processWideAnalyzer;
    private readonly HumanOS.Agents.Studio.WebGroundingService _webGrounding;
    private readonly HumanOS.Services.PerplexitySearchService _perplexitySearch;
    private readonly HumanOS.Services.ProcessDocuments.CourseOutlineService _courseOutlineService;
    private readonly IEngramStudioClassCandidateFinder _candidateFinder;
    private readonly IEngramStudioEngramSelector _engramSelector;
    private readonly ILearningContentCoverageAnalyzer _coverageAnalyzer;
    private readonly ILearningContentGapDraftGenerator _draftGenerator;
    private readonly IStepLearningAssignmentGenerator _assignmentGenerator;
    private readonly IEmployeeStepLearningAssignmentService _employeeAssignmentService;
    private readonly IDbContextFactory<HumanOsDbContext>? _dbContextFactory;
    private readonly ILogger<ProcessDocumentFunction> _logger;

    public ProcessDocumentFunction(
        ProcessDocumentPipelineService pipeline,
        ProcessDocumentOrchestrator orchestrator,
        IProcessDocumentStorageService storageService,
        IWorkStepRequirementsAnalyzer stepAnalyzer,
        IStepBusinessRequirementsAnalyzer businessRequirementsAnalyzer,
        IProcessWideLearningRecommendationAnalyzer processWideAnalyzer,
        HumanOS.Agents.Studio.WebGroundingService webGrounding,
        HumanOS.Services.PerplexitySearchService perplexitySearch,
        HumanOS.Services.ProcessDocuments.CourseOutlineService courseOutlineService,
        IEngramStudioClassCandidateFinder candidateFinder,
        IEngramStudioEngramSelector engramSelector,
        ILearningContentCoverageAnalyzer coverageAnalyzer,
        ILearningContentGapDraftGenerator draftGenerator,
        IStepLearningAssignmentGenerator assignmentGenerator,
        IEmployeeStepLearningAssignmentService employeeAssignmentService,
        IDbContextFactory<HumanOsDbContext>? dbContextFactory = null,
        ILogger<ProcessDocumentFunction>? logger = null)
    {
        _pipeline = pipeline;
        _orchestrator = orchestrator;
        _storageService = storageService;
        _stepAnalyzer = stepAnalyzer;
        _businessRequirementsAnalyzer = businessRequirementsAnalyzer;
        _processWideAnalyzer = processWideAnalyzer;
        _webGrounding = webGrounding;
        _perplexitySearch = perplexitySearch;
        _courseOutlineService = courseOutlineService;
        _candidateFinder = candidateFinder;
        _engramSelector = engramSelector;
        _coverageAnalyzer = coverageAnalyzer;
        _draftGenerator = draftGenerator;
        _assignmentGenerator = assignmentGenerator;
        _employeeAssignmentService = employeeAssignmentService;
        _dbContextFactory = dbContextFactory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessDocumentFunction>.Instance;
    }

    [Function("UploadProcessDocument")]
    public async Task<HttpResponseData> RunUploadAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve authentication/identity from headers or query
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        // Determine request content type
        var contentTypeHeader = request.Headers.TryGetValues("Content-Type", out var ctVals)
            ? ctVals.FirstOrDefault() ?? string.Empty
            : string.Empty;

        byte[]? fileBytes = null;
        string? fileName = null;
        string? fileContentType = null;
        string? processName = query["processName"] ?? query["process_name"];
        string? processDescription = query["processDescription"] ?? query["process_description"];
        var isAsync = string.Equals(query["async"], "true", StringComparison.OrdinalIgnoreCase);

        // 2. Parse payload based on Content-Type
        if (contentTypeHeader.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var multipart = await MultipartFormDataHelper.ParseAsync(request.Body, contentTypeHeader, cancellationToken);

            if (multipart.Fields.TryGetValue("user_id", out var formUserId) || multipart.Fields.TryGetValue("userId", out formUserId))
            {
                userId ??= formUserId;
            }
            if (multipart.Fields.TryGetValue("organization_id", out var formOrgId) || multipart.Fields.TryGetValue("organizationId", out formOrgId))
            {
                organizationId ??= formOrgId;
            }
            if (multipart.Fields.TryGetValue("process_name", out var formProcName) || multipart.Fields.TryGetValue("processName", out formProcName))
            {
                processName ??= formProcName;
            }
            if (multipart.Fields.TryGetValue("process_description", out var formProcDesc) || multipart.Fields.TryGetValue("processDescription", out formProcDesc))
            {
                processDescription ??= formProcDesc;
            }
            if (multipart.Fields.TryGetValue("is_async", out var formAsync) || multipart.Fields.TryGetValue("async", out formAsync))
            {
                isAsync = isAsync || string.Equals(formAsync, "true", StringComparison.OrdinalIgnoreCase);
            }

            var filePart = multipart.Files.FirstOrDefault();
            if (filePart != null)
            {
                fileBytes = filePart.Data;
                fileName = filePart.FileName;
                fileContentType = filePart.ContentType;
            }
        }
        else if (contentTypeHeader.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            ProcessDocumentUploadRequest? jsonRequest;
            try
            {
                jsonRequest = await JsonSerializer.DeserializeAsync<ProcessDocumentUploadRequest>(
                    request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
            }
            catch (JsonException ex)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidJson", $"Request JSON is invalid: {ex.Message}", cancellationToken);
            }

            if (jsonRequest != null)
            {
                userId ??= jsonRequest.UserId;
                organizationId ??= jsonRequest.OrganizationId;
                processName ??= jsonRequest.ProcessName;
                processDescription ??= jsonRequest.ProcessDescription;
                fileName = jsonRequest.FileName;
                fileContentType = jsonRequest.ContentType ?? "application/pdf";
                isAsync = isAsync || jsonRequest.IsAsync;

                if (!string.IsNullOrWhiteSpace(jsonRequest.ContentBase64))
                {
                    try
                    {
                        fileBytes = Convert.FromBase64String(jsonRequest.ContentBase64);
                    }
                    catch (FormatException)
                    {
                        return await FunctionResponseFactory.ErrorResponseAsync(
                            request, HttpStatusCode.BadRequest, "InvalidBase64", "The file content is not valid base64.", cancellationToken);
                    }
                }
            }
        }
        else if (contentTypeHeader.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            // Raw PDF stream in body
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, cancellationToken);
            fileBytes = ms.ToArray();
            fileName = query["fileName"] ?? "document.pdf";
            fileContentType = "application/pdf";
        }

        // 3. Authenticate and Authorize validation
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or form parameters.",
                cancellationToken);
        }

        // 4. File existence validation
        if (fileBytes == null || fileBytes.Length == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "MissingFile",
                "No PDF file was provided in the request.",
                cancellationToken);
        }

        // Validate mandatory process name
        if (string.IsNullOrWhiteSpace(processName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "ProcessNameRequired",
                "El nombre del proceso es obligatorio y debe ser único en la organización.",
                cancellationToken);
        }

        // Validate unique process name across the tenant
        if (_dbContextFactory != null)
        {
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                var explicitOrg = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
                var tenantGuid = await ResolveTenantGuidAsync(explicitOrg ?? organizationId, userId, db, cancellationToken);
                if (tenantGuid.HasValue)
                {
                    organizationId = tenantGuid.Value.ToString("D").ToLowerInvariant();
                    var trimmedName = processName.Trim();
                    var nameExists = await db.ProcessDocuments
                        .AnyAsync(p => p.TenantId == tenantGuid.Value && p.Title.ToLower() == trimmedName.ToLower(), cancellationToken);

                    if (nameExists)
                    {
                        return await FunctionResponseFactory.ErrorResponseAsync(
                            request,
                            HttpStatusCode.Conflict,
                            "DuplicateProcessName",
                            $"Ya existe un proceso registrado con el nombre '{trimmedName}'. El nombre del proceso debe ser único.",
                            cancellationToken);
                    }
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to check process name uniqueness in RunUploadAsync");
            }
        }

        // 5. PDF validation & Size check
        try
        {
            ProcessDocumentPipelineService.ValidatePdf(fileBytes, fileName, fileContentType);
        }
        catch (ProcessDocumentValidationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, ex.ErrorCode, ex.Message, cancellationToken);
        }

        if (fileBytes.Length > _pipeline.MaxFileSizeBytes)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "OversizedFile",
                $"File exceeds maximum permitted size of {_pipeline.MaxFileSizeBytes / (1024 * 1024)}MB.",
                cancellationToken);
        }

        // 6. Asynchronous execution mode if requested
        if (isAsync)
        {
            var asyncDocId = _orchestrator.StartAsync(
                fileBytes,
                fileName ?? "document.pdf",
                fileContentType,
                userId,
                organizationId,
                processName,
                processDescription);

            var asyncResponse = new ProcessDocumentUploadResponse
            {
                DocumentId = asyncDocId,
                FileName = fileName ?? "document.pdf",
                ProcessName = processName ?? string.Empty,
                Status = "accepted",
                StatusUrl = $"/api/process-documents/{asyncDocId}/status"
            };

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, asyncResponse, HttpStatusCode.Accepted, cancellationToken);
        }

        // 7. Synchronous execution
        try
        {
            var result = await _pipeline.ProcessAsync(
                fileBytes,
                fileName ?? "document.pdf",
                fileContentType,
                userId,
                organizationId,
                processName,
                processDescription,
                cancellationToken);

            _orchestrator.RegisterCompleted(result.DocumentId, result);

            return await FunctionResponseFactory.CreatedResponseAsync(
                request, result, cancellationToken);
        }
        catch (ProcessDocumentUnauthorizedException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized", ex.Message, cancellationToken);
        }
        catch (ProcessDocumentValidationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, ex.ErrorCode, ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            var code = ex.Message.StartsWith("Storage failure", StringComparison.OrdinalIgnoreCase)
                ? "StorageFailure"
                : "DocumentProcessingFailure";

            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, code, ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing process document");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "InternalError", ex.Message, cancellationToken);
        }
    }

    [Function("GetProcessDocumentStatus")]
    public async Task<HttpResponseData> RunStatusAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "process-documents/{documentId}/status")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        var status = _orchestrator.GetStatus(documentId);
        if (status == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound", $"No processing job found for document ID '{documentId}'.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
    }

    [Function("GetProcessDocumentStructure")]
    public async Task<HttpResponseData> RunGetStructureAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "process-documents/{documentId}")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var orgId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (string.IsNullOrWhiteSpace(orgId) && request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            orgId = tidVals.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(orgId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "OrganizationIdRequired", "organizationId is required.", cancellationToken);
        }

        var structured = await GetStructuredDocumentWithFallbackAsync(orgId, documentId, cancellationToken);
        if (structured == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound", $"Structured extraction not found for document ID '{documentId}'.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, structured, cancellationToken: cancellationToken);
    }

    [Function("GetProcessDocuments")]
    public async Task<HttpResponseData> RunGetProcessDocumentsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "process-documents")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId)) userId = query["userId"] ?? query["user_id"];
        if (string.IsNullOrWhiteSpace(organizationId)) organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.SuccessResponseAsync(request, Array.Empty<object>(), cancellationToken: cancellationToken);
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var tenantGuid = await ResolveTenantGuidAsync(organizationId, userId, db, cancellationToken);
            if (!tenantGuid.HasValue)
            {
                return await FunctionResponseFactory.SuccessResponseAsync(request, Array.Empty<object>(), cancellationToken: cancellationToken);
            }

            var list = await db.ProcessDocuments
                .AsNoTracking()
                .Where(p => p.TenantId == tenantGuid.Value)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new
                {
                    ProcessDocumentId = p.ProcessDocumentId,
                    Title = p.Title,
                    Code = p.Code,
                    Version = p.Version,
                    Status = p.Status,
                    Description = p.Description,
                    TotalStepsCount = p.TotalStepsCount,
                    UniqueSkillsCount = p.UniqueSkillsCount,
                    CreatedDate = p.CreatedDate,
                    UpdatedDate = p.UpdatedDate
                })
                .ToListAsync(cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, list, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve process documents");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "QueryFailure", ex.Message, cancellationToken);
        }
    }

    [Function("GetProcessDocumentDetail")]
    public async Task<HttpResponseData> RunGetProcessDocumentDetailAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "process-documents/{documentId}/detail")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId)) userId = query["userId"] ?? query["user_id"];
        if (string.IsNullOrWhiteSpace(organizationId)) organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "DbUnavailable", "Database context factory is not configured.", cancellationToken);
        }

        if (!Guid.TryParse(documentId, out var procGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidId", "Invalid document ID format.", cancellationToken);
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var tenantGuid = await ResolveTenantGuidAsync(organizationId, userId, db, cancellationToken);
            if (!tenantGuid.HasValue)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Unauthorized, "TenantNotFound", "Could not resolve tenant.", cancellationToken);
            }

            var record = await db.ProcessDocuments
                .AsNoTracking()
                .Include(p => p.Chapters)
                .Include(p => p.Steps.OrderBy(s => s.OrderIndex))
                    .ThenInclude(s => s.Skills)
                .FirstOrDefaultAsync(p => p.ProcessDocumentId == procGuid && p.TenantId == tenantGuid.Value, cancellationToken);

            if (record == null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "NotFound", $"Process document with ID '{documentId}' was not found in database.", cancellationToken);
            }

            // Also attempt to get the source file metadata or structured result from storage if available
            var storageDoc = await GetStructuredDocumentWithFallbackAsync(tenantGuid.Value.ToString("D"), documentId, cancellationToken);

            // dbo.ProcessStep has no SourceContent/StepImages columns (that real PDF text excerpt +
            // real extracted images only ever lived in blob storage) — so look them up by step number
            // from the same storage fallback used for SourceFile/Tables/Appendices above, otherwise the
            // "Review Process Steps" screen goes blank for every step once the process is saved to SQL.
            var storageStepsByNumber = (storageDoc?.Chapters ?? [])
                .SelectMany(c => c.Steps)
                .Where(s => !string.IsNullOrWhiteSpace(s.StepNumber))
                .GroupBy(s => s.StepNumber)
                .ToDictionary(g => g.Key, g => g.First());

            // Reconstruct ProcessDocumentStructureDto
            var chaptersDto = record.Chapters
                .OrderBy(c => c.ChapterNumber)
                .Select(c =>
                {
                    var chSteps = record.Steps
                        .Where(s => s.ProcessChapterId == c.ProcessChapterId)
                        .OrderBy(s => s.OrderIndex)
                        .Select(s =>
                        {
                            var activities = !string.IsNullOrWhiteSpace(s.ActivitiesJson)
                                ? JsonSerializer.Deserialize<List<string>>(s.ActivitiesJson) ?? []
                                : new List<string>();

                            var deliverables = !string.IsNullOrWhiteSpace(s.DeliverablesJson)
                                ? JsonSerializer.Deserialize<List<string>>(s.DeliverablesJson) ?? []
                                : new List<string>();

                            var sourcePages = !string.IsNullOrWhiteSpace(s.SourcePagesJson)
                                ? JsonSerializer.Deserialize<List<int>>(s.SourcePagesJson) ?? []
                                : new List<int>();

                            storageStepsByNumber.TryGetValue(s.StepNumber ?? string.Empty, out var storageStep);

                            return new ProcessStep
                            {
                                StepId = !string.IsNullOrWhiteSpace(s.StepNumber) ? $"step-{s.StepNumber}" : $"step-{s.OrderIndex}",
                                StepNumber = s.StepNumber,
                                StepName = s.Name,
                                Objective = s.Objective ?? string.Empty,
                                RequiredActivities = activities,
                                RequiredSkills = s.Skills.Select(sk => sk.SkillName).ToList(),
                                Deliverables = deliverables,
                                CompletionCondition = s.CompletionCondition ?? string.Empty,
                                SourcePages = sourcePages,
                                SourceContent = !string.IsNullOrWhiteSpace(s.SourceContent) ? s.SourceContent : (storageStep?.SourceContent ?? string.Empty),
                                StepImages = !string.IsNullOrWhiteSpace(s.StepImagesJson)
                                    ? JsonSerializer.Deserialize<List<ProcessStepImage>>(s.StepImagesJson) ?? []
                                    : (storageStep?.StepImages ?? []),
                                Substeps = [],
                                Checklists = []
                            };
                        })
                        .ToList();

                    return new ProcessChapter
                    {
                        ChapterId = $"ch-{c.ChapterNumber}",
                        ChapterNumber = c.ChapterNumber.ToString(),
                        Title = c.Title,
                        Description = c.Description ?? string.Empty,
                        Modules = [],
                        Steps = chSteps
                    };
                })
                .ToList();

            var structure = new ProcessDocumentStructure
            {
                DocumentId = record.ProcessDocumentId.ToString("D"),
                ProcessName = record.Title,
                DocumentTitle = record.Title,
                SourceFile = storageDoc?.SourceFile ?? new SourceFileInfo
                {
                    FileName = $"{record.Title}.pdf",
                    ContentType = "application/pdf",
                    StoragePath = record.StorageBlobPath
                },
                Chapters = chaptersDto,
                Tables = storageDoc?.Tables ?? [],
                Appendices = storageDoc?.Appendices ?? [],
                Warnings = storageDoc?.Warnings ?? [],
                ProcessingStatus = "completed"
            };

            // Reconstruct StepRequirements and StepCoverages
            var stepRequirements = new Dictionary<string, WorkStepRequirementsAnalysisResult>();
            var stepCoverages = new Dictionary<string, LearningContentCoverageResponse>();

            foreach (var st in record.Steps)
            {
                var stepKey = !string.IsNullOrWhiteSpace(st.StepNumber) ? $"step-{st.StepNumber}" : $"step-{st.OrderIndex}";

                var obsAbilities = !string.IsNullOrWhiteSpace(st.ObservableAbilitiesJson)
                    ? JsonSerializer.Deserialize<List<string>>(st.ObservableAbilitiesJson) ?? []
                    : new List<string>();

                var reqToolNames = !string.IsNullOrWhiteSpace(st.RequiredToolsJson)
                    ? JsonSerializer.Deserialize<List<string>>(st.RequiredToolsJson) ?? []
                    : new List<string>();

                var tools = reqToolNames.Select(tn => new RequiredToolItem { Name = tn, UseInStep = "Herramienta requerida para el paso" }).ToList();

                var sourcePages = !string.IsNullOrWhiteSpace(st.SourcePagesJson)
                    ? JsonSerializer.Deserialize<List<int>>(st.SourcePagesJson) ?? []
                    : new List<int>();

                // Build categorized skill lists
                var softSkills = new List<SkillItem>();
                var technicalSkills = new List<SkillItem>();
                var businessSkills = new List<SkillItem>();
                var deptProcSkills = new List<SkillItem>();
                var hrProcSkills = new List<SkillItem>();
                var aiSkills = new List<SkillItem>();

                var coveredList = new List<CoveredRequirementItem>();
                var gapList = new List<ContentGapItem>();

                foreach (var sk in st.Skills)
                {
                    var item = new SkillItem
                    {
                        SkillId = sk.SkillId?.ToString("D") ?? $"skill-{sk.Category}-{sk.SkillName.ToLower().Replace(" ", "-")}",
                        Name = sk.SkillName,
                        Reason = sk.Reason ?? "Requerimiento identificado para el paso",
                        Category = sk.Category,
                        IsAiSkill = sk.IsAiSkill
                    };

                    if (sk.IsAiSkill)
                    {
                        aiSkills.Add(item);
                    }
                    else if (sk.Category == "soft-skills")
                    {
                        softSkills.Add(item);
                    }
                    else if (sk.Category == "department-processes")
                    {
                        deptProcSkills.Add(item);
                    }
                    else if (sk.Category == "hr-processes")
                    {
                        hrProcSkills.Add(item);
                    }
                    else
                    {
                        technicalSkills.Add(item);
                    }

                    if (string.Equals(sk.CoverageStatus, "covered", StringComparison.OrdinalIgnoreCase))
                    {
                        coveredList.Add(new CoveredRequirementItem
                        {
                            Requirement = sk.SkillName,
                            Criticality = sk.Criticality,
                            Category = sk.Category,
                            IsAiSkill = sk.IsAiSkill,
                            MatchedCapabilityName = sk.MatchedCourseName
                        });
                    }
                    else
                    {
                        gapList.Add(new ContentGapItem
                        {
                            Requirement = sk.SkillName,
                            Criticality = sk.Criticality,
                            Category = sk.Category,
                            IsAiSkill = sk.IsAiSkill,
                            IsInCatalog = string.Equals(sk.CoverageStatus, "in_catalog_pending_course", StringComparison.OrdinalIgnoreCase),
                            TargetClassName = sk.MatchedCourseName,
                            GapReason = sk.Reason ?? "Requiere asignación o creación de curso formativo"
                        });
                    }
                }

                stepRequirements[stepKey] = new WorkStepRequirementsAnalysisResult
                {
                    DocumentId = record.ProcessDocumentId.ToString("D"),
                    StepId = stepKey,
                    StepNumber = st.StepNumber,
                    WorkStep = st.Name,
                    EmployeeMustBeAbleTo = obsAbilities,
                    RequiredSkillAreas = new RequiredSkillAreas
                    {
                        SoftSkills = softSkills,
                        TechnicalSkills = technicalSkills,
                        BusinessSkills = businessSkills,
                        DepartmentProcesses = deptProcSkills,
                        HrProcesses = hrProcSkills,
                        AiSkills = aiSkills
                    },
                    RequiredTools = tools,
                    CompletionEvidence = !string.IsNullOrWhiteSpace(st.CompletionCondition) ? [st.CompletionCondition] : [],
                    Assumptions = [],
                    Warnings = [],
                    SourcePages = sourcePages
                };

                stepCoverages[stepKey] = new LearningContentCoverageResponse
                {
                    DocumentId = record.ProcessDocumentId.ToString("D"),
                    StepId = stepKey,
                    StepNumber = st.StepNumber,
                    WorkStep = st.Name,
                    OverallCoverageStatus = gapList.Count == 0 ? "fully_covered" : (coveredList.Count > 0 ? "partially_covered" : "uncovered"),
                    CoverageSummary = $"{coveredList.Count} cubiertas, {gapList.Count} con brecha.",
                    CoveredRequirements = coveredList,
                    ContentGaps = gapList,
                    Assumptions = [],
                    Warnings = [],
                    SourcePages = sourcePages
                };
            }

            var responseObj = new
            {
                processDocumentId = record.ProcessDocumentId,
                title = record.Title,
                code = record.Code,
                version = record.Version,
                description = record.Description,
                departmentId = record.DepartmentId,
                jobRoleId = record.JobRoleId,
                status = record.Status,
                totalStepsCount = record.TotalStepsCount,
                uniqueSkillsCount = record.UniqueSkillsCount,
                createdDate = record.CreatedDate,
                updatedDate = record.UpdatedDate,
                structure = structure,
                stepRequirements = stepRequirements,
                stepCoverages = stepCoverages,
                processWideLearningRecommendation = storageDoc?.ProcessWideLearningRecommendation
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, responseObj, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve process document detail for {DocumentId}", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "QueryFailure", ex.Message, cancellationToken);
        }
    }

    /// <summary>Streams back the "enriched" PDF (index + extracted content + matched images) for HR
    /// to preview — this is the exact document that gets handed to Studio for course generation once
    /// the process is saved as a Capability, so HR can see/authorize the final result before approving.</summary>
    [Function("GetProcessDocumentEnrichedPdf")]
    public async Task<HttpResponseData> RunGetProcessDocumentEnrichedPdfAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "process-documents/{documentId}/enriched-pdf")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "DbUnavailable", "Database context factory is not configured.", cancellationToken);
        }

        if (!Guid.TryParse(documentId, out var procGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidId", "Invalid document ID format.", cancellationToken);
        }

        string? userId = null;
        string? organizationId = null;
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId)) userId = query["userId"] ?? query["user_id"];
        if (string.IsNullOrWhiteSpace(organizationId)) organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var tenantGuid = await ResolveTenantGuidAsync(organizationId, userId, db, cancellationToken);
            if (!tenantGuid.HasValue)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Unauthorized, "TenantNotFound", "Could not resolve tenant.", cancellationToken);
            }

            var record = await db.ProcessDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProcessDocumentId == procGuid && p.TenantId == tenantGuid.Value, cancellationToken);

            if (record == null || string.IsNullOrWhiteSpace(record.EnrichedPdfStoragePath))
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "EnrichedPdfNotFound",
                    "No enriched PDF has been generated for this process yet. Approve & save the process first.", cancellationToken);
            }

            var stream = await _storageService.DownloadEnrichedPdfAsync(
                record.StorageContainer, record.EnrichedPdfStoragePath, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/pdf");
            var dispositionName = string.IsNullOrWhiteSpace(record.EnrichedPdfFileName) ? "enriched.pdf" : record.EnrichedPdfFileName;
            response.Headers.Add("Content-Disposition", $"inline; filename=\"{dispositionName}\"");
            await stream.CopyToAsync(response.Body, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream enriched PDF for {DocumentId}", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "DownloadError", ex.Message, cancellationToken);
        }
    }

    [Function("DeleteProcessDocument")]
    public async Task<HttpResponseData> RunDeleteProcessDocumentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "process-documents/{documentId}")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId)) userId = query["userId"] ?? query["user_id"];
        if (string.IsNullOrWhiteSpace(organizationId)) organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "DbUnavailable", "Database context factory is not configured.", cancellationToken);
        }

        if (!Guid.TryParse(documentId, out var procGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidId", "Invalid document ID format.", cancellationToken);
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var tenantGuid = await ResolveTenantGuidAsync(organizationId, userId, db, cancellationToken);
            if (!tenantGuid.HasValue)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Unauthorized, "TenantNotFound", "Could not resolve tenant.", cancellationToken);
            }

            var recordExists = await db.ProcessDocuments
                .AnyAsync(p => p.ProcessDocumentId == procGuid && p.TenantId == tenantGuid.Value, cancellationToken);

            if (!recordExists)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "NotFound", $"Process document with ID '{documentId}' was not found.", cancellationToken);
            }

            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM dbo.ProcessStepSkill WHERE ProcessStepId IN (SELECT ProcessStepId FROM dbo.ProcessStep WHERE ProcessDocumentId = {0})", procGuid);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM dbo.ProcessStep WHERE ProcessDocumentId = {0}", procGuid);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM dbo.ProcessChapter WHERE ProcessDocumentId = {0}", procGuid);
            // Also remove the Capability/Skill that was created from this process (via "Save as Capability"),
            // so deleting a process doesn't leave an orphaned skill behind in the catalog.
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM dbo.Skill WHERE SourceProcessDocumentId = {0}", procGuid);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM dbo.ProcessDocument WHERE ProcessDocumentId = {0}", procGuid);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new { success = true, message = "Process document deleted successfully." }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete process document {DocumentId}", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "DeleteFailure", ex.Message, cancellationToken);
        }
    }

    [Function("AnalyzeWorkStepRequirements")]
    public async Task<HttpResponseData> RunAnalyzeStepAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/analyze-requirements")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Retrieve structured process JSON from Azure Data Lake Storage (with bidirectional fallback)
        var document = await GetStructuredDocumentWithFallbackAsync(normalizedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        // 3. Locate requested step
        ProcessStep? targetStep = null;
        string chapterOrPhase = string.Empty;

        foreach (var chapter in document.Chapters)
        {
            var match = chapter.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                targetStep = match;
                chapterOrPhase = chapter.Title;
                break;
            }
        }

        if (targetStep == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StepNotFound",
                $"Work step with ID '{stepId}' was not found in document '{documentId}'.",
                cancellationToken);
        }

        // 4. Find relevant tables (e.g. matching step source pages)
        List<ProcessTable> relevantTables = [];
        if (targetStep.SourcePages.Count > 0 && document.Tables.Count > 0)
        {
            relevantTables = document.Tables
                .Where(t => targetStep.SourcePages.Contains(t.SourcePage))
                .ToList();
        }

        // 5. Check analyzer configuration
        if (!_stepAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "WorkStepRequirementsAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        // Optional: Parse existing identified skills from body to prevent duplicate skill synonyms across steps
        var cumulativeSkills = new List<SkillItem>();
        try
        {
            if (request.Body != null && request.Body.CanRead && request.Body.Length > 0)
            {
                using var reader = new StreamReader(request.Body);
                var rawBody = await reader.ReadToEndAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(rawBody) && rawBody.TrimStart().StartsWith('['))
                {
                    var parsed = JsonSerializer.Deserialize<List<SkillItem>>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) cumulativeSkills.AddRange(parsed);
                }
            }
        }
        catch
        {
            // Optional body parameter; continue gracefully if absent
        }

        // Also query the organization's real Skill Catalog (dbo.Skill) from Database
        var catalogSkills = new List<SkillItem>();
        if (_dbContextFactory != null)
        {
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                var explicitOrg = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
                var tenantGuid = await ResolveTenantGuidAsync(explicitOrg ?? organizationId, userId, db, cancellationToken);
                if (tenantGuid.HasValue)
                {
                    var dbSkills = await db.Skills
                        .AsNoTracking()
                        .Where(s => s.TenantId == tenantGuid.Value && s.IsActive)
                        .Select(s => new SkillItem
                        {
                            SkillId = $"skill-{s.Category}-{s.Name.ToLower().Replace(" ", "-")}",
                            Name = s.Name,
                            Category = s.Category,
                            Reason = s.Description ?? "Existing organization catalog skill"
                        })
                        .ToListAsync(cancellationToken);

                    catalogSkills.AddRange(dbSkills);
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to load tenant skills from DB in RunAnalyzeStepAsync; proceeding with request context.");
            }
        }

        // 6. Perform requirements analysis
        try
        {
            var analysisResult = await _stepAnalyzer.AnalyzeStepRequirementsAsync(
                documentId,
                document.ProcessName,
                chapterOrPhase,
                targetStep,
                relevantTables,
                cumulativeSkills,
                catalogSkills,
                cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, analysisResult, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing requirements for step {StepId} of document {DocumentId}", stepId, documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AnalysisFailure",
                $"Failed to analyze step requirements: {ex.Message}",
                cancellationToken);
        }
    }

    /// <summary>
    /// PHASE B — deep business-requirements analysis for ONE step at a time (role, systems, trigger, business
    /// rules, exceptions), using only that step's own verbatim SourceContent captured during the fast Phase A
    /// index extraction (UploadProcessDocument). Updates and persists the step in place so the wizard can show
    /// the analyzed fields immediately. Intended to be called once per step, as the user reviews each step,
    /// never for the whole document at once.
    /// </summary>
    [Function("AnalyzeStepBusinessRequirements")]
    public async Task<HttpResponseData> RunAnalyzeStepBusinessRequirementsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/analyze-business-requirements")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId)) userId = query["userId"] ?? query["user_id"];
        if (string.IsNullOrWhiteSpace(organizationId)) organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Retrieve structured process JSON
        var document = await GetStructuredDocumentWithFallbackAsync(normalizedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        // 3. Locate requested step
        ProcessStep? targetStep = null;
        string chapterOrPhase = string.Empty;
        foreach (var chapter in document.Chapters)
        {
            var match = chapter.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                targetStep = match;
                chapterOrPhase = chapter.Title;
                break;
            }
        }

        if (targetStep == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StepNotFound",
                $"Work step with ID '{stepId}' was not found in document '{documentId}'.",
                cancellationToken);
        }

        if (!_businessRequirementsAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "StepBusinessRequirementsAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        // 4. Perform the deep per-step analysis and apply the results onto the step
        try
        {
            var analysis = await _businessRequirementsAnalyzer.AnalyzeStepAsync(
                documentId,
                document.ProcessName,
                chapterOrPhase,
                targetStep,
                cancellationToken);

            targetStep.PrimaryResponsibleRole = analysis.PrimaryResponsibleRole;
            targetStep.SupportingRoles = analysis.SupportingRoles;
            targetStep.SystemsAndTools = analysis.SystemsAndTools;
            targetStep.Trigger = analysis.Trigger;
            targetStep.RequiredInputs = analysis.RequiredInputs;
            targetStep.DecisionsAndHumanJudgment = analysis.DecisionsAndHumanJudgment;
            targetStep.BusinessRulesAndControls = analysis.BusinessRulesAndControls;
            targetStep.ExceptionsAndEscalations = analysis.ExceptionsAndEscalations;
            targetStep.EvidenceProducedOrRequired = analysis.EvidenceProducedOrRequired;
            targetStep.CompletionCondition = analysis.CompletionCondition;
            targetStep.MissingInformation = analysis.MissingInformation;
            targetStep.BusinessRequirementsAnalyzed = true;

            // Classify (Wizard Step 3): owner status + categorized lists with stable per-category IDs.
            targetStep.OwnerRole = analysis.OwnerRole;
            targetStep.OwnerStatus = analysis.OwnerIdentified ? "Identified" : "ToBeConfirmed";
            targetStep.ClassifiedInputs = BuildCodedList(analysis.Inputs, document.ProcessCode, targetStep.StepCode, "IN");
            targetStep.ClassifiedSystems = BuildCodedList(analysis.Systems, document.ProcessCode, targetStep.StepCode, "SYS");
            targetStep.ClassifiedDependencies = BuildCodedList(analysis.Dependencies, document.ProcessCode, targetStep.StepCode, "DEP");
            targetStep.ClassifiedBusinessRules = BuildCodedList(analysis.BusinessRules, document.ProcessCode, targetStep.StepCode, "BR");
            targetStep.ClassifiedExceptions = BuildCodedList(analysis.Exceptions, document.ProcessCode, targetStep.StepCode, "EX");
            targetStep.ClassifiedOutputs = BuildCodedList(analysis.Outputs, document.ProcessCode, targetStep.StepCode, "OUT");
            targetStep.ProcessClassified = true;

            // Map Activities, assigning stable ActivityIds [ProcessCode]-[StepCode]-A[NN] here (not by the AI).
            targetStep.Activities = analysis.Activities
                .OrderBy(a => a.ActivitySequence)
                .Select((a, idx) =>
                {
                    var sequence = a.ActivitySequence > 0 ? a.ActivitySequence : idx + 1;
                    return new ProcessActivity
                    {
                        ActivityId = $"{document.ProcessCode}-{targetStep.StepCode}-A{sequence:D2}",
                        ActivitySequence = sequence,
                        ActivityName = a.ActivityName,
                        ActivityDescription = a.ActivityDescription,
                        ActivityType = a.ActivityType,
                        PrimaryResponsibleRole = a.PrimaryResponsibleRole,
                        SystemsAndTools = a.SystemsAndTools,
                        HumanJudgmentRequired = a.HumanJudgmentRequired,
                        AiContribution = a.AiContribution,
                        HumanVerificationRequired = a.HumanVerificationRequired,
                        RequiredCapabilities = a.RequiredCapabilities,
                        SuggestedLearningMethod = a.SuggestedLearningMethod,
                        SourcePage = a.SourcePage,
                        RequiredInputs = a.RequiredInputs,
                        Outputs = a.Outputs,
                        BusinessRules = a.BusinessRules,
                        ReadinessCriteria = a.ReadinessCriteria,
                        SourceText = a.SourceText,
                        MissingInformation = a.MissingInformation
                    };
                })
                .ToList();

            // 5. Persist the updated document so the analyzed step is available on subsequent reads
            await _storageService.SaveStructuredResultAsync(normalizedOrgId, documentId, document, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, targetStep, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing business requirements for step {StepId} of document {DocumentId}", stepId, documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AnalysisFailure",
                $"Failed to analyze step business requirements: {ex.Message}",
                cancellationToken);
        }
    }

    [Function("AnalyzeProcessLearningNeeds")]
    public async Task<HttpResponseData> RunAnalyzeProcessLearningNeedsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/analyze-all-requirements")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Resolve the CANONICAL storage org key the same way GetProcessDocumentDetail does (via the
        // DB Tenant/Person tables), instead of guessing from whichever org id header happened to arrive.
        // This guarantees analysis results are read from / written to the exact same location the
        // detail endpoint reads from on page load.
        var resolvedOrgId = await ResolveCanonicalStorageOrgIdAsync(organizationId.Trim(), userId, cancellationToken);

        // 3. Retrieve structured process JSON from Azure Data Lake Storage (with bidirectional fallback)
        var document = await GetStructuredDocumentWithFallbackAsync(resolvedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        if (document.Chapters.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.UnprocessableEntity,
                "NoStepsFound",
                $"Process document '{documentId}' has no chapters/steps to analyze.",
                cancellationToken);
        }

        // 3. Check analyzer configuration
        if (!_processWideAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "ProcessWideLearningRecommendationAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        // 4. PHASE 1 — identify the skill gaps for the whole process WITHOUT any internet findings yet,
        // so each gap's exact name/category is fixed before we search (avoids generic, unfocused queries).
        var context = string.Join(" — ", document.Chapters.Select(c => c.Title));
        ProcessWideLearningRecommendationResult result;
        try
        {
            result = await _processWideAnalyzer.AnalyzeProcessAsync(
                documentId,
                document.ProcessName,
                document.Chapters,
                webFindings: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying whole-process skill gaps for document {DocumentId}", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AnalysisFailure",
                "Failed to analyze the process document. See logs for details.",
                cancellationToken);
        }

        // 5. PHASE 0 — match each gap against the tenant's EXISTING dbo.Skill catalog first; gaps that are
        // already covered skip internet search entirely (per user's "catalog before internet" design).
        if (result.SkillGaps.Count > 0 && _dbContextFactory != null && _processWideAnalyzer.IsConfigured)
        {
            try
            {
                await using var catalogDb = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var tenantGuid = await ResolveTenantGuidAsync(normalizedOrgId, userId, catalogDb, cancellationToken);
                if (tenantGuid.HasValue)
                {
                    var catalogSkills = await catalogDb.Skills
                        .AsNoTracking()
                        .Where(s => s.TenantId == tenantGuid.Value && s.IsActive)
                        .Select(s => new CatalogSkillEntry
                        {
                            SkillId = s.SkillId.ToString(),
                            Name = s.Name,
                            Description = s.Description,
                            Category = s.Category,
                        })
                        .ToListAsync(cancellationToken);

                    if (catalogSkills.Count > 0)
                    {
                        result.SkillGaps = await _processWideAnalyzer.MatchGapsToCatalogAsync(
                            result.SkillGaps, catalogSkills, cancellationToken);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AnalyzeProcessLearningNeeds: catalog-matching pass failed for document {DocumentId}, continuing without it.", documentId);
            }
        }

        // 6. PHASE 2 — real internet search, ONE targeted query PER skill gap NOT already covered by the
        // catalog (technology-skills first, since the user wants the exact tool/module named in the process
        // searched specifically, e.g. the precise Salesforce module mentioned — not a generic process-wide
        // query). Each skill's findings are kept in their own "### Skill: <name>" labeled section so course
        // assignment can't cross-contaminate skills.
        var gapsNeedingSearch = result.SkillGaps.Where(g => !g.IsFromCatalog).ToList();
        if (gapsNeedingSearch.Count > 0)
        {
            var orderedGaps = gapsNeedingSearch
                .OrderBy(g => g.Category == "technology-skills" ? 0 : 1)
                .ToList();

            var labeledFindings = new StringBuilder();
            foreach (var gap in orderedGaps)
            {
                var skillFindings = await SearchCoursesForSkillAsync(gap.Name, document.ProcessName, context, cancellationToken);
                if (!string.IsNullOrWhiteSpace(skillFindings))
                {
                    labeledFindings.AppendLine($"### Skill: {gap.Name}");
                    labeledFindings.AppendLine(skillFindings);
                    labeledFindings.AppendLine();
                }
            }

            // 6. PHASE 3 — assign only the courses found in each skill's own labeled section back to that gap.
            if (labeledFindings.Length > 0)
            {
                try
                {
                    gapsNeedingSearch = await _processWideAnalyzer.AssignCoursesToGapsAsync(
                        document.ProcessName,
                        gapsNeedingSearch,
                        labeledFindings.ToString(),
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "AnalyzeProcessLearningNeeds: course assignment pass failed for document {DocumentId}, skill gaps will have no courses.", documentId);
                    result.Warnings.Add("Course search completed but course assignment failed; no courses were attached.");
                }
            }
            else
            {
                result.Warnings.Add("No verified internet courses were found for any skill gap.");
            }
        }

        // 7. Preserve any previously manually-assigned skills (added by the user via the catalog picker)
        // that the fresh LLM pass didn't re-identify, so re-running the analysis for "new ideas" never
        // silently discards the user's manual choices.
        var previousManualGaps = document.ProcessWideLearningRecommendation?.SkillGaps
            .Where(g => g.IsManual)
            .ToList() ?? [];
        if (previousManualGaps.Count > 0)
        {
            var existingNames = new HashSet<string>(result.SkillGaps.Select(g => g.Name.Trim().ToLowerInvariant()));
            foreach (var manualGap in previousManualGaps)
            {
                if (existingNames.Add(manualGap.Name.Trim().ToLowerInvariant()))
                {
                    result.SkillGaps.Add(manualGap);
                }
            }
        }

        // 8. Persist the recommendation (with a fresh "analyzed at" timestamp) so it survives navigation
        // and future sessions without requiring the user to re-run the analysis.
        result.AnalyzedAt = DateTime.UtcNow;
        document.ProcessWideLearningRecommendation = result;
        try
        {
            await _storageService.SaveStructuredResultAsync(resolvedOrgId, documentId, document, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AnalyzeProcessLearningNeeds: failed to persist process-wide learning recommendation for document {DocumentId}.", documentId);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, result, cancellationToken: cancellationToken);
    }

    [Function("SaveProcessWideLearningRecommendation")]
    public async Task<HttpResponseData> RunSaveProcessWideLearningRecommendationAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "process-documents/{documentId}/process-wide-recommendation")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId)) userId = query["userId"] ?? query["user_id"];
        if (string.IsNullOrWhiteSpace(organizationId)) organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Unauthorized, "Unauthorized",
                "Organization ID must be provided via header (X-Azure-TID) or query parameter.", cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // Resolve the CANONICAL storage org key (same DB-backed resolution GetProcessDocumentDetail uses)
        // instead of guessing from whichever org id header happened to arrive on this request.
        var resolvedOrgId = await ResolveCanonicalStorageOrgIdAsync(organizationId.Trim(), userId, cancellationToken);

        ProcessWideLearningRecommendationResult? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ProcessWideLearningRecommendationResult>(
                request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", $"Invalid request body: {ex.Message}", cancellationToken);
        }

        if (body == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "Request body is required.", cancellationToken);
        }

        var document = await GetStructuredDocumentWithFallbackAsync(resolvedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.", cancellationToken);
        }

        // Manual-only saves (e.g. adding/removing a manually-assigned skill) never represent a new LLM
        // run, so the original "analyzed at" timestamp is preserved unless none exists yet.
        body.AnalyzedAt ??= document.ProcessWideLearningRecommendation?.AnalyzedAt;
        document.ProcessWideLearningRecommendation = body;

        try
        {
            await _storageService.SaveStructuredResultAsync(resolvedOrgId, documentId, document, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to persist process-wide learning recommendation for document {DocumentId}.", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "SaveFailure", "Failed to save the skill recommendation.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { success = true, analyzedAt = body.AnalyzedAt }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Runs ONE real internet search targeted at a single skill gap (e.g. the exact Salesforce module named
    /// in the process), preferring PerplexitySearchService and falling back to WebGroundingService.
    /// Returns the raw findings text (answer + citations), or null if no results/search unavailable.
    /// </summary>
    private async Task<string?> SearchCoursesForSkillAsync(
        string skillName,
        string processName,
        string chapterContext,
        CancellationToken cancellationToken)
    {
        if (_perplexitySearch.IsConfigured)
        {
            try
            {
                var searchQuery = $"Contexto del proceso \"{processName}\" ({chapterContext}).\n\n" +
                    $"Busca cursos reales y verificables (con título exacto, proveedor/plataforma y URL) disponibles " +
                    $"actualmente en internet específicamente para aprender: {skillName}.";
                var perplexityResult = await _perplexitySearch.SearchAsync(searchQuery, cancellationToken);
                var findings = perplexityResult.Answer;
                if (perplexityResult.Citations.Count > 0)
                {
                    findings += "\n\nCitations:\n" + string.Join("\n", perplexityResult.Citations);
                }
                return findings;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "SearchCoursesForSkillAsync: Perplexity search failed for skill '{SkillName}', trying web grounding fallback.", skillName);
            }
        }

        if (_webGrounding.IsConfigured)
        {
            try
            {
                var groundingResult = await _webGrounding.SearchAsync(
                    $"Best real training courses for: {skillName}", chapterContext, cancellationToken);
                return groundingResult.Text;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "SearchCoursesForSkillAsync: web grounding search failed for skill '{SkillName}'.", skillName);
            }
        }

        return null;
    }

    /// <summary>
    /// Given a recommended course (title/provider/URL) for a specific skill gap, extracts its real module
    /// index by scraping the course page; if that fails, the LLM drafts a plausible outline instead
    /// (clearly labeled "ai-suggested"). EngramsIQ Studio can use either as a starting draft to auto-generate
    /// the course content.
    /// </summary>
    [Function("GetCourseOutline")]
    public async Task<HttpResponseData> RunGetCourseOutlineAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "courses/outline")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CourseOutlineRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CourseOutlineRequest>(
                request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", $"Invalid request body: {ex.Message}", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.CourseTitle))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingCourseTitle", "'courseTitle' is required.", cancellationToken);
        }

        try
        {
            var result = await _courseOutlineService.GetOrGenerateOutlineAsync(body, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error getting/generating course outline for '{CourseTitle}'", body.CourseTitle);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "CourseOutlineFailure",
                "Failed to extract or generate a course outline. See logs for details.", cancellationToken);
        }
    }

    [Function("SearchCandidateClasses")]
    public async Task<HttpResponseData> RunSearchCandidateClassesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/candidate-classes")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Retrieve structured process JSON from Azure Data Lake Storage (with fallback)
        var document = await GetStructuredDocumentWithFallbackAsync(normalizedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        // 3. Locate requested step
        ProcessStep? targetStep = null;
        string chapterOrPhase = string.Empty;

        foreach (var chapter in document.Chapters)
        {
            var match = chapter.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                targetStep = match;
                chapterOrPhase = chapter.Title;
                break;
            }
        }

        if (targetStep == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StepNotFound",
                $"Work step with ID '{stepId}' was not found in document '{documentId}'.",
                cancellationToken);
        }

        // 4. Retrieve or generate Step 2 requirements analysis
        List<ProcessTable> relevantTables = [];
        if (targetStep.SourcePages.Count > 0 && document.Tables.Count > 0)
        {
            relevantTables = document.Tables
                .Where(t => targetStep.SourcePages.Contains(t.SourcePage))
                .ToList();
        }

        // Try reading requirements from request body first
        WorkStepRequirementsAnalysisResult? requirementsAnalysis = null;
        try
        {
            if (request.Body != null && request.Body.CanRead && request.Body.Length > 0)
            {
                using var reader = new StreamReader(request.Body);
                var rawBody = await reader.ReadToEndAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(rawBody) && rawBody.TrimStart().StartsWith('{'))
                {
                    requirementsAnalysis = JsonSerializer.Deserialize<WorkStepRequirementsAnalysisResult>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
        }
        catch { }

        // Resolve real tenant GUID
        var resolvedOrg = normalizedOrgId;
        var catalogSkills = new List<SkillItem>();
        if (_dbContextFactory != null)
        {
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                var explicitOrg = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
                var tenantGuid = await ResolveTenantGuidAsync(explicitOrg ?? organizationId, userId, db, cancellationToken);
                if (tenantGuid.HasValue)
                {
                    resolvedOrg = tenantGuid.Value.ToString();
                    var dbSkills = await db.Skills
                        .AsNoTracking()
                        .Where(s => s.TenantId == tenantGuid.Value && s.IsActive)
                        .Select(s => new SkillItem
                        {
                            SkillId = $"skill-{s.Category}-{s.Name.ToLower().Replace(" ", "-")}",
                            Name = s.Name,
                            Category = s.Category,
                            Reason = s.Description ?? "Existing organization catalog skill"
                        })
                        .ToListAsync(cancellationToken);

                    catalogSkills.AddRange(dbSkills);
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to resolve tenant or load skills in RunSearchCandidateClassesAsync");
            }
        }

        if (requirementsAnalysis == null)
        {
            if (!_stepAnalyzer.IsConfigured)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.ServiceUnavailable,
                    "AnalyzerNotConfigured",
                    "WorkStepRequirementsAnalyzer is not configured (missing Azure OpenAI credentials).",
                    cancellationToken);
            }

            try
            {
                requirementsAnalysis = await _stepAnalyzer.AnalyzeStepRequirementsAsync(
                    documentId,
                    document.ProcessName,
                    chapterOrPhase,
                    targetStep,
                    relevantTables,
                    previouslyIdentifiedSkills: null,
                    catalogSkills: catalogSkills,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze requirements for step {StepId}", stepId);
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.InternalServerError,
                    "AnalysisFailure",
                    $"Failed to analyze step requirements prior to catalog search: {ex.Message}",
                    cancellationToken);
            }
        }

        // 5. Search Engram Studio for candidate Classes
        try
        {
            var candidateClasses = await _candidateFinder.FindCandidateClassesAsync(
                requirementsAnalysis,
                resolvedOrg,
                language: query["language"],
                maxResults: 5,
                cancellationToken: cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, candidateClasses, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Engram Studio catalog is unavailable", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Engram Studio catalog was unavailable during search");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "CatalogUnavailable",
                "Engram Studio catalog is currently unavailable. Please try again later.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find candidate classes for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "SearchFailure",
                $"Failed to search candidate classes: {ex.Message}",
                cancellationToken);
        }
    }

    [Function("SelectRelevantEngrams")]
    public async Task<HttpResponseData> RunSelectRelevantEngramsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/relevant-engrams")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Retrieve structured process JSON from Azure Data Lake Storage (with fallback)
        var document = await GetStructuredDocumentWithFallbackAsync(normalizedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        // 3. Locate requested step
        ProcessStep? targetStep = null;
        string chapterOrPhase = string.Empty;

        foreach (var chapter in document.Chapters)
        {
            var match = chapter.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                targetStep = match;
                chapterOrPhase = chapter.Title;
                break;
            }
        }

        if (targetStep == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StepNotFound",
                $"Work step with ID '{stepId}' was not found in document '{documentId}'.",
                cancellationToken);
        }

        // 4. Retrieve or generate Step 2 requirements analysis
        List<ProcessTable> relevantTables = [];
        if (targetStep.SourcePages.Count > 0 && document.Tables.Count > 0)
        {
            relevantTables = document.Tables
                .Where(t => targetStep.SourcePages.Contains(t.SourcePage))
                .ToList();
        }

        // Resolve real tenant GUID and load catalog skills
        var resolvedOrg = normalizedOrgId;
        var catalogSkills = new List<SkillItem>();
        if (_dbContextFactory != null)
        {
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                var explicitOrg = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
                var tenantGuid = await ResolveTenantGuidAsync(explicitOrg ?? organizationId, userId, db, cancellationToken);
                if (tenantGuid.HasValue)
                {
                    resolvedOrg = tenantGuid.Value.ToString();
                    var dbSkills = await db.Skills
                        .AsNoTracking()
                        .Where(s => s.TenantId == tenantGuid.Value && s.IsActive)
                        .Select(s => new SkillItem
                        {
                            SkillId = $"skill-{s.Category}-{s.Name.ToLower().Replace(" ", "-")}",
                            Name = s.Name,
                            Category = s.Category,
                            Reason = s.Description ?? "Existing organization catalog skill"
                        })
                        .ToListAsync(cancellationToken);

                    catalogSkills.AddRange(dbSkills);
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to resolve tenant or load skills in RunSelectRelevantEngramsAsync");
            }
        }

        if (!_stepAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "WorkStepRequirementsAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        WorkStepRequirementsAnalysisResult requirementsAnalysis;
        try
        {
            requirementsAnalysis = await _stepAnalyzer.AnalyzeStepRequirementsAsync(
                documentId,
                document.ProcessName,
                chapterOrPhase,
                targetStep,
                relevantTables,
                previouslyIdentifiedSkills: null,
                catalogSkills: catalogSkills,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze requirements for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AnalysisFailure",
                $"Failed to analyze step requirements: {ex.Message}",
                cancellationToken);
        }

        // 5. Retrieve or generate Step 3 candidate Classes
        CandidateClassSearchResponse candidateSearch;
        try
        {
            candidateSearch = await _candidateFinder.FindCandidateClassesAsync(
                requirementsAnalysis,
                resolvedOrg,
                language: query["language"],
                maxResults: 5,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search candidate classes for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "CandidateSearchFailure",
                $"Failed to search candidate classes: {ex.Message}",
                cancellationToken);
        }

        // 6. Verify that all candidate Class IDs came from Step 3 (do not trust arbitrary Class IDs sent by UX)
        var candidateClassesToUse = candidateSearch.CandidateClasses;

        if (request.Body != null && request.Body.CanRead && request.Body.Length > 0)
        {
            try
            {
                var bodyOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var selectRequest = await JsonSerializer.DeserializeAsync<SelectRelevantEngramsRequest>(request.Body, bodyOptions, cancellationToken);
                if (selectRequest?.CandidateClassIds != null && selectRequest.CandidateClassIds.Count > 0)
                {
                    var validStep3ClassIds = candidateSearch.CandidateClasses
                        .Select(c => c.ClassId)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var unverifiedIds = selectRequest.CandidateClassIds
                        .Where(id => !validStep3ClassIds.Contains(id))
                        .ToList();

                    if (unverifiedIds.Count > 0)
                    {
                        return await FunctionResponseFactory.ErrorResponseAsync(
                            request,
                            HttpStatusCode.BadRequest,
                            "UnverifiedCandidateClassId",
                            $"Candidate class IDs must come from the Step 3 candidate search result. Unverified IDs: {string.Join(", ", unverifiedIds)}",
                            cancellationToken);
                    }

                    candidateClassesToUse = candidateSearch.CandidateClasses
                        .Where(c => selectRequest.CandidateClassIds.Contains(c.ClassId, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }
            }
            catch (JsonException)
            {
                // If body isn't a custom filter payload, proceed with candidateSearch.CandidateClasses as default
            }
        }

        // 7. Select relevant Engrams via EngramStudioEngramSelector
        try
        {
            var engramSelection = await _engramSelector.SelectRelevantEngramsAsync(
                requirementsAnalysis,
                candidateClassesToUse,
                resolvedOrg,
                language: query["language"],
                cancellationToken: cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, engramSelection, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Engram Studio catalog is unavailable", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Engram Studio catalog was unavailable during Engram selection");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "CatalogUnavailable",
                "Engram Studio catalog is currently unavailable. Please try again later.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select relevant Engrams for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "SelectionFailure",
                $"Failed to select relevant Engrams: {ex.Message}",
                cancellationToken);
        }
    }

    [Function("AnalyzeLearningContentCoverage")]
    public async Task<HttpResponseData> RunAnalyzeContentCoverageAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/content-coverage")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Retrieve structured process JSON from Azure Data Lake Storage (with fallback)
        var document = await GetStructuredDocumentWithFallbackAsync(normalizedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        // 3. Locate requested step
        ProcessStep? targetStep = null;
        string chapterOrPhase = string.Empty;

        foreach (var chapter in document.Chapters)
        {
            var match = chapter.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                targetStep = match;
                chapterOrPhase = chapter.Title;
                break;
            }
        }

        if (targetStep == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StepNotFound",
                $"Work step with ID '{stepId}' was not found in document '{documentId}'.",
                cancellationToken);
        }

        // 4. Retrieve or generate Step 2 requirements analysis
        List<ProcessTable> relevantTables = [];
        if (targetStep.SourcePages.Count > 0 && document.Tables.Count > 0)
        {
            relevantTables = document.Tables
                .Where(t => targetStep.SourcePages.Contains(t.SourcePage))
                .ToList();
        }

        if (!_stepAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "WorkStepRequirementsAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        WorkStepRequirementsAnalysisResult requirementsAnalysis;
        try
        {
            requirementsAnalysis = await _stepAnalyzer.AnalyzeStepRequirementsAsync(
                documentId,
                document.ProcessName,
                chapterOrPhase,
                targetStep,
                relevantTables,
                previouslyIdentifiedSkills: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze requirements for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AnalysisFailure",
                $"Failed to analyze step requirements: {ex.Message}",
                cancellationToken);
        }

        // 5. Retrieve or generate Step 3 candidate Classes
        CandidateClassSearchResponse candidateSearch;
        try
        {
            candidateSearch = await _candidateFinder.FindCandidateClassesAsync(
                requirementsAnalysis,
                normalizedOrgId,
                language: query["language"],
                maxResults: 5,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search candidate classes for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "CandidateSearchFailure",
                $"Failed to search candidate classes: {ex.Message}",
                cancellationToken);
        }

        // 6. Select relevant Engrams via EngramStudioEngramSelector (Step 4)
        RelevantEngramsResponse step4Result;
        try
        {
            step4Result = await _engramSelector.SelectRelevantEngramsAsync(
                requirementsAnalysis,
                candidateSearch.CandidateClasses,
                normalizedOrgId,
                language: query["language"],
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select relevant Engrams for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "SelectionFailure",
                $"Failed to select relevant Engrams: {ex.Message}",
                cancellationToken);
        }

        // 7. Perform Step 5 Content Coverage and Gap Analysis
        if (!_coverageAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "LearningContentCoverageAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        try
        {
            var coverageOrgId = normalizedOrgId;
            if (_dbContextFactory != null)
            {
                try
                {
                    using var db = _dbContextFactory.CreateDbContext();
                    var explicitOrg = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
                    var resolvedTenant = await ResolveTenantGuidAsync(explicitOrg ?? organizationId, userId, db, cancellationToken);
                    if (resolvedTenant.HasValue)
                    {
                        coverageOrgId = resolvedTenant.Value.ToString();
                    }
                }
                catch { }
            }

            var coverageResult = await _coverageAnalyzer.AnalyzeCoverageAndGapsAsync(
                step4Result,
                requirementsAnalysis,
                coverageOrgId,
                cancellationToken: cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, coverageResult, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze content coverage for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "CoverageAnalysisFailure",
                $"Failed to analyze learning content coverage: {ex.Message}",
                cancellationToken);
        }
    }

    [Function("GenerateContentGapDraft")]
    public async Task<HttpResponseData> RunGenerateContentGapDraftAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/content-gap-draft")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Retrieve structured process JSON from Azure Data Lake Storage (with fallback)
        var document = await GetStructuredDocumentWithFallbackAsync(normalizedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        // 3. Locate requested step
        ProcessStep? targetStep = null;
        string chapterOrPhase = string.Empty;

        foreach (var chapter in document.Chapters)
        {
            var match = chapter.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                targetStep = match;
                chapterOrPhase = chapter.Title;
                break;
            }
        }

        if (targetStep == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StepNotFound",
                $"Work step with ID '{stepId}' was not found in document '{documentId}'.",
                cancellationToken);
        }

        // 4. Retrieve or generate Step 2 requirements analysis
        List<ProcessTable> relevantTables = [];
        if (targetStep.SourcePages.Count > 0 && document.Tables.Count > 0)
        {
            relevantTables = document.Tables
                .Where(t => targetStep.SourcePages.Contains(t.SourcePage))
                .ToList();
        }

        if (!_stepAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "WorkStepRequirementsAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        WorkStepRequirementsAnalysisResult requirementsAnalysis;
        try
        {
            requirementsAnalysis = await _stepAnalyzer.AnalyzeStepRequirementsAsync(
                documentId,
                document.ProcessName,
                chapterOrPhase,
                targetStep,
                relevantTables,
                previouslyIdentifiedSkills: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze requirements for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AnalysisFailure",
                $"Failed to analyze step requirements: {ex.Message}",
                cancellationToken);
        }

        // 5. Retrieve or generate Step 3 candidate Classes
        CandidateClassSearchResponse candidateSearch;
        try
        {
            candidateSearch = await _candidateFinder.FindCandidateClassesAsync(
                requirementsAnalysis,
                normalizedOrgId,
                language: query["language"],
                maxResults: 5,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search candidate classes for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "CandidateSearchFailure",
                $"Failed to search candidate classes: {ex.Message}",
                cancellationToken);
        }

        // 6. Select relevant Engrams via EngramStudioEngramSelector (Step 4)
        RelevantEngramsResponse step4Result;
        try
        {
            step4Result = await _engramSelector.SelectRelevantEngramsAsync(
                requirementsAnalysis,
                candidateSearch.CandidateClasses,
                normalizedOrgId,
                language: query["language"],
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select relevant Engrams for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "SelectionFailure",
                $"Failed to select relevant Engrams: {ex.Message}",
                cancellationToken);
        }

        // 7. Perform Step 5 Content Coverage Analysis
        if (!_coverageAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "LearningContentCoverageAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        LearningContentCoverageResponse step5Result;
        try
        {
            step5Result = await _coverageAnalyzer.AnalyzeCoverageAndGapsAsync(
                step4Result,
                requirementsAnalysis,
                normalizedOrgId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze content coverage for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "CoverageAnalysisFailure",
                $"Failed to analyze learning content coverage: {ex.Message}",
                cancellationToken);
        }

        // 8. Perform Step 6 Content Gap Draft Generation
        if (!_draftGenerator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "GeneratorNotConfigured",
                "LearningContentGapDraftGenerator is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        try
        {
            var draftResult = await _draftGenerator.GenerateContentGapDraftAsync(
                step5Result,
                requirementsAnalysis,
                normalizedOrgId,
                language: query["language"],
                cancellationToken: cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, draftResult, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate content gap drafts for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "DraftGenerationFailure",
                $"Failed to generate content gap drafts: {ex.Message}",
                cancellationToken);
        }
    }

    [Function("GenerateStepLearningAssignment")]
    public async Task<HttpResponseData> RunGenerateStepLearningAssignmentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/learning-assignment")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Retrieve structured process JSON from Azure Data Lake Storage
        var document = await _storageService.GetStructuredResultAsync(normalizedOrgId, documentId, cancellationToken);
        if (document == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "DocumentNotFound",
                $"Process document with ID '{documentId}' was not found in organization '{normalizedOrgId}'.",
                cancellationToken);
        }

        // 3. Locate requested step
        ProcessStep? targetStep = null;
        string chapterOrPhase = string.Empty;

        foreach (var chapter in document.Chapters)
        {
            var match = chapter.Steps.FirstOrDefault(s =>
                string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StepNumber, stepId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                targetStep = match;
                chapterOrPhase = chapter.Title;
                break;
            }
        }

        if (targetStep == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "StepNotFound",
                $"Work step with ID '{stepId}' was not found in document '{documentId}'.",
                cancellationToken);
        }

        // 4. Retrieve or generate Step 2 requirements analysis for GenerateStepLearningAssignment
        List<ProcessTable> relevantTables = [];
        if (targetStep.SourcePages.Count > 0 && document.Tables.Count > 0)
        {
            relevantTables = document.Tables
                .Where(t => targetStep.SourcePages.Contains(t.SourcePage))
                .ToList();
        }

        if (!_stepAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "WorkStepRequirementsAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        WorkStepRequirementsAnalysisResult requirementsAnalysis;
        try
        {
            requirementsAnalysis = await _stepAnalyzer.AnalyzeStepRequirementsAsync(
                documentId,
                document.ProcessName,
                chapterOrPhase,
                targetStep,
                relevantTables,
                previouslyIdentifiedSkills: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze requirements for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AnalysisFailure",
                $"Failed to analyze step requirements: {ex.Message}",
                cancellationToken);
        }

        // 5. Retrieve or generate Step 3 candidate Classes
        CandidateClassSearchResponse candidateSearch;
        try
        {
            candidateSearch = await _candidateFinder.FindCandidateClassesAsync(
                requirementsAnalysis,
                normalizedOrgId,
                language: query["language"],
                maxResults: 5,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search candidate classes for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "CandidateSearchFailure",
                $"Failed to search candidate classes: {ex.Message}",
                cancellationToken);
        }

        // 6. Select relevant Engrams via EngramStudioEngramSelector (Step 4)
        RelevantEngramsResponse step4Result;
        try
        {
            step4Result = await _engramSelector.SelectRelevantEngramsAsync(
                requirementsAnalysis,
                candidateSearch.CandidateClasses,
                normalizedOrgId,
                language: query["language"],
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select relevant Engrams for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "SelectionFailure",
                $"Failed to select relevant Engrams: {ex.Message}",
                cancellationToken);
        }

        // 7. Perform Step 5 Content Coverage Analysis
        if (!_coverageAnalyzer.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "AnalyzerNotConfigured",
                "LearningContentCoverageAnalyzer is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        LearningContentCoverageResponse step5Result;
        try
        {
            step5Result = await _coverageAnalyzer.AnalyzeCoverageAndGapsAsync(
                step4Result,
                requirementsAnalysis,
                normalizedOrgId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze content coverage for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "CoverageAnalysisFailure",
                $"Failed to analyze learning content coverage: {ex.Message}",
                cancellationToken);
        }

        // 8. Perform Step 6 Content Gap Draft Generation
        if (!_draftGenerator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "GeneratorNotConfigured",
                "LearningContentGapDraftGenerator is not configured (missing Azure OpenAI credentials).",
                cancellationToken);
        }

        LearningContentGapDraftResponse step6Result;
        try
        {
            step6Result = await _draftGenerator.GenerateContentGapDraftAsync(
                step5Result,
                requirementsAnalysis,
                normalizedOrgId,
                language: query["language"],
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate content gap drafts for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "DraftGenerationFailure",
                $"Failed to generate content gap drafts: {ex.Message}",
                cancellationToken);
        }

        // 9. Generate Standard Step Learning Assignment (Step 7)
        try
        {
            var assignmentResult = await _assignmentGenerator.GenerateAssignmentAsync(
                requirementsAnalysis,
                step4Result,
                step5Result,
                step6Result,
                normalizedOrgId,
                cancellationToken: cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, assignmentResult, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Step Learning Assignment for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AssignmentGenerationFailure",
                $"Failed to generate Step Learning Assignment: {ex.Message}",
                cancellationToken);
        }
    }

    [Function("CreateEmployeeStepLearningAssignment")]
    public async Task<HttpResponseData> RunCreateEmployeeAssignmentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/steps/{stepId}/employee-assignments")]
        HttpRequestData request,
        string documentId,
        string stepId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate and authorize user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals))
        {
            userId = oidVals.FirstOrDefault();
        }
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            organizationId = tidVals.FirstOrDefault();
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = query["userId"] ?? query["user_id"];
        }
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. User ID and Organization ID must be provided via headers (X-Azure-OID, X-Azure-TID) or query parameters.",
                cancellationToken);
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();

        // 2. Parse request body for employeeId
        string? employeeIdStr = query["employeeId"] ?? query["employee_id"];

        if (request.Body != null && request.Body.CanRead && request.Body.Length > 0)
        {
            try
            {
                var bodyOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var body = await JsonSerializer.DeserializeAsync<CreateEmployeeAssignmentRequest>(request.Body, bodyOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(body?.EmployeeId))
                {
                    employeeIdStr = body.EmployeeId;
                }
            }
            catch (JsonException)
            {
                // Fall back to query param
            }
        }

        if (string.IsNullOrWhiteSpace(employeeIdStr) || !Guid.TryParse(employeeIdStr, out var employeeGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidEmployeeId",
                "A valid employeeId GUID is required in the request body or query.",
                cancellationToken);
        }

        // 3. Create employee assignment via EmployeeStepLearningAssignmentService
        try
        {
            var assignment = await _employeeAssignmentService.CreateEmployeeAssignmentAsync(
                employeeGuid,
                documentId,
                stepId,
                normalizedOrgId,
                language: query["language"],
                cancellationToken: cancellationToken);

            return await FunctionResponseFactory.CreatedResponseAsync(
                request, assignment, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "NotFound",
                ex.Message,
                cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Forbidden,
                "Forbidden",
                ex.Message,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create employee learning assignment for employee {EmployeeId} on step {StepId}", employeeGuid, stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "AssignmentError",
                $"Failed to personalize and assign learning: {ex.Message}",
                cancellationToken);
        }
    }

    [Function("GetEmployeeStepLearningAssignment")]
    public async Task<HttpResponseData> RunGetEmployeeAssignmentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employee-step-learning-assignments/{assignmentId}")]
        HttpRequestData request,
        string assignmentId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var orgId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (string.IsNullOrWhiteSpace(orgId) && request.Headers.TryGetValues("X-Azure-TID", out var tidVals))
        {
            orgId = tidVals.FirstOrDefault();
        }

        var assignment = await _employeeAssignmentService.GetAssignmentByIdAsync(
            assignmentId, orgId, cancellationToken);

        if (assignment is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "AssignmentNotFound",
                $"Employee step learning assignment '{assignmentId}' was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, assignment, cancellationToken: cancellationToken);
    }

    [Function("SaveProcessAnalysis")]
    public async Task<HttpResponseData> RunSaveProcessAnalysisAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/save-analysis")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        // 1. Authenticate user/organization
        string? userId = null;
        string? organizationId = null;

        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (string.IsNullOrWhiteSpace(userId)) userId = query["userId"] ?? query["user_id"];
        if (string.IsNullOrWhiteSpace(organizationId)) organizationId = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];

        if (string.IsNullOrWhiteSpace(organizationId) && string.IsNullOrWhiteSpace(userId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Authentication required. Either organizationId or userId must be provided.",
                cancellationToken);
        }

        var normalizedOrgId = (organizationId ?? string.Empty).Trim().ToLowerInvariant();

        // 2. Read request body
        SaveProcessAnalysisRequest? saveReq = null;
        try
        {
            using var reader = new StreamReader(request.Body);
            var bodyText = await reader.ReadToEndAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                saveReq = JsonSerializer.Deserialize<SaveProcessAnalysisRequest>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SaveProcessAnalysisRequest body");
        }

        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "DbUnavailable",
                "Database context factory is not configured.",
                cancellationToken);
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var explicitOrg = query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
            var tenantGuid = await ResolveTenantGuidAsync(explicitOrg ?? organizationId, userId, db, cancellationToken);
            if (!tenantGuid.HasValue)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "TenantNotFound",
                    $"No active tenant found for organization identifier '{organizationId}'.",
                    cancellationToken);
            }

            var tenantId = tenantGuid.Value;
            var procGuid = Guid.TryParse(documentId, out var parsedDocId) ? parsedDocId : Guid.NewGuid();

            // 3. Resolve structured document from request or storage
            ProcessDocumentStructure? document = saveReq?.Structure;
            if (document == null && !string.IsNullOrWhiteSpace(normalizedOrgId))
            {
                document = await _storageService.GetStructuredResultAsync(normalizedOrgId, documentId, cancellationToken);
            }
            if (document == null)
            {
                document = await _storageService.GetStructuredResultAsync(tenantId.ToString("D").ToLowerInvariant(), documentId, cancellationToken);
            }

            if (document == null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.NotFound,
                    "DocumentNotFound",
                    $"Process document with ID '{documentId}' was not found in storage.",
                    cancellationToken);
            }

            // Execute all mutations inside execution strategy to support SqlServerRetryingExecutionStrategy
            var strategy = db.Database.CreateExecutionStrategy();
            SaveProcessAnalysisResponse? resp = null;

            await strategy.ExecuteAsync(async () =>
            {
                // Delete existing child records directly with SQL commands to avoid EF Core cascade-delete concurrency conflicts
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM dbo.ProcessStepSkill WHERE ProcessStepId IN (SELECT ProcessStepId FROM dbo.ProcessStep WHERE ProcessDocumentId = {0})", procGuid);
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM dbo.ProcessStep WHERE ProcessDocumentId = {0}", procGuid);
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM dbo.ProcessChapter WHERE ProcessDocumentId = {0}", procGuid);

                // Find or create ProcessDocumentRecord (no child includes needed since they were cleared above)
                var record = await db.ProcessDocuments
                    .FirstOrDefaultAsync(p => p.ProcessDocumentId == procGuid, cancellationToken);

                if (record == null)
                {
                    record = new Models.Processes.ProcessDocumentRecord
                    {
                        ProcessDocumentId = procGuid,
                        TenantId = tenantId,
                        CreatedDate = DateTime.UtcNow,
                    };
                    db.ProcessDocuments.Add(record);
                }

                var resolvedTitle = (!string.IsNullOrWhiteSpace(saveReq?.Title) ? saveReq.Title : (!string.IsNullOrWhiteSpace(document.ProcessName) ? document.ProcessName : "Process Document")).Trim();

                // Check title uniqueness across other process documents in the organization
                var titleExists = await db.ProcessDocuments
                    .AnyAsync(p => p.TenantId == tenantId && p.ProcessDocumentId != procGuid && p.Title.ToLower() == resolvedTitle.ToLower(), cancellationToken);
                if (titleExists)
                {
                    throw new InvalidOperationException($"Ya existe un proceso registrado con el nombre '{resolvedTitle}'. El nombre del proceso debe ser único.");
                }

                record.Title = resolvedTitle;
                record.Code = saveReq?.Code ?? record.Code;
                record.Description = saveReq?.Description ?? record.Description;
                record.DepartmentId = saveReq?.DepartmentId ?? record.DepartmentId;
                record.JobRoleId = saveReq?.JobRoleId ?? record.JobRoleId;
                record.AppliesToAllDepartments = saveReq?.AppliesToAllDepartments ?? document.AppliesToAllDepartments;
                record.StorageContainer = tenantId.ToString("D").ToLowerInvariant();
                record.StorageBlobPath = $"process-documents/{documentId}/structured/process-structure.json";
                record.Status = "Analyzed";
                record.UpdatedDate = DateTime.UtcNow;

                // Replace Department/JobRole scope links (delete-then-insert, same pattern as steps/skills above)
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM dbo.ProcessDocumentDepartment WHERE ProcessDocumentId = {0}", procGuid);
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM dbo.ProcessDocumentJobRole WHERE ProcessDocumentId = {0}", procGuid);

                var scopedDepartmentIds = (saveReq?.DepartmentIds ?? document.DepartmentIds ?? []).Distinct().ToList();
                var scopedJobRoleIds = (saveReq?.JobRoleIds ?? document.JobRoleIds ?? []).Distinct().ToList();

                foreach (var deptId in scopedDepartmentIds)
                {
                    db.ProcessDocumentDepartments.Add(new Models.Processes.ProcessDocumentDepartment
                    {
                        ProcessDocumentId = procGuid,
                        DepartmentId = deptId,
                    });
                }

                foreach (var roleId in scopedJobRoleIds)
                {
                    db.ProcessDocumentJobRoles.Add(new Models.Processes.ProcessDocumentJobRole
                    {
                        ProcessDocumentId = procGuid,
                        JobRoleId = roleId,
                    });
                }

                // Load existing tenant skills to resolve foreign keys and coverage status
                var tenantSkills = await db.Skills
                    .AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.IsActive)
                    .ToListAsync(cancellationToken);
                var tenantSkillMap = new Dictionary<string, Models.OrgStructure.Skill>(StringComparer.OrdinalIgnoreCase);
                foreach (var sk in tenantSkills)
                {
                    if (!string.IsNullOrWhiteSpace(sk.Name))
                    {
                        tenantSkillMap[sk.Name.Trim()] = sk;
                    }
                }

                // Build consolidated full text from all chapters and steps
                var fullTextBuilder = new System.Text.StringBuilder();
                fullTextBuilder.AppendLine($"PROCESS: {record.Title}");
                if (!string.IsNullOrWhiteSpace(record.Description)) fullTextBuilder.AppendLine(record.Description);

                var newChapters = new List<Models.Processes.ProcessChapterRecord>();
                var newSteps = new List<Models.Processes.ProcessStepRecord>();
                int order = 1;
                var uniqueSkillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int chIdx = 0; chIdx < document.Chapters.Count; chIdx++)
                {
                    var ch = document.Chapters[chIdx];
                    var chRecord = new Models.Processes.ProcessChapterRecord
                    {
                        ProcessChapterId = Guid.NewGuid(),
                        ProcessDocumentId = procGuid,
                        ChapterNumber = chIdx + 1,
                        Title = !string.IsNullOrWhiteSpace(ch.Title) ? ch.Title : $"Phase {chIdx + 1}",
                        Description = ch.Description
                    };
                    newChapters.Add(chRecord);

                    fullTextBuilder.AppendLine($"\nCHAPTER {chRecord.ChapterNumber}: {chRecord.Title}");

                    foreach (var st in ch.Steps)
                    {
                        List<string>? obsAbilities = null;
                        List<string>? reqTools = null;
                        WorkStepRequirementsAnalysisResult? reqAnalysis = null;
                        if (saveReq?.StepRequirements != null && saveReq.StepRequirements.TryGetValue(st.StepId, out reqAnalysis))
                        {
                            obsAbilities = reqAnalysis.EmployeeMustBeAbleTo;
                            reqTools = reqAnalysis.RequiredTools?.Select(t => t.Name).ToList();
                        }

                        var stRecord = new Models.Processes.ProcessStepRecord
                        {
                            ProcessStepId = Guid.NewGuid(),
                            ProcessDocumentId = procGuid,
                            ProcessChapterId = chRecord.ProcessChapterId,
                            StepNumber = !string.IsNullOrWhiteSpace(st.StepNumber) ? st.StepNumber : order.ToString(),
                            OrderIndex = order++,
                            Name = st.StepName,
                            Objective = st.Objective,
                            ActivitiesJson = st.RequiredActivities.Count > 0 ? JsonSerializer.Serialize(st.RequiredActivities) : null,
                            ObservableAbilitiesJson = obsAbilities != null && obsAbilities.Count > 0 ? JsonSerializer.Serialize(obsAbilities) : null,
                            RequiredToolsJson = reqTools != null && reqTools.Count > 0 ? JsonSerializer.Serialize(reqTools) : null,
                            DeliverablesJson = st.Deliverables.Count > 0 ? JsonSerializer.Serialize(st.Deliverables) : null,
                            CompletionCondition = st.CompletionCondition,
                            SourcePagesJson = st.SourcePages.Count > 0 ? JsonSerializer.Serialize(st.SourcePages) : null,
                            SourceContent = !string.IsNullOrWhiteSpace(st.SourceContent) ? st.SourceContent : null,
                            StepImagesJson = st.StepImages.Count > 0 ? JsonSerializer.Serialize(st.StepImages) : null,
                            IsExcluded = false
                        };

                        fullTextBuilder.AppendLine($"  STEP {stRecord.StepNumber}: {stRecord.Name}");
                        if (!string.IsNullOrWhiteSpace(stRecord.Objective)) fullTextBuilder.AppendLine($"    Objective: {stRecord.Objective}");

                        // Attach Step Skills from coverage analysis if provided, or directly from StepRequirements
                        if (saveReq?.StepCoverages != null && saveReq.StepCoverages.TryGetValue(st.StepId, out var coverage) && coverage != null)
                        {
                            foreach (var cov in coverage.CoveredRequirements)
                            {
                                uniqueSkillNames.Add(cov.Requirement);
                                tenantSkillMap.TryGetValue(cov.Requirement.Trim(), out var matchedSkill);
                                stRecord.Skills.Add(new Models.Processes.ProcessStepSkillRecord
                                {
                                    ProcessStepSkillId = Guid.NewGuid(),
                                    ProcessStepId = stRecord.ProcessStepId,
                                    SkillId = matchedSkill?.SkillId,
                                    SkillName = cov.Requirement,
                                    Category = cov.Category ?? "job-skills",
                                    IsAiSkill = cov.IsAiSkill,
                                    Criticality = cov.Criticality ?? "critical",
                                    CoverageStatus = "covered",
                                    MatchedCourseName = cov.MatchedCapabilityName
                                });
                            }

                            foreach (var gap in coverage.ContentGaps)
                            {
                                uniqueSkillNames.Add(gap.Requirement);
                                tenantSkillMap.TryGetValue(gap.Requirement.Trim(), out var matchedSkill);
                                stRecord.Skills.Add(new Models.Processes.ProcessStepSkillRecord
                                {
                                    ProcessStepSkillId = Guid.NewGuid(),
                                    ProcessStepId = stRecord.ProcessStepId,
                                    SkillId = matchedSkill?.SkillId,
                                    SkillName = gap.Requirement,
                                    Category = gap.Category ?? "job-skills",
                                    IsAiSkill = gap.IsAiSkill,
                                    Criticality = gap.Criticality ?? "critical",
                                    Reason = gap.GapReason,
                                    CoverageStatus = gap.IsInCatalog ? "in_catalog_pending_course" : "new_skill_required",
                                    MatchedCourseName = gap.TargetClassName
                                });
                            }
                        }
                        else if (reqAnalysis?.RequiredSkillAreas != null)
                        {
                            void AddSkills(IEnumerable<SkillItem>? list, string category, bool isAi)
                            {
                                if (list == null) return;
                                foreach (var sk in list)
                                {
                                    if (string.IsNullOrWhiteSpace(sk.Name)) continue;
                                    uniqueSkillNames.Add(sk.Name);
                                    var trimmed = sk.Name.Trim();
                                    var inCatalog = tenantSkillMap.TryGetValue(trimmed, out var matchedSkill);

                                    stRecord.Skills.Add(new Models.Processes.ProcessStepSkillRecord
                                    {
                                        ProcessStepSkillId = Guid.NewGuid(),
                                        ProcessStepId = stRecord.ProcessStepId,
                                        SkillId = matchedSkill?.SkillId,
                                        SkillName = trimmed,
                                        Category = category,
                                        IsAiSkill = isAi,
                                        Criticality = "critical",
                                        Reason = sk.Reason,
                                        CoverageStatus = inCatalog ? "in_catalog_pending_course" : "new_skill_required"
                                    });
                                }
                            }

                            AddSkills(reqAnalysis.RequiredSkillAreas.SoftSkills, "soft-skills", false);
                            AddSkills(reqAnalysis.RequiredSkillAreas.TechnicalSkills, "job-skills", false);
                            AddSkills(reqAnalysis.RequiredSkillAreas.BusinessSkills, "job-skills", false);
                            AddSkills(reqAnalysis.RequiredSkillAreas.DepartmentProcesses, "department-processes", false);
                            AddSkills(reqAnalysis.RequiredSkillAreas.HrProcesses, "hr-processes", false);
                            AddSkills(reqAnalysis.RequiredSkillAreas.AiSkills, "job-skills", true);
                        }

                        newSteps.Add(stRecord);
                    }
                }

                record.FullText = fullTextBuilder.ToString();
                record.TotalStepsCount = newSteps.Count;
                record.UniqueSkillsCount = uniqueSkillNames.Count;

                await db.ProcessChapters.AddRangeAsync(newChapters, cancellationToken);
                await db.ProcessSteps.AddRangeAsync(newSteps, cancellationToken);

                await db.SaveChangesAsync(cancellationToken);

                // Generate and upload the "enriched" PDF (index + extracted content + matched images)
                // that Studio's course-generation pipeline will consume instead of the original source PDF.
                try
                {
                    var enrichedBytes = Services.ProcessDocuments.EnrichedProcessPdfGenerator.Generate(record.Title, record.Description, document);
                    using var enrichedStream = new MemoryStream(enrichedBytes);
                    var enrichedFileName = $"{record.Title}.pdf";
                    var enrichedPath = await _storageService.UploadEnrichedPdfAsync(
                        record.StorageContainer, documentId, enrichedFileName, enrichedStream, cancellationToken);
                    record.EnrichedPdfStoragePath = enrichedPath;
                    record.EnrichedPdfFileName = enrichedFileName;
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception enrichEx)
                {
                    // Non-fatal: the structured save already succeeded; Studio will fall back to the
                    // original source PDF if the enriched PDF could not be generated/uploaded.
                    _logger.LogWarning(enrichEx, "Failed to generate/upload enriched PDF for document {DocumentId}", documentId);
                }

                resp = new SaveProcessAnalysisResponse
                {
                    Success = true,
                    ProcessDocumentId = procGuid,
                    Title = record.Title,
                    TotalSteps = record.TotalStepsCount,
                    TotalSkills = record.UniqueSkillsCount,
                    StorageContainer = record.StorageContainer,
                    StorageBlobPath = record.StorageBlobPath,
                    Message = "Process document and Skills Training Matrix successfully saved to database."
                };
            });

            return await FunctionResponseFactory.SuccessResponseAsync(request, resp, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save process analysis for document {DocumentId}", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "SaveProcessAnalysisFailure",
                $"Error saving process analysis to database: {ex.Message}",
                cancellationToken);
        }
    }

    /// <summary>Saves (or updates, if <c>processSkillRequirementId</c> is supplied via query string) a Step 4
    /// skill gap as a formal, scoped training requirement for this process: which Job Role(s)/Department(s)/the
    /// whole company must have it, and whether it is required or just recommended.</summary>
    [Function("SaveProcessSkillRequirement")]
    public async Task<HttpResponseData> RunSaveProcessSkillRequirementAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/skill-requirements")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "DbUnavailable",
                "Database context factory is not configured.", cancellationToken);
        }

        if (!Guid.TryParse(documentId, out var procGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDocumentId",
                $"'{documentId}' is not a valid process document ID.", cancellationToken);
        }

        SaveProcessSkillRequirementRequest? reqBody;
        try
        {
            using var reader = new StreamReader(request.Body);
            var bodyText = await reader.ReadToEndAsync(cancellationToken);
            reqBody = JsonSerializer.Deserialize<SaveProcessSkillRequirementRequest>(
                bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SaveProcessSkillRequirementRequest body");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "Request body could not be parsed.", cancellationToken);
        }

        if (reqBody == null || string.IsNullOrWhiteSpace(reqBody.SkillName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "SkillNameRequired", "skillName is required.", cancellationToken);
        }

        if (reqBody.IsNewFromInternet && !string.IsNullOrWhiteSpace(reqBody.MatchedSkillId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidSkillSource",
                "A skill requirement cannot be both 'isNewFromInternet' and reference an existing 'matchedSkillId'.", cancellationToken);
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            var existingId = Guid.TryParse(query["processSkillRequirementId"], out var parsedReqId) ? parsedReqId : (Guid?)null;

            // Resolve the matched dbo.Skill: case (a) reuse the catalog skill as-is (nothing created);
            // case (b) create a brand-new Skill row from the real internet course, source + outline preserved.
            Guid? matchedSkillId = Guid.TryParse(reqBody.MatchedSkillId, out var parsedSkillId) ? parsedSkillId : null;

            if (reqBody.IsNewFromInternet)
            {
                string? newSkillUserId = null;
                string? newSkillOrgId = null;
                if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) newSkillUserId = oidVals.FirstOrDefault();
                if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) newSkillOrgId = tidVals.FirstOrDefault();
                newSkillOrgId ??= query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
                newSkillUserId ??= query["userId"] ?? query["user_id"];

                var tenantGuid = await ResolveTenantGuidAsync(newSkillOrgId, newSkillUserId, db, cancellationToken);
                if (!tenantGuid.HasValue)
                {
                    return await FunctionResponseFactory.ErrorResponseAsync(
                        request, HttpStatusCode.Unauthorized, "TenantRequired",
                        "Could not resolve the tenant for this new internet-sourced skill. Provide X-Azure-OID/X-Azure-TID headers or organizationId.", cancellationToken);
                }

                var newSkill = new Models.OrgStructure.Skill
                {
                    SkillId = Guid.NewGuid(),
                    TenantId = tenantGuid.Value,
                    Category = reqBody.Category,
                    Name = reqBody.SkillName.Trim(),
                    Description = reqBody.Reason,
                    IsActive = true,
                    SourceCourseTitle = reqBody.SourceCourseTitle,
                    SourceCourseProvider = reqBody.SourceCourseProvider,
                    SourceCourseUrl = reqBody.SourceCourseUrl,
                    SourceOutlineJson = reqBody.SourceOutlineModules is { Count: > 0 }
                        ? JsonSerializer.Serialize(reqBody.SourceOutlineModules)
                        : null,
                    SourceOutlineSource = reqBody.SourceOutlineSource,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                };

                db.Skills.Add(newSkill);
                await db.SaveChangesAsync(cancellationToken);
                matchedSkillId = newSkill.SkillId;
            }
            else if (matchedSkillId.HasValue)
            {
                var catalogSkillExists = await db.Skills.AnyAsync(s => s.SkillId == matchedSkillId.Value, cancellationToken);
                if (!catalogSkillExists)
                {
                    return await FunctionResponseFactory.ErrorResponseAsync(
                        request, HttpStatusCode.BadRequest, "InvalidMatchedSkillId",
                        $"matchedSkillId '{matchedSkillId}' does not reference an existing Skill.", cancellationToken);
                }
            }

            if (!await db.ProcessDocuments.AnyAsync(p => p.ProcessDocumentId == procGuid, cancellationToken))
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "DocumentNotFound",
                    $"Process document '{documentId}' was not found.", cancellationToken);
            }

            var departmentIds = reqBody.DepartmentIds
                .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
            var jobRoleIds = reqBody.JobRoleIds
                .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();

            Models.Processes.ProcessSkillRequirement? record = existingId.HasValue
                ? await db.ProcessSkillRequirements
                    .Include(r => r.ScopedDepartments)
                    .Include(r => r.ScopedJobRoles)
                    .FirstOrDefaultAsync(r => r.ProcessSkillRequirementId == existingId.Value && r.ProcessDocumentId == procGuid, cancellationToken)
                : null;

            var isNew = record == null;
            record ??= new Models.Processes.ProcessSkillRequirement
            {
                ProcessSkillRequirementId = Guid.NewGuid(),
                ProcessDocumentId = procGuid,
                CreatedDate = DateTime.UtcNow,
            };

            record.SkillName = reqBody.SkillName.Trim();
            record.Category = reqBody.Category;
            record.IsAiSkill = reqBody.IsAiSkill;
            record.Reason = reqBody.Reason;
            record.AppliesToAllDepartments = reqBody.AppliesToAllDepartments;
            record.IsRequired = reqBody.IsRequired;
            record.MatchedSkillId = matchedSkillId;
            record.UpdatedDate = DateTime.UtcNow;

            if (isNew)
            {
                db.ProcessSkillRequirements.Add(record);
            }
            else
            {
                record.ScopedDepartments.Clear();
                record.ScopedJobRoles.Clear();
            }

            if (!reqBody.AppliesToAllDepartments)
            {
                foreach (var deptId in departmentIds)
                {
                    record.ScopedDepartments.Add(new Models.Processes.ProcessSkillRequirementDepartment
                    {
                        ProcessSkillRequirementId = record.ProcessSkillRequirementId,
                        DepartmentId = deptId,
                    });
                }

                foreach (var roleId in jobRoleIds)
                {
                    record.ScopedJobRoles.Add(new Models.Processes.ProcessSkillRequirementJobRole
                    {
                        ProcessSkillRequirementId = record.ProcessSkillRequirementId,
                        JobRoleId = roleId,
                    });
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            var response = new ProcessSkillRequirementResponse
            {
                ProcessSkillRequirementId = record.ProcessSkillRequirementId,
                ProcessDocumentId = record.ProcessDocumentId,
                SkillName = record.SkillName,
                Category = record.Category,
                IsAiSkill = record.IsAiSkill,
                Reason = record.Reason,
                AppliesToAllDepartments = record.AppliesToAllDepartments,
                IsRequired = record.IsRequired,
                DepartmentIds = record.ScopedDepartments.Select(d => d.DepartmentId.ToString()).ToList(),
                JobRoleIds = record.ScopedJobRoles.Select(r => r.JobRoleId.ToString()).ToList(),
                MatchedSkillId = record.MatchedSkillId?.ToString(),
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save process skill requirement for document {DocumentId}", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "SaveProcessSkillRequirementFailure",
                $"Error saving skill requirement: {ex.Message}", cancellationToken);
        }
    }

    /// <summary>Saves the WHOLE process (not an individual skill gap) as a "department-processes"
    /// Capability Catalog Skill — so HR can later open it in Studio and generate its course, exactly
    /// like any other capability. Reuses the process's already-uploaded original PDF and its
    /// chapter/step outline as the Skill's source document + course outline. Re-calling this for the
    /// same process updates the existing catalog entry instead of creating a duplicate.</summary>
    [Function("SaveProcessAsCapability")]
    public async Task<HttpResponseData> RunSaveProcessAsCapabilityAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "process-documents/{documentId}/save-as-capability")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "DbUnavailable",
                "Database context factory is not configured.", cancellationToken);
        }

        if (!Guid.TryParse(documentId, out var procGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDocumentId",
                $"'{documentId}' is not a valid process document ID.", cancellationToken);
        }

        SaveProcessAsCapabilityRequest? reqBody;
        try
        {
            using var reader = new StreamReader(request.Body);
            var bodyText = await reader.ReadToEndAsync(cancellationToken);
            reqBody = JsonSerializer.Deserialize<SaveProcessAsCapabilityRequest>(
                bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SaveProcessAsCapabilityRequest body");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "Request body could not be parsed.", cancellationToken);
        }

        if (reqBody == null || !Guid.TryParse(reqBody.DepartmentId, out var departmentGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "DepartmentRequired",
                "departmentId is required and must be a valid GUID — it identifies who authored this process (e.g. HR, Finance).", cancellationToken);
        }

        string? userId = null;
        string? organizationId = null;
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidVals)) userId = oidVals.FirstOrDefault();
        if (request.Headers.TryGetValues("X-Azure-TID", out var tidVals)) organizationId = tidVals.FirstOrDefault();
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        organizationId ??= query["organizationId"] ?? query["organization_id"] ?? query["tenantId"] ?? query["tenant_id"];
        userId ??= query["userId"] ?? query["user_id"];

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var tenantGuid = await ResolveTenantGuidAsync(organizationId, userId, db, cancellationToken);
            if (!tenantGuid.HasValue)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Unauthorized, "TenantRequired", "Could not resolve the tenant.", cancellationToken);
            }

            var record = await db.ProcessDocuments
                .Include(p => p.Chapters)
                .Include(p => p.Steps.OrderBy(s => s.OrderIndex))
                .FirstOrDefaultAsync(p => p.ProcessDocumentId == procGuid && p.TenantId == tenantGuid.Value, cancellationToken);

            // The wizard lets the user "Save as Capability" BEFORE the final "Approve & Save Process
            // to Database" step persists the ProcessDocuments SQL row (that only happens via
            // SaveProcessAnalysis, called right after this succeeds). At that point the structured
            // index/PDF already exist in blob storage even though no SQL row exists yet — so create a
            // minimal row here instead of 404ing; SaveProcessAnalysis will fill in chapters/steps
            // normally afterwards, exactly as if the row had existed all along.
            ProcessDocumentStructure? storageDocForNewRecord = null;
            if (record == null)
            {
                storageDocForNewRecord = await GetStructuredDocumentWithFallbackAsync(tenantGuid.Value.ToString("D"), documentId, cancellationToken);
                if (storageDocForNewRecord == null)
                {
                    return await FunctionResponseFactory.ErrorResponseAsync(
                        request, HttpStatusCode.NotFound, "DocumentNotFound",
                        $"Process document '{documentId}' was not found.", cancellationToken);
                }

                record = new Models.Processes.ProcessDocumentRecord
                {
                    ProcessDocumentId = procGuid,
                    TenantId = tenantGuid.Value,
                    Title = (!string.IsNullOrWhiteSpace(storageDocForNewRecord.ProcessName) ? storageDocForNewRecord.ProcessName : "Process Document").Trim(),
                    Status = "Structured",
                    CreatedDate = DateTime.UtcNow,
                };
                db.ProcessDocuments.Add(record);

                // Persist the new ProcessDocumentRecord by itself first. Otherwise EF Core detects a
                // circular FK dependency once the new Skill (added further below) references this
                // record via SourceProcessDocumentId while this record references the Skill back via
                // CapabilitySkillId — both rows being newly-added makes the insert order ambiguous.
                await db.SaveChangesAsync(cancellationToken);
            }

            var departmentExists = await db.Departments.AnyAsync(
                d => d.DepartmentId == departmentGuid && d.TenantId == tenantGuid.Value, cancellationToken);
            if (!departmentExists)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidDepartmentId",
                    $"departmentId '{departmentGuid}' does not reference an existing Department for this tenant.", cancellationToken);
            }

            // Persists who authored this process — distinct from ScopedDepartments/
            // AppliesToAllDepartments, which track who must FOLLOW it, not who wrote it.
            record.DepartmentId = departmentGuid;

            // Reuse the real, already-extracted source PDF + chapter/step outline instead of
            // re-scraping or re-uploading anything.
            var storageDoc = storageDocForNewRecord ?? await GetStructuredDocumentWithFallbackAsync(tenantGuid.Value.ToString("D"), documentId, cancellationToken);
            var outlineModules = record.Chapters
                .OrderBy(c => c.ChapterNumber)
                .Select(c => new CourseOutlineModule
                {
                    Title = $"{c.ChapterNumber}. {c.Title}",
                    Lessons = record.Steps
                        .Where(s => s.ProcessChapterId == c.ProcessChapterId)
                        .OrderBy(s => s.OrderIndex)
                        .Select(s => s.Name)
                        .ToList(),
                })
                .ToList();

            var skill = record.CapabilitySkillId.HasValue
                ? await db.Skills.FirstOrDefaultAsync(s => s.SkillId == record.CapabilitySkillId.Value, cancellationToken)
                : null;
            var alreadyExisted = skill != null;

            skill ??= new Models.OrgStructure.Skill
            {
                SkillId = Guid.NewGuid(),
                TenantId = tenantGuid.Value,
                CreatedDate = DateTime.UtcNow,
            };

            skill.Category = "department-processes";
            skill.Name = record.Title;
            skill.Description = record.Description;
            skill.AssignmentScope = "DEPARTMENT_ESSENTIAL";
            skill.IsActive = true;
            // SourceFile.StoragePath comes back as "{containerName}/{blobPath}" (see
            // ProcessDocumentStorageService.UploadSourcePdfAsync), but SkillDocumentStorageService
            // (used by GetSkillDocumentFunction) resolves the container from TenantId itself and
            // expects a container-relative path — strip the leading "{containerName}/" so the
            // existing "Generar curso"/document download flow can find the blob.
            // Prefer the "enriched" PDF (index + extracted content + matched images, generated at
            // save-analysis time) over the original source PDF, so Studio's course-generation
            // pipeline gets pre-structured, correctly-illustrated content for every Engram.
            var rawStoragePath = record.EnrichedPdfStoragePath ?? storageDoc?.SourceFile?.StoragePath ?? record.StorageBlobPath;
            var containerPrefix = tenantGuid.Value.ToString("D").ToLowerInvariant() + "/";
            skill.DocumentStoragePath = rawStoragePath != null && rawStoragePath.ToLowerInvariant().StartsWith(containerPrefix)
                ? rawStoragePath[containerPrefix.Length..]
                : rawStoragePath;
            skill.DocumentFileName = record.EnrichedPdfFileName ?? storageDoc?.SourceFile?.FileName ?? $"{record.Title}.pdf";
            skill.SourceOutlineJson = outlineModules.Count > 0 ? JsonSerializer.Serialize(outlineModules) : null;
            skill.SourceOutlineSource = "scraped";
            skill.SourceProcessDocumentId = procGuid;
            skill.UpdatedDate = DateTime.UtcNow;

            if (!alreadyExisted)
            {
                db.Skills.Add(skill);
            }

            record.CapabilitySkillId = skill.SkillId;

            await db.SaveChangesAsync(cancellationToken);

            var response = new SaveProcessAsCapabilityResponse { SkillId = skill.SkillId, AlreadyExisted = alreadyExisted };
            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save process {DocumentId} as a capability", documentId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "SaveProcessAsCapabilityFailure",
                $"Error saving process as capability: {ex.Message}", cancellationToken);
        }
    }

    /// <summary>Lists the saved, scoped skill training requirements for a process (Step 4 "Save" results).</summary>
    [Function("GetProcessSkillRequirements")]
    public async Task<HttpResponseData> RunGetProcessSkillRequirementsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "process-documents/{documentId}/skill-requirements")]
        HttpRequestData request,
        string documentId,
        CancellationToken cancellationToken)
    {
        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "DbUnavailable",
                "Database context factory is not configured.", cancellationToken);
        }

        if (!Guid.TryParse(documentId, out var procGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDocumentId",
                $"'{documentId}' is not a valid process document ID.", cancellationToken);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var records = await db.ProcessSkillRequirements
            .AsNoTracking()
            .Include(r => r.ScopedDepartments)
            .Include(r => r.ScopedJobRoles)
            .Where(r => r.ProcessDocumentId == procGuid)
            .OrderBy(r => r.SkillName)
            .Select(r => new ProcessSkillRequirementResponse
            {
                ProcessSkillRequirementId = r.ProcessSkillRequirementId,
                ProcessDocumentId = r.ProcessDocumentId,
                SkillName = r.SkillName,
                Category = r.Category,
                IsAiSkill = r.IsAiSkill,
                Reason = r.Reason,
                AppliesToAllDepartments = r.AppliesToAllDepartments,
                IsRequired = r.IsRequired,
                DepartmentIds = r.ScopedDepartments.Select(d => d.DepartmentId.ToString()).ToList(),
                JobRoleIds = r.ScopedJobRoles.Select(jr => jr.JobRoleId.ToString()).ToList(),
                MatchedSkillId = r.MatchedSkillId != null ? r.MatchedSkillId.ToString() : null,
            })
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, records, cancellationToken: cancellationToken);
    }

    /// <summary>Removes a previously saved scoped skill training requirement.</summary>
    [Function("DeleteProcessSkillRequirement")]
    public async Task<HttpResponseData> RunDeleteProcessSkillRequirementAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "process-documents/{documentId}/skill-requirements/{requirementId}")]
        HttpRequestData request,
        string documentId,
        string requirementId,
        CancellationToken cancellationToken)
    {
        if (_dbContextFactory == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "DbUnavailable",
                "Database context factory is not configured.", cancellationToken);
        }

        if (!Guid.TryParse(documentId, out var procGuid) || !Guid.TryParse(requirementId, out var reqGuid))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidId", "Invalid document or requirement ID.", cancellationToken);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await db.ProcessSkillRequirements
            .FirstOrDefaultAsync(r => r.ProcessSkillRequirementId == reqGuid && r.ProcessDocumentId == procGuid, cancellationToken);

        if (record == null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "RequirementNotFound", "Skill requirement not found.", cancellationToken);
        }

        db.ProcessSkillRequirements.Remove(record);
        await db.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { success = true }, cancellationToken: cancellationToken);
    }

    /// <summary>Converts the AI's raw labeled items for one classification category into CodedListItem entries
    /// with stable IDs in the form [ProcessCode]-[StepCode]-[CategoryPrefix][NN], assigned here (never by the
    /// AI) so IDs never change across re-analysis.</summary>
    private static List<CodedListItem> BuildCodedList(
        List<ClassifiedItemDto> items,
        string processCode,
        string stepCode,
        string categoryPrefix)
    {
        var source = items is { Count: > 0 } ? items : [new ClassifiedItemDto { Label = "None identified in the source" }];
        return source
            .Select((item, idx) => new CodedListItem
            {
                Id = $"{processCode}-{stepCode}-{categoryPrefix}{idx + 1:D2}",
                Label = item.Label
            })
            .ToList();
    }

    private static async Task<Guid?> ResolveTenantGuidAsync(
        string? organizationId,
        string? userId,
        HumanOsDbContext db,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(organizationId, out var parsedGuid))
        {
            if (await db.Tenants.AnyAsync(t => t.TenantId == parsedGuid, cancellationToken))
            {
                return parsedGuid;
            }
        }

        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            var tenant = await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.AzureTenantId == organizationId, cancellationToken);
            if (tenant != null) return tenant.TenantId;
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var person = await db.People
                .AsNoTracking()
                .FirstOrDefaultAsync(p => (p.AzureOid == userId || p.AzureTid == organizationId) && p.TenantId != null, cancellationToken);
            if (person?.TenantId != null) return person.TenantId.Value;
        }

        return null;
    }

    /// <summary>
    /// Resolves the CANONICAL storage org key for a request's organization id, using the exact same
    /// DB-backed resolution (<see cref="ResolveTenantGuidAsync"/>) that <c>GetProcessDocumentDetail</c>
    /// uses to read a document. Callers that both READ and WRITE the structured document (e.g. to
    /// persist the process-wide learning recommendation) must resolve this key FIRST and use it for
    /// both operations — attempting a direct storage lookup by the raw org id header first (as a
    /// previous implementation did) can find a stale/orphaned copy saved under the wrong key and keep
    /// perpetuating the mismatch, since detail reads always resolve via the DB Tenant/Person tables.
    /// </summary>
    private async Task<string> ResolveCanonicalStorageOrgIdAsync(
        string organizationId,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (_dbContextFactory != null)
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var tenantGuid = await ResolveTenantGuidAsync(organizationId, userId, db, cancellationToken);
                if (tenantGuid.HasValue)
                {
                    return tenantGuid.Value.ToString("D").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ResolveCanonicalStorageOrgIdAsync: failed to resolve canonical tenant for orgId {OrgId}", organizationId);
            }
        }

        return organizationId.ToLowerInvariant();
    }

    private async Task<ProcessDocumentStructure?> GetStructuredDocumentWithFallbackAsync(
        string organizationId,
        string documentId,
        CancellationToken cancellationToken)
    {
        var normalized = organizationId.ToLowerInvariant();
        var structured = await _storageService.GetStructuredResultAsync(normalized, documentId, cancellationToken);
        if (structured != null) return structured;

        if (_dbContextFactory != null)
        {
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                Guid? parsedOrgGuid = Guid.TryParse(organizationId, out var parsedG) ? parsedG : null;
                var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t =>
                    (parsedOrgGuid.HasValue && t.TenantId == parsedOrgGuid.Value) || t.AzureTenantId == organizationId, cancellationToken);

                if (tenant != null)
                {
                    if (parsedOrgGuid.HasValue && parsedOrgGuid.Value == tenant.TenantId && !string.IsNullOrWhiteSpace(tenant.AzureTenantId))
                    {
                        var altKey = tenant.AzureTenantId.ToLowerInvariant();
                        structured = await _storageService.GetStructuredResultAsync(altKey, documentId, cancellationToken);
                        if (structured != null) return structured;
                    }
                    else if (!string.IsNullOrWhiteSpace(tenant.AzureTenantId) && string.Equals(tenant.AzureTenantId, organizationId, StringComparison.OrdinalIgnoreCase))
                    {
                        var altKey = tenant.TenantId.ToString("D").ToLowerInvariant();
                        structured = await _storageService.GetStructuredResultAsync(altKey, documentId, cancellationToken);
                        if (structured != null) return structured;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed fallback lookup in GetStructuredDocumentWithFallbackAsync for orgId {OrgId}", organizationId);
            }
        }

        return structured;
    }
}
