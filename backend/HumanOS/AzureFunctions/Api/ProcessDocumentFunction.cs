using System.Net;
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
                stepCoverages = stepCoverages
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
                record.StorageContainer = tenantId.ToString("D").ToLowerInvariant();
                record.StorageBlobPath = $"process-documents/{documentId}/structured/process-structure.json";
                record.Status = "Analyzed";
                record.UpdatedDate = DateTime.UtcNow;

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

    private async Task<ProcessDocumentStructure?> GetStructuredDocumentWithFallbackAsync(
        string organizationId,
        string documentId,
        CancellationToken cancellationToken)
    {
        var structured = await _storageService.GetStructuredResultAsync(organizationId.ToLowerInvariant(), documentId, cancellationToken);
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
                        structured = await _storageService.GetStructuredResultAsync(tenant.AzureTenantId.ToLowerInvariant(), documentId, cancellationToken);
                    }
                    else if (!string.IsNullOrWhiteSpace(tenant.AzureTenantId) && string.Equals(tenant.AzureTenantId, organizationId, StringComparison.OrdinalIgnoreCase))
                    {
                        structured = await _storageService.GetStructuredResultAsync(tenant.TenantId.ToString("D").ToLowerInvariant(), documentId, cancellationToken);
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
