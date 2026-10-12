-- ==============================================================================
-- ENGRAMSIQ FAMILY / HUMANOS FOR FAMILIES (USA & LATAM)
-- BASE DE DATOS Y ESQUEMA INDEPENDIENTE
--
-- NOTA IMPORTANTE DE AISLAMIENTO:
-- Este script NO modifica, altera, elimina ni reemplaza ninguna tabla existente
-- de los otros proyectos (HumanOS Enterprise, Employee, Grow, etc.).
-- Todo este esquema es 100% aislado, nuevo e independiente.
-- ==============================================================================

-- 1. Crear la base de datos exclusiva EngramsFamily si no existe
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'EngramsFamily')
BEGIN
    CREATE DATABASE [EngramsFamily];
END
GO

USE [EngramsFamily];
GO

-- 2. Esquema dedicado 'family' para máxima organización
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'family')
BEGIN
    EXEC('CREATE SCHEMA [family]');
END
GO

-- ==============================================================================
-- TABLA 1: family.FamilyMaster (Master File de la Familia / Tenant)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyMaster' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.FamilyMaster (
        -- Identificador único de la familia (TenantId)
        FamilyId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        FamilyName NVARCHAR(150) NOT NULL,                  -- Ej: "Familia Mendoza-Ríos"
        FamilySlug NVARCHAR(100) NOT NULL,                  -- Ej: "mendoza-rios"
        HouseholdType NVARCHAR(50) NOT NULL DEFAULT 'Bicultural', -- Bicultural, Nuclear, Extended

        -- Administrador Principal del Hogar (Papá, Mamá o Tutor Legal)
        AdminFullName NVARCHAR(150) NOT NULL,               -- Ej: "Carlos Mendoza"
        AdminEmail NVARCHAR(150) NOT NULL,                  -- Correo del administrador
        AdminPhone NVARCHAR(50) NULL,                       -- Teléfono celular
        AdminAzureOid NVARCHAR(100) NULL,                   -- OID de Azure Entra CIAM para login

        -- Co-Administrador (Cónyuge / Segundo Tutor)
        CoAdminFullName NVARCHAR(150) NULL,                 -- Ej: "Dra. Elena Ríos"
        CoAdminEmail NVARCHAR(150) NULL,

        -- Domicilio Principal (USA o LATAM)
        PrimaryStreet NVARCHAR(200) NULL,                   -- Calle y número
        PrimaryCity NVARCHAR(100) NULL,                     -- Ej: "Austin"
        PrimaryState NVARCHAR(100) NULL,                    -- Ej: "TX"
        PrimaryZipCode NVARCHAR(20) NULL,                   -- Código postal
        PrimaryCountry NVARCHAR(50) NOT NULL DEFAULT 'USA', -- "USA", "MEX", "COL", etc.

        -- Residencia o Hub Secundario (para familias transfronterizas / biculturales)
        SecondaryLocation NVARCHAR(150) NULL,               -- Ej: "Ciudad de México (Residencia secundaria)"

        -- Configuración Regional y Bicultural
        TimeZone NVARCHAR(100) NOT NULL DEFAULT 'America/Chicago', -- CST (Austin / CDMX)
        PreferredCurrency NVARCHAR(3) NOT NULL DEFAULT 'USD',      -- "USD" o "MXN"
        PrimaryLanguage NVARCHAR(10) NOT NULL DEFAULT 'es',        -- "es" o "en"
        IsBilingualHousehold BIT NOT NULL DEFAULT 1,

        -- Parámetros de Bienestar y Finanzas Familiares
        AnnualEducationBudget DECIMAL(18,2) NULL,           -- Ej: 27000.00
        DigitalSunsetTime TIME NULL DEFAULT '21:00:00',     -- Desconexión de pantallas
        FamilyMissionStatement NVARCHAR(MAX) NULL,          -- Valores y acuerdos del hogar

        -- Estado del Servicio
        SubscriptionTier NVARCHAR(50) NOT NULL DEFAULT 'FamilyEnterprise',
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_FamilyMaster PRIMARY KEY (FamilyId),
        CONSTRAINT UQ_FamilyMaster_Slug UNIQUE (FamilySlug)
    );
