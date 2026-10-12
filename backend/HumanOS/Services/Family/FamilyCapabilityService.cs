using System.Data;
using System.Text.Json;
using HumanOS.Contracts.Family;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services.Family;

/// <summary>
/// Service managing the Family Global and Custom Capabilities and AI-generated course content.
/// Operates on the isolated family.* schema (100% additive, 0 impact on existing corporate tables).
/// </summary>
public sealed class FamilyCapabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string? _connectionString;
    private readonly ILogger<FamilyCapabilityService> _logger;

    // In-memory fallback repository for local resilience and rapid testing
    private static readonly List<FamilyCapabilityResponse> InMemoryCapabilities = [];
    private static readonly List<SaveCapabilityLearningContentRequest> InMemoryContents = [];
    private static readonly Dictionary<string, SaveAcademicCourseDocumentRequest> InMemoryAcademicCourses = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object SyncLock = new();

    public FamilyCapabilityService(IConfiguration configuration, ILogger<FamilyCapabilityService> logger)
    {
        _connectionString = configuration.GetConnectionString("FamilyDatabase") 
                            ?? configuration["FamilyDatabase"]
                            ?? configuration.GetConnectionString("HumanOSDatabase")
                            ?? configuration["HumanOSDatabase"];
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all capabilities for a given stage (e.g. 'kindergarten'),
    /// combining global reference capabilities (FamilyId IS NULL) and custom ones for the family.
    /// </summary>
    public async Task<List<FamilyCapabilityResponse>> GetStageCapabilitiesAsync(
        string stageKey,
        Guid? familyId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FamilyCapabilityResponse>();

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT 
                        c.CapabilityId,
                        c.Code,
                        s.StageKey,
                        c.NameEs,
                        c.NameEn,
                        c.Domain,
                        c.DescriptionEs,
                        c.DescriptionEn,
                        c.WhyItMattersEs,
                        c.WhyItMattersEn,
                        r.TargetMasteryLevel,
                        r.RecommendedHoursToMaster,
                        c.SkillsCoveredJson,
                        c.FamilyId,
                        c.CreatedDate
                    FROM family.HumanCapabilities c
                    INNER JOIN family.StageCapabilityRequirements r ON c.CapabilityId = r.CapabilityId
                    INNER JOIN family.HumanDevelopmentStages s ON r.StageId = s.StageId
                    WHERE s.StageKey = @StageKey
                      AND (c.FamilyId IS NULL OR c.FamilyId = @FamilyId)
                    ORDER BY c.Code ASC;";

                await using var command = new SqlCommand(sql, connection);
                command.Parameters.Add(new SqlParameter("@StageKey", SqlDbType.NVarChar, 50) { Value = stageKey });
                command.Parameters.Add(new SqlParameter("@FamilyId", SqlDbType.UniqueIdentifier) { Value = (object?)familyId ?? DBNull.Value });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var skillsJson = reader.IsDBNull(12) ? null : reader.GetString(12);
                    var skillsList = !string.IsNullOrWhiteSpace(skillsJson)
                        ? JsonSerializer.Deserialize<List<string>>(skillsJson, JsonOptions) ?? []
                        : [];

                    results.Add(new FamilyCapabilityResponse
                    {
                        CapabilityId = reader.GetGuid(0),
                        Code = reader.GetString(1),
                        StageKey = reader.GetString(2),
                        NameEs = reader.GetString(3),
                        NameEn = reader.GetString(4),
                        Domain = reader.GetString(5),
                        DescriptionEs = reader.GetString(6),
                        DescriptionEn = reader.GetString(7),
                        WhyItMattersEs = reader.IsDBNull(8) ? null : reader.GetString(8),
                        WhyItMattersEn = reader.IsDBNull(9) ? null : reader.GetString(9),
                        TargetMasteryLevel = reader.GetString(10),
                        RecommendedHoursToMaster = reader.GetInt32(11),
                        SkillsCovered = skillsList,
                        IsGlobal = reader.IsDBNull(13),
                        FamilyId = reader.IsDBNull(13) ? null : reader.GetGuid(13),
                        CreatedDate = reader.GetDateTime(14)
                    });
                }

                if (results.Count > 0)
                {
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch capabilities from SQL database; falling back to in-memory store.");
            }
        }

        // Fallback / In-Memory
        lock (SyncLock)
        {
            var memoryItems = InMemoryCapabilities
                .Where(c => c.StageKey.Equals(stageKey, StringComparison.OrdinalIgnoreCase)
                            && (c.FamilyId == null || c.FamilyId == familyId))
                .ToList();

            return Task.FromResult(memoryItems).Result;
        }
    }

    /// <summary>
    /// Saves or updates a capability (used by the AI Agent and the Admin portal).
    /// </summary>
    public async Task<FamilyCapabilityResponse> SaveCapabilityAsync(
        SaveFamilyCapabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var capabilityId = Guid.NewGuid();
        var skillsJson = JsonSerializer.Serialize(request.SkillsCovered, JsonOptions);

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                // 1. Ensure stage exists
                const string stageSql = "SELECT StageId FROM family.HumanDevelopmentStages WHERE StageKey = @StageKey;";
                await using var stageCmd = new SqlCommand(stageSql, connection);
                stageCmd.Parameters.Add(new SqlParameter("@StageKey", SqlDbType.NVarChar, 50) { Value = request.StageKey });
                var stageIdObj = await stageCmd.ExecuteScalarAsync(cancellationToken);

                Guid stageId;
                if (stageIdObj != null && stageIdObj != DBNull.Value)
                {
                    stageId = (Guid)stageIdObj;
                }
                else
                {
                    stageId = Guid.NewGuid();
                    const string insertStageSql = @"
                        INSERT INTO family.HumanDevelopmentStages (StageId, StageKey, StageNameEs, StageNameEn, AgeRange, CategoryGroup, DisplayOrder)
                        VALUES (@StageId, @StageKey, @StageKey, @StageKey, 'K-12', 'General', 1);";
                    await using var insertStageCmd = new SqlCommand(insertStageSql, connection);
                    insertStageCmd.Parameters.Add(new SqlParameter("@StageId", stageId));
                    insertStageCmd.Parameters.Add(new SqlParameter("@StageKey", request.StageKey));
                    await insertStageCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // 2. Insert or update Capability
                const string capSql = @"
                    MERGE family.HumanCapabilities AS target
                    USING (SELECT @Code AS Code) AS source
                    ON (target.Code = source.Code)
                    WHEN MATCHED THEN
                        UPDATE SET 
                            NameEs = @NameEs,
                            NameEn = @NameEn,
                            Domain = @Domain,
                            DescriptionEs = @DescriptionEs,
                            DescriptionEn = @DescriptionEn,
                            WhyItMattersEs = @WhyItMattersEs,
                            WhyItMattersEn = @WhyItMattersEn,
                            SkillsCoveredJson = @SkillsCoveredJson,
                            FamilyId = @FamilyId
                    WHEN NOT MATCHED THEN
                        INSERT (CapabilityId, Code, NameEs, NameEn, Domain, DescriptionEs, DescriptionEn, WhyItMattersEs, WhyItMattersEn, SkillsCoveredJson, FamilyId)
                        VALUES (@CapabilityId, @Code, @NameEs, @NameEn, @Domain, @DescriptionEs, @DescriptionEn, @WhyItMattersEs, @WhyItMattersEn, @SkillsCoveredJson, @FamilyId)
                    OUTPUT inserted.CapabilityId;";

                await using var capCmd = new SqlCommand(capSql, connection);
                capCmd.Parameters.Add(new SqlParameter("@CapabilityId", capabilityId));
                capCmd.Parameters.Add(new SqlParameter("@Code", request.Code));
                capCmd.Parameters.Add(new SqlParameter("@NameEs", request.NameEs));
                capCmd.Parameters.Add(new SqlParameter("@NameEn", request.NameEn));
                capCmd.Parameters.Add(new SqlParameter("@Domain", request.Domain));
                capCmd.Parameters.Add(new SqlParameter("@DescriptionEs", request.DescriptionEs));
                capCmd.Parameters.Add(new SqlParameter("@DescriptionEn", request.DescriptionEn));
                capCmd.Parameters.Add(new SqlParameter("@WhyItMattersEs", (object?)request.WhyItMattersEs ?? DBNull.Value));
                capCmd.Parameters.Add(new SqlParameter("@WhyItMattersEn", (object?)request.WhyItMattersEn ?? DBNull.Value));
                capCmd.Parameters.Add(new SqlParameter("@SkillsCoveredJson", skillsJson));
                capCmd.Parameters.Add(new SqlParameter("@FamilyId", (object?)request.FamilyId ?? DBNull.Value));

                var actualCapId = (Guid)(await capCmd.ExecuteScalarAsync(cancellationToken) ?? capabilityId);

                // 3. Insert or update Requirement
                const string reqSql = @"
                    MERGE family.StageCapabilityRequirements AS target
                    USING (SELECT @StageId AS StageId, @CapabilityId AS CapabilityId) AS source
                    ON (target.StageId = source.StageId AND target.CapabilityId = source.CapabilityId)
                    WHEN MATCHED THEN
                        UPDATE SET TargetMasteryLevel = @Mastery, RecommendedHoursToMaster = @Hours, IsMandatoryForNextStage = @Mandatory
                    WHEN NOT MATCHED THEN
                        INSERT (RequirementId, StageId, CapabilityId, TargetMasteryLevel, RecommendedHoursToMaster, IsMandatoryForNextStage)
                        VALUES (NEWID(), @StageId, @CapabilityId, @Mastery, @Hours, @Mandatory);";

                await using var reqCmd = new SqlCommand(reqSql, connection);
                reqCmd.Parameters.Add(new SqlParameter("@StageId", stageId));
                reqCmd.Parameters.Add(new SqlParameter("@CapabilityId", actualCapId));
                reqCmd.Parameters.Add(new SqlParameter("@Mastery", request.TargetMasteryLevel));
                reqCmd.Parameters.Add(new SqlParameter("@Hours", request.RecommendedHoursToMaster));
                reqCmd.Parameters.Add(new SqlParameter("@Mandatory", request.IsMandatoryForNextStage));
                await reqCmd.ExecuteNonQueryAsync(cancellationToken);

                capabilityId = actualCapId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist capability to SQL; caching in memory.");
            }
        }

        var response = new FamilyCapabilityResponse
        {
            CapabilityId = capabilityId,
            Code = request.Code,
            StageKey = request.StageKey,
            NameEs = request.NameEs,
            NameEn = request.NameEn,
            Domain = request.Domain,
            DescriptionEs = request.DescriptionEs,
            DescriptionEn = request.DescriptionEn,
            WhyItMattersEs = request.WhyItMattersEs,
            WhyItMattersEn = request.WhyItMattersEn,
            TargetMasteryLevel = request.TargetMasteryLevel,
            RecommendedHoursToMaster = request.RecommendedHoursToMaster,
            SkillsCovered = request.SkillsCovered,
            IsGlobal = request.FamilyId == null,
            FamilyId = request.FamilyId,
            CreatedDate = DateTime.UtcNow
        };

        lock (SyncLock)
        {
            InMemoryCapabilities.RemoveAll(c => c.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
            InMemoryCapabilities.Add(response);
        }

        return response;
    }

    /// <summary>
    /// Saves the pedagogical learning content generated by the AI agent for a specific capability.
    /// </summary>
    public async Task<Guid> SaveLearningContentAsync(
        SaveCapabilityLearningContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var contentId = Guid.NewGuid();
        var homeActivitiesJson = JsonSerializer.Serialize(request.HomeActivities, JsonOptions);
        var socraticJson = JsonSerializer.Serialize(request.SocraticQuestions, JsonOptions);

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    INSERT INTO family.CapabilityLearningContent (
                        ContentId, CapabilityId, FamilyId, ModuleTitle,
                        PedagogicalGoal, KidExplanationStory, ParentGuide,
                        HomeActivitiesJson, SocraticQuestionsJson, MasteryRubricJson, IsPublished
                    ) VALUES (
                        @ContentId, @CapabilityId, @FamilyId, @ModuleTitle,
                        @PedagogicalGoal, @KidExplanationStory, @ParentGuide,
                        @HomeActivitiesJson, @SocraticQuestionsJson, @MasteryRubricJson, @IsPublished
                    );";

                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@ContentId", contentId));
                cmd.Parameters.Add(new SqlParameter("@CapabilityId", request.CapabilityId));
                cmd.Parameters.Add(new SqlParameter("@FamilyId", (object?)request.FamilyId ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ModuleTitle", request.ModuleTitle));
                cmd.Parameters.Add(new SqlParameter("@PedagogicalGoal", request.PedagogicalGoal));
                cmd.Parameters.Add(new SqlParameter("@KidExplanationStory", (object?)request.KidExplanationStory ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ParentGuide", (object?)request.ParentGuide ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@HomeActivitiesJson", homeActivitiesJson));
                cmd.Parameters.Add(new SqlParameter("@SocraticQuestionsJson", socraticJson));
                cmd.Parameters.Add(new SqlParameter("@MasteryRubricJson", (object?)request.MasteryRubric ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@IsPublished", request.IsPublished));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist course learning content to SQL; storing in memory.");
            }
        }

        lock (SyncLock)
        {
            InMemoryContents.Add(request);
        }

        return contentId;
    }

    /// <summary>
    /// Persists (upsert, keyed by CourseCode) the complete academic course document authored
    /// by the ProfesorExpert AI agent — this is what makes "Aprobar y Guardar Curso en Base de
    /// Datos" in the Academic Course Generator UI actually durable instead of a no-op toast.
    /// </summary>
    public async Task<SaveAcademicCourseDocumentResponse> SaveAcademicCourseDocumentAsync(
        SaveAcademicCourseDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var documentId = Guid.NewGuid();
        var updatedDate = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    MERGE family.AcademicCourseDocument AS target
                    USING (SELECT @CourseCode AS CourseCode) AS source
                    ON target.CourseCode = source.CourseCode
                    WHEN MATCHED THEN
                        UPDATE SET
                            TitleEs = @TitleEs,
                            TitleEn = @TitleEn,
                            Subject = @Subject,
                            GradeLevel = @GradeLevel,
                            Language = @Language,
                            EstimatedHours = @EstimatedHours,
                            CourseDocumentMarkdown = @CourseDocumentMarkdown,
                            TeacherIntroduction = @TeacherIntroduction,
                            PedagogicalApproach = @PedagogicalApproach,
                            ArchitectureSummaryJson = @ArchitectureSummaryJson,
                            IsApprovedByEducator = @IsApprovedByEducator,
                            EducatorReviewNotes = @EducatorReviewNotes,
                            UpdatedDate = @UpdatedDate
                    WHEN NOT MATCHED THEN
                        INSERT (
                            DocumentId, CourseCode, TitleEs, TitleEn, Subject, GradeLevel, Language,
                            EstimatedHours, CourseDocumentMarkdown, TeacherIntroduction, PedagogicalApproach,
                            ArchitectureSummaryJson, IsApprovedByEducator, EducatorReviewNotes, CreatedDate, UpdatedDate
                        )
                        VALUES (
                            @DocumentId, @CourseCode, @TitleEs, @TitleEn, @Subject, @GradeLevel, @Language,
                            @EstimatedHours, @CourseDocumentMarkdown, @TeacherIntroduction, @PedagogicalApproach,
                            @ArchitectureSummaryJson, @IsApprovedByEducator, @EducatorReviewNotes, @UpdatedDate, @UpdatedDate
                        )
                    OUTPUT inserted.DocumentId;";

                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@DocumentId", documentId));
                cmd.Parameters.Add(new SqlParameter("@CourseCode", request.CourseCode));
                cmd.Parameters.Add(new SqlParameter("@TitleEs", request.TitleEs));
                cmd.Parameters.Add(new SqlParameter("@TitleEn", request.TitleEn));
                cmd.Parameters.Add(new SqlParameter("@Subject", request.Subject));
                cmd.Parameters.Add(new SqlParameter("@GradeLevel", request.GradeLevel));
                cmd.Parameters.Add(new SqlParameter("@Language", request.Language));
                cmd.Parameters.Add(new SqlParameter("@EstimatedHours", request.EstimatedHours));
                cmd.Parameters.Add(new SqlParameter("@CourseDocumentMarkdown", request.CourseDocumentMarkdown));
                cmd.Parameters.Add(new SqlParameter("@TeacherIntroduction", (object?)request.TeacherIntroduction ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@PedagogicalApproach", (object?)request.PedagogicalApproach ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ArchitectureSummaryJson", (object?)request.ArchitectureSummaryJson ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@IsApprovedByEducator", request.IsApprovedByEducator));
                cmd.Parameters.Add(new SqlParameter("@EducatorReviewNotes", (object?)request.EducatorReviewNotes ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@UpdatedDate", updatedDate));

                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                var savedId = result is Guid g ? g : documentId;

                lock (SyncLock)
                {
                    InMemoryAcademicCourses[request.CourseCode] = request;
                }

                return new SaveAcademicCourseDocumentResponse
                {
                    DocumentId = savedId,
                    CourseCode = request.CourseCode,
                    UpdatedDate = updatedDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist academic course document to SQL; storing in memory only.");
            }
        }

        lock (SyncLock)
        {
            InMemoryAcademicCourses[request.CourseCode] = request;
        }

        return new SaveAcademicCourseDocumentResponse
        {
            DocumentId = documentId,
            CourseCode = request.CourseCode,
            UpdatedDate = updatedDate
        };
    }

    /// <summary>Retrieves the previously saved academic course document for a given CourseCode, if any.</summary>
    public async Task<AcademicCourseDocumentResponse?> GetAcademicCourseDocumentAsync(
        string courseCode,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT DocumentId, CourseCode, TitleEs, TitleEn, Subject, GradeLevel, Language,
                           EstimatedHours, CourseDocumentMarkdown, TeacherIntroduction, PedagogicalApproach,
                           ArchitectureSummaryJson, IsApprovedByEducator, EducatorReviewNotes, CreatedDate, UpdatedDate
                    FROM family.AcademicCourseDocument
                    WHERE CourseCode = @CourseCode;";

                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@CourseCode", SqlDbType.NVarChar, 50) { Value = courseCode });

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return new AcademicCourseDocumentResponse
                    {
                        DocumentId = reader.GetGuid(0),
                        CourseCode = reader.GetString(1),
                        TitleEs = reader.GetString(2),
                        TitleEn = reader.GetString(3),
                        Subject = reader.GetString(4),
                        GradeLevel = reader.GetString(5),
                        Language = reader.GetString(6),
                        EstimatedHours = reader.GetInt32(7),
                        CourseDocumentMarkdown = reader.GetString(8),
                        TeacherIntroduction = reader.IsDBNull(9) ? null : reader.GetString(9),
                        PedagogicalApproach = reader.IsDBNull(10) ? null : reader.GetString(10),
                        ArchitectureSummaryJson = reader.IsDBNull(11) ? null : reader.GetString(11),
                        IsApprovedByEducator = reader.GetBoolean(12),
                        EducatorReviewNotes = reader.IsDBNull(13) ? null : reader.GetString(13),
                        CreatedDate = reader.GetDateTime(14),
                        UpdatedDate = reader.GetDateTime(15)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch academic course document from SQL; falling back to in-memory store.");
            }
        }

        lock (SyncLock)
        {
            if (InMemoryAcademicCourses.TryGetValue(courseCode, out var memoryRequest))
            {
                return new AcademicCourseDocumentResponse
                {
                    DocumentId = Guid.Empty,
                    CourseCode = memoryRequest.CourseCode,
                    TitleEs = memoryRequest.TitleEs,
                    TitleEn = memoryRequest.TitleEn,
                    Subject = memoryRequest.Subject,
                    GradeLevel = memoryRequest.GradeLevel,
                    Language = memoryRequest.Language,
                    EstimatedHours = memoryRequest.EstimatedHours,
                    CourseDocumentMarkdown = memoryRequest.CourseDocumentMarkdown,
                    TeacherIntroduction = memoryRequest.TeacherIntroduction,
                    PedagogicalApproach = memoryRequest.PedagogicalApproach,
                    ArchitectureSummaryJson = memoryRequest.ArchitectureSummaryJson,
                    IsApprovedByEducator = memoryRequest.IsApprovedByEducator,
                    EducatorReviewNotes = memoryRequest.EducatorReviewNotes,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };
            }
        }

        return null;
    }

    /// <summary>Lightweight listing of every saved academic course document (2026-10-08) —
    /// powers the "Catálogo Académico" sidebar page, deliberately omitting the full
    /// CourseDocumentMarkdown (can be 100K+ chars) to keep the list payload small.</summary>
    public async Task<List<AcademicCourseDocumentSummaryResponse>> ListAcademicCourseDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<AcademicCourseDocumentSummaryResponse>();

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT CourseCode, TitleEs, TitleEn, Subject, GradeLevel, Language,
                           EstimatedHours, CAST(LEN(CourseDocumentMarkdown) AS INT), IsApprovedByEducator,
                           CreatedDate, UpdatedDate
                    FROM family.AcademicCourseDocument
                    WHERE CourseCode IS NOT NULL AND LTRIM(RTRIM(CourseCode)) <> ''
                    ORDER BY UpdatedDate DESC;";

                await using var cmd = new SqlCommand(sql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new AcademicCourseDocumentSummaryResponse
                    {
                        CourseCode = reader.GetString(0),
                        TitleEs = reader.GetString(1),
                        TitleEn = reader.GetString(2),
                        Subject = reader.GetString(3),
                        GradeLevel = reader.GetString(4),
                        Language = reader.GetString(5),
                        EstimatedHours = reader.GetInt32(6),
                        CourseDocumentMarkdownLength = reader.GetInt32(7),
                        IsApprovedByEducator = reader.GetBoolean(8),
                        CreatedDate = reader.GetDateTime(9),
                        UpdatedDate = reader.GetDateTime(10)
                    });
                }

                if (results.Count > 0)
                {
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not list academic course documents from SQL; falling back to in-memory store.");
            }
        }

        lock (SyncLock)
        {
            return InMemoryAcademicCourses.Values
                .Where(r => !string.IsNullOrWhiteSpace(r.CourseCode))
                .OrderByDescending(r => r.CourseCode)
                .Select(r => new AcademicCourseDocumentSummaryResponse
                {
                    CourseCode = r.CourseCode,
                    TitleEs = r.TitleEs,
                    TitleEn = r.TitleEn,
                    Subject = r.Subject,
                    GradeLevel = r.GradeLevel,
                    Language = r.Language,
                    EstimatedHours = r.EstimatedHours,
                    CourseDocumentMarkdownLength = r.CourseDocumentMarkdown.Length,
                    IsApprovedByEducator = r.IsApprovedByEducator,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                })
                .ToList();
        }
    }

    /// <summary>Deletes one saved academic course document by CourseCode (2026-10-08) —
    /// lets the "Catálogo Académico" page's delete button clean up test/broken drafts.
    /// Idempotent: deleting a code that doesn't exist is a no-op, not an error.</summary>
    public async Task DeleteAcademicCourseDocumentAsync(string courseCode, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = "DELETE FROM family.AcademicCourseDocument WHERE CourseCode = @CourseCode;";
                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@CourseCode", courseCode));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete academic course document '{CourseCode}' from SQL.", courseCode);
            }
        }

        lock (SyncLock)
        {
            InMemoryAcademicCourses.Remove(courseCode);
        }
    }
}