END
GO

-- ==============================================================================
-- TABLA 2: family.FamilyMembers (Integrantes de la Familia)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyMembers' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.FamilyMembers (
        MemberId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        PreferredName NVARCHAR(100) NOT NULL,
        FamilyRole NVARCHAR(50) NOT NULL,                   -- Father, Mother, TeenDaughter, Son, Grandmother, Mentor
        RoleTitleEs NVARCHAR(150) NOT NULL,                 -- Ej: "Padre · VP de Ingeniería & IA"
        RoleTitleEn NVARCHAR(150) NOT NULL,                 -- Ej: "Father · VP of Engineering & AI"
        DateOfBirth DATE NULL,
        Age INT NULL,
        EducationLevelEs NVARCHAR(150) NULL,
        EducationLevelEn NVARCHAR(150) NULL,
        Institution NVARCHAR(150) NULL,
        PrimaryLocation NVARCHAR(100) NULL,
        Email NVARCHAR(150) NULL,
        AzureOid NVARCHAR(100) NULL,
        AvatarBg NVARCHAR(50) NOT NULL DEFAULT 'from-emerald-600 to-teal-700',
        LanguagesJson NVARCHAR(500) NULL,                   -- JSON array de idiomas
        TopFocusJson NVARCHAR(500) NULL,                    -- JSON array de enfoques
        WeeklyHoursTarget DECIMAL(5,2) NOT NULL DEFAULT 8.0,
        LearningStreakDays INT NOT NULL DEFAULT 0,
        TargetMilestoneEs NVARCHAR(300) NULL,
        TargetMilestoneEn NVARCHAR(300) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_FamilyMembers PRIMARY KEY (MemberId),
        CONSTRAINT FK_FamilyMembers_FamilyMaster FOREIGN KEY (FamilyId)
            REFERENCES family.FamilyMaster (FamilyId) ON DELETE CASCADE
    );
END
GO

-- ==============================================================================
-- TABLA 3: family.FamilyGoals (Metas Estratégicas del Hogar)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyGoals' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.FamilyGoals (
        GoalId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        TitleEs NVARCHAR(200) NOT NULL,
        TitleEn NVARCHAR(200) NOT NULL,
        DescriptionEs NVARCHAR(MAX) NULL,
        DescriptionEn NVARCHAR(MAX) NULL,
        Category NVARCHAR(50) NOT NULL,                     -- academic, career, wellness, culture_bilingual, finance
        AssignedMemberIdsJson NVARCHAR(500) NULL,           -- JSON array de MemberIds
        ProgressPercent INT NOT NULL DEFAULT 0,
        DueDate DATE NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'in_progress', -- planned, in_progress, completed
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_FamilyGoals PRIMARY KEY (GoalId),
        CONSTRAINT FK_FamilyGoals_FamilyMaster FOREIGN KEY (FamilyId)
            REFERENCES family.FamilyMaster (FamilyId) ON DELETE CASCADE
    );
END
GO

-- ==============================================================================
-- TABLA 4: family.FamilyMilestones (Hitos y Agenda de Compromisos Clave)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyMilestones' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.FamilyMilestones (
        MilestoneId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        MemberId UNIQUEIDENTIFIER NULL,
        TitleEs NVARCHAR(200) NOT NULL,
        TitleEn NVARCHAR(200) NOT NULL,
        EventDate DATETIME2 NOT NULL,
        TimeDescription NVARCHAR(50) NULL,                  -- Ej: "09:00 AM CST"
        Location NVARCHAR(200) NULL,                        -- Ej: "Austin Convention Center"
        TagEs NVARCHAR(100) NULL,
        TagEn NVARCHAR(100) NULL,
        Category NVARCHAR(50) NOT NULL,                     -- university, career, school, family_time, health
        IsCompleted BIT NOT NULL DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_FamilyMilestones PRIMARY KEY (MilestoneId),
        CONSTRAINT FK_FamilyMilestones_FamilyMaster FOREIGN KEY (FamilyId)
            REFERENCES family.FamilyMaster (FamilyId) ON DELETE CASCADE,
        CONSTRAINT FK_FamilyMilestones_Member FOREIGN KEY (MemberId)
            REFERENCES family.FamilyMembers (MemberId)
    );
END
GO

-- ==============================================================================
-- TABLA 5: family.FamilyLearningTracks (Rutas de Estudio y Certificaciones)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyLearningTracks' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.FamilyLearningTracks (
        TrackId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        MemberId UNIQUEIDENTIFIER NOT NULL,
        TitleEs NVARCHAR(200) NOT NULL,
        TitleEn NVARCHAR(200) NOT NULL,
        Institution NVARCHAR(150) NOT NULL,
        TargetLevel NVARCHAR(100) NULL,
        HoursPerWeek DECIMAL(4,1) NOT NULL DEFAULT 2.0,
        ProgressPercent INT NOT NULL DEFAULT 0,
        BadgeEs NVARCHAR(100) NULL,
        BadgeEn NVARCHAR(100) NULL,
        RegionFocus NVARCHAR(50) NOT NULL DEFAULT 'USA',    -- USA, LATAM, Bicultural, Global
        Status NVARCHAR(50) NOT NULL DEFAULT 'active',      -- active, completed, upcoming
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_FamilyLearningTracks PRIMARY KEY (TrackId),
        CONSTRAINT FK_FamilyLearningTracks_FamilyMaster FOREIGN KEY (FamilyId)
            REFERENCES family.FamilyMaster (FamilyId) ON DELETE CASCADE,
        CONSTRAINT FK_FamilyLearningTracks_Member FOREIGN KEY (MemberId)
            REFERENCES family.FamilyMembers (MemberId)
    );
END
GO

-- ==============================================================================
-- TABLA 6: family.FamilyBudgetItems (Presupuesto Educativo Detallado)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyBudgetItems' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.FamilyBudgetItems (
        BudgetItemId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        CategoryEs NVARCHAR(150) NOT NULL,
        CategoryEn NVARCHAR(150) NOT NULL,
        CostUsd DECIMAL(18,2) NOT NULL DEFAULT 0,
        CostMxn DECIMAL(18,2) NOT NULL DEFAULT 0,
        AllocatedTo NVARCHAR(150) NOT NULL,
        SpentPercent INT NOT NULL DEFAULT 0,
        FiscalYear INT NOT NULL DEFAULT 2026,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_FamilyBudgetItems PRIMARY KEY (BudgetItemId),
        CONSTRAINT FK_FamilyBudgetItems_FamilyMaster FOREIGN KEY (FamilyId)
            REFERENCES family.FamilyMaster (FamilyId) ON DELETE CASCADE
    );
END
GO

-- ==============================================================================
-- TABLA 7: family.HumanDevelopmentStages (Niveles en el Tiempo Humano)
-- Desde Elementary (Kinder a 5th Grade), Middle, High School hasta Profesional/Adulto
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HumanDevelopmentStages' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.HumanDevelopmentStages (
        StageId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        StageKey NVARCHAR(50) NOT NULL,                    -- 'first_grade', 'third_grade', 'high_school', 'professional'
        StageNameEs NVARCHAR(100) NOT NULL,               -- Ej: '1° de Primaria (First Grade)'
        StageNameEn NVARCHAR(100) NOT NULL,               -- Ej: 'First Grade'
        AgeRange NVARCHAR(50) NOT NULL,                   -- Ej: '6 - 7 años'
        CategoryGroup NVARCHAR(50) NOT NULL,              -- Elementary, Middle, HighSchool, College, Professional
        DisplayOrder INT NOT NULL,
        DescriptionEs NVARCHAR(MAX) NULL,
        DescriptionEn NVARCHAR(MAX) NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_HumanDevelopmentStages PRIMARY KEY (StageId),
        CONSTRAINT UQ_HumanDevelopmentStages_Key UNIQUE (StageKey)
    );
END
GO

-- ==============================================================================
-- TABLA 8: family.HumanCapabilities (Catálogo Global de Capabilities / Skills)
-- Como lo tiene HumanOS, estructurado por dominios cognitivos, socioemocionales y técnicos
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HumanCapabilities' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.HumanCapabilities (
        CapabilityId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        Code NVARCHAR(50) NOT NULL,                        -- Ej: 'ELEM-MATH-01'
        NameEs NVARCHAR(150) NOT NULL,
        NameEn NVARCHAR(150) NOT NULL,
        Domain NVARCHAR(50) NOT NULL,                     -- 'STEM & Math', 'Language & Literacy', 'Socio-Emotional', 'Critical Thinking', 'Practical Life'
        DescriptionEs NVARCHAR(MAX) NOT NULL,
        DescriptionEn NVARCHAR(MAX) NOT NULL,
        WhyItMattersEs NVARCHAR(MAX) NULL,
        WhyItMattersEn NVARCHAR(MAX) NULL,
        IsCoreFoundational BIT NOT NULL DEFAULT 1,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_HumanCapabilities PRIMARY KEY (CapabilityId),
        CONSTRAINT UQ_HumanCapabilities_Code UNIQUE (Code)
    );
END
GO

-- ==============================================================================
-- TABLA 9: family.StageCapabilityRequirements (Nivel Humano ↔ Capabilities Requeridas)
-- Permite que un nivel humano (ej: First Grade) tenga muchas capabilities para dominar (Mastery)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StageCapabilityRequirements' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.StageCapabilityRequirements (
        RequirementId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        StageId UNIQUEIDENTIFIER NOT NULL,
        CapabilityId UNIQUEIDENTIFIER NOT NULL,
        TargetMasteryLevel NVARCHAR(50) NOT NULL DEFAULT 'Proficient', -- Foundation, Proficient, Advanced, Master
        IsMandatoryForNextStage BIT NOT NULL DEFAULT 1,
        RecommendedHoursToMaster INT NOT NULL DEFAULT 40,
        AssessmentCriteriaEs NVARCHAR(MAX) NULL,
        AssessmentCriteriaEn NVARCHAR(MAX) NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_StageCapabilityRequirements PRIMARY KEY (RequirementId),
        CONSTRAINT FK_StageCap_Stage FOREIGN KEY (StageId) 
            REFERENCES family.HumanDevelopmentStages (StageId) ON DELETE CASCADE,
        CONSTRAINT FK_StageCap_Cap FOREIGN KEY (CapabilityId) 
            REFERENCES family.HumanCapabilities (CapabilityId) ON DELETE CASCADE,
        CONSTRAINT UQ_Stage_Capability UNIQUE (StageId, CapabilityId)
    );
END
GO

-- ==============================================================================
-- TABLA 10: family.CapabilityLearningContent (Contenido Pedagógico / Cursos Generados)
-- Almacena los módulos, cuentos, guías para padres y retos generados por el Agente AI
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CapabilityLearningContent' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.CapabilityLearningContent (
        ContentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CapabilityId UNIQUEIDENTIFIER NOT NULL,
        StageId UNIQUEIDENTIFIER NULL,
        FamilyId UNIQUEIDENTIFIER NULL,                    -- NULL = Contenido Estándar Global; o ID de Familia para contenido personalizado
        ModuleTitle NVARCHAR(200) NOT NULL,
        PedagogicalGoal NVARCHAR(MAX) NOT NULL,
        KidExplanationStory NVARCHAR(MAX) NULL,             -- Cuento o analogía explicativa adaptada a la edad
        ParentGuide NVARCHAR(MAX) NULL,                     -- Guía de 5 minutos para acompañamiento en casa
        HomeActivitiesJson NVARCHAR(MAX) NULL,              -- JSON con 3 actividades prácticas para el hogar
        SocraticQuestionsJson NVARCHAR(MAX) NULL,           -- Preguntas socráticas para el tutor interactivo AI
        MasteryRubricJson NVARCHAR(MAX) NULL,               -- Rúbrica de evaluación para marcar 'Master'
        IsPublished BIT NOT NULL DEFAULT 1,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_CapabilityLearningContent PRIMARY KEY (ContentId),
        CONSTRAINT FK_CapContent_Capability FOREIGN KEY (CapabilityId) 
            REFERENCES family.HumanCapabilities (CapabilityId) ON DELETE CASCADE,
        CONSTRAINT FK_CapContent_Family FOREIGN KEY (FamilyId) 
            REFERENCES family.FamilyMaster (FamilyId)
    );
END
GO

-- ==============================================================================
-- TABLA 11: family.AcademicCourseDocument (Documento Completo del Curso Académico
-- generado por el Agente ProfesorExpert — el "libro de curso" completo con
-- CourseDocumentMarkdown, keyed por CourseCode (ej. 'KIND-LIT-01') en vez de un
-- CapabilityId, para no requerir que el código exista primero en HumanCapabilities.
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AcademicCourseDocument' AND schema_id = SCHEMA_ID('family'))
BEGIN
    CREATE TABLE family.AcademicCourseDocument (
        DocumentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CourseCode NVARCHAR(50) NOT NULL,
        TitleEs NVARCHAR(300) NOT NULL,
        TitleEn NVARCHAR(300) NOT NULL,
        Subject NVARCHAR(200) NOT NULL,
        GradeLevel NVARCHAR(200) NOT NULL,
        Language NVARCHAR(10) NOT NULL DEFAULT 'es',
        EstimatedHours INT NOT NULL DEFAULT 35,
        CourseDocumentMarkdown NVARCHAR(MAX) NOT NULL,
        TeacherIntroduction NVARCHAR(MAX) NULL,
        PedagogicalApproach NVARCHAR(MAX) NULL,
        ArchitectureSummaryJson NVARCHAR(MAX) NULL,
        IsApprovedByEducator BIT NOT NULL DEFAULT 0,
        EducatorReviewNotes NVARCHAR(MAX) NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_AcademicCourseDocument PRIMARY KEY (DocumentId),
        CONSTRAINT UQ_AcademicCourseDocument_Code UNIQUE (CourseCode)
    );
END
GO

-- ==============================================================================
-- DATOS SEMILLA (Familia Mendoza-Ríos en Austin & CDMX)
-- ==============================================================================
DECLARE @FamId UNIQUEIDENTIFIER = 'A1000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM family.FamilyMaster WHERE FamilyId = @FamId)
BEGIN
    INSERT INTO family.FamilyMaster (
        FamilyId, FamilyName, FamilySlug, HouseholdType,
        AdminFullName, AdminEmail, AdminPhone,
        CoAdminFullName, CoAdminEmail,
        PrimaryStreet, PrimaryCity, PrimaryState, PrimaryZipCode, PrimaryCountry,
        SecondaryLocation, TimeZone, PreferredCurrency, PrimaryLanguage, IsBilingualHousehold,
        AnnualEducationBudget, DigitalSunsetTime,
        FamilyMissionStatement, SubscriptionTier, IsActive
    ) VALUES (
        @FamId, 'Familia Mendoza-Ríos', 'mendoza-rios', 'Bicultural',
        'Carlos Mendoza, M.Sc.', 'carlos.mendoza@engrams.family', '+1 (512) 555-0192',
        'Dra. Elena Ríos Mendoza', 'elena.rios@engrams.family',
        '1204 Oak Creek Blvd', 'Austin', 'TX', '78701', 'USA',
        'Ciudad de México (Residencia secundaria)', 'America/Chicago', 'USD', 'es', 1,
        27000.00, '21:00:00',
        'Compromiso con el bilingüismo académico, el desarrollo socioemocional, la curiosidad intelectual y el orgullo por nuestras raíces latinoamericanas.',
        'FamilyEnterprise', 1
    );

    PRINT 'Familia Mendoza-Ríos insertada exitosamente en el esquema family.';
END
GO
