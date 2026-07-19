CREATE TABLE [dbo].[CapabilityDomain] (
    [CapabilityDomainId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [Code] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityDomain] PRIMARY KEY ([CapabilityDomainId])
);
GO


CREATE TABLE [dbo].[Goal] (
    [GoalId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [Category] nvarchar(200) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Goal] PRIMARY KEY ([GoalId]),
    CONSTRAINT [CK_Goal_Category] CHECK ([Category] IS NULL
OR [Category] IN
(
    'PERSONAL_GROWTH',
    'CAPABILITY_DEVELOPMENT',
    'PROFESSIONAL',
    'VALUE_CREATION',
    'CONTRIBUTION',
    'LIFE'
))
);
GO


CREATE TABLE [dbo].[Language] (
    [LanguageCode] nvarchar(10) NOT NULL,
    [EnglishName] nvarchar(100) NOT NULL,
    [NativeName] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Language] PRIMARY KEY ([LanguageCode])
);
GO


CREATE TABLE [dbo].[RuntimeSessionStatus] (
    [SessionId] nvarchar(100) NOT NULL,
    [IsTerminal] bit NOT NULL,
    [FinalStage] nvarchar(50) NULL,
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_RuntimeSessionStatus] PRIMARY KEY ([SessionId])
);
GO


CREATE TABLE [dbo].[RuntimeWorkflowCheckpoint] (
    [RuntimeWorkflowCheckpointId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [SessionId] nvarchar(100) NOT NULL,
    [CheckpointId] nvarchar(100) NOT NULL,
    [ParentCheckpointId] nvarchar(100) NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_RuntimeWorkflowCheckpoint] PRIMARY KEY ([RuntimeWorkflowCheckpointId])
);
GO


CREATE TABLE [dbo].[Tenant] (
    [TenantId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [Name] nvarchar(200) NOT NULL,
    [Slug] nvarchar(100) NOT NULL,
    [Domain] nvarchar(255) NULL,
    [Description] nvarchar(1000) NULL,
    [Address] nvarchar(500) NULL,
    [Email] nvarchar(255) NULL,
    [Phone] nvarchar(50) NULL,
    [CultureCode] nvarchar(10) NOT NULL DEFAULT N'en-US',
    [TimeZone] nvarchar(100) NOT NULL DEFAULT N'UTC',
    [AzureTenantId] nvarchar(100) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Tenant] PRIMARY KEY ([TenantId])
);
GO


CREATE TABLE [dbo].[Capability] (
    [CapabilityId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityDomainId] uniqueidentifier NOT NULL,
    [Code] nvarchar(200) NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Capability] PRIMARY KEY ([CapabilityId]),
    CONSTRAINT [FK_Capability_CapabilityDomain] FOREIGN KEY ([CapabilityDomainId]) REFERENCES [dbo].[CapabilityDomain] ([CapabilityDomainId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[CapabilityDomainTranslation] (
    [CapabilityDomainId] uniqueidentifier NOT NULL,
    [LanguageCode] nvarchar(20) NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityDomainTranslation] PRIMARY KEY ([CapabilityDomainId], [LanguageCode]),
    CONSTRAINT [FK_CapabilityDomainTranslation_Domain] FOREIGN KEY ([CapabilityDomainId]) REFERENCES [dbo].[CapabilityDomain] ([CapabilityDomainId]) ON DELETE CASCADE,
    CONSTRAINT [FK_CapabilityDomainTranslation_Language] FOREIGN KEY ([LanguageCode]) REFERENCES [dbo].[Language] ([LanguageCode]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[GoalTranslation] (
    [GoalId] uniqueidentifier NOT NULL,
    [LanguageCode] nvarchar(10) NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_GoalTranslation] PRIMARY KEY ([GoalId], [LanguageCode]),
    CONSTRAINT [FK_GoalTranslation_Goal] FOREIGN KEY ([GoalId]) REFERENCES [dbo].[Goal] ([GoalId]) ON DELETE CASCADE,
    CONSTRAINT [FK_GoalTranslation_Language] FOREIGN KEY ([LanguageCode]) REFERENCES [dbo].[Language] ([LanguageCode]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[Person] (
    [PersonId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [TenantId] uniqueidentifier NOT NULL,
    [AzureOid] nvarchar(100) NOT NULL,
    [AzureTid] nvarchar(100) NOT NULL,
    [Email] nvarchar(255) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [LastLoginDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Person] PRIMARY KEY ([PersonId]),
    CONSTRAINT [FK_Person_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[Agent] (
    [AgentId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityId] uniqueidentifier NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    [Role] nvarchar(255) NULL,
    [Description] nvarchar(max) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Agent] PRIMARY KEY ([AgentId]),
    CONSTRAINT [FK_Agent_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[Assessment] (
    [AssessmentId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityId] uniqueidentifier NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [AssessmentType] nvarchar(100) NOT NULL,
    [PassingScore] decimal(5,2) NOT NULL DEFAULT 70.0,
    [MaxScore] decimal(5,2) NOT NULL DEFAULT 100.0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Assessment] PRIMARY KEY ([AssessmentId]),
    CONSTRAINT [CK_Assessment_Scores] CHECK ([MaxScore] > 0
AND [PassingScore] BETWEEN 0 AND [MaxScore]),
    CONSTRAINT [FK_Assessment_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CapabilityGraphs] (
    [CapabilityGraphId] uniqueidentifier NOT NULL,
    [CapabilityId] uniqueidentifier NOT NULL,
    [Name] nvarchar(500) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_CapabilityGraphs] PRIMARY KEY ([CapabilityGraphId]),
    CONSTRAINT [FK_CapabilityGraphs_Capability_CapabilityId] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityLevel] (
    [CapabilityLevelId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityId] uniqueidentifier NOT NULL,
    [Layer] nvarchar(50) NOT NULL,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(400) NOT NULL,
    [HumanTransformation] nvarchar(2000) NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityLevel] PRIMARY KEY ([CapabilityLevelId]),
    CONSTRAINT [FK_CapabilityLevel_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityTranslation] (
    [CapabilityId] uniqueidentifier NOT NULL,
    [LanguageCode] nvarchar(10) NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityTranslation] PRIMARY KEY ([CapabilityId], [LanguageCode]),
    CONSTRAINT [FK_CapabilityTranslation_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE CASCADE,
    CONSTRAINT [FK_CapabilityTranslation_Language] FOREIGN KEY ([LanguageCode]) REFERENCES [dbo].[Language] ([LanguageCode]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[GoalCapability] (
    [GoalId] uniqueidentifier NOT NULL,
    [CapabilityId] uniqueidentifier NOT NULL,
    [IsRequired] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_GoalCapability] PRIMARY KEY ([GoalId], [CapabilityId]),
    CONSTRAINT [FK_GoalCapability_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GoalCapability_Goal] FOREIGN KEY ([GoalId]) REFERENCES [dbo].[Goal] ([GoalId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[Project] (
    [ProjectId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityId] uniqueidentifier NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [DifficultyLevel] int NOT NULL DEFAULT 1,
    [EstimatedHours] decimal(6,2) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Project] PRIMARY KEY ([ProjectId]),
    CONSTRAINT [CK_Project_DifficultyLevel] CHECK ([DifficultyLevel] BETWEEN 1 AND 5),
    CONSTRAINT [FK_Project_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[HumanProfile] (
    [HumanProfileId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [MissionStatement] nvarchar(4000) NULL,
    [PrimaryGoal] nvarchar(2000) NULL,
    [LearningStyle] nvarchar(200) NULL,
    [CurrentLifeStage] nvarchar(200) NULL,
    [WeeklyAvailabilityHours] decimal(5,2) NULL,
    [MotivationScore] decimal(5,2) NULL,
    [ConfidenceScore] decimal(5,2) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_HumanProfile] PRIMARY KEY ([HumanProfileId]),
    CONSTRAINT [CK_HumanProfile_Scores] CHECK (([MotivationScore] IS NULL
    OR [MotivationScore] BETWEEN 0 AND 100)
AND
([ConfidenceScore] IS NULL
    OR [ConfidenceScore] BETWEEN 0 AND 100)),
    CONSTRAINT [CK_HumanProfile_WeeklyAvailabilityHours] CHECK ([WeeklyAvailabilityHours] IS NULL
OR [WeeklyAvailabilityHours] BETWEEN 0 AND 168),
    CONSTRAINT [FK_HumanProfile_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[HumanState] (
    [HumanStateId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Energy] int NULL,
    [Focus] int NULL,
    [Streak] int NOT NULL DEFAULT 0,
    [RecordedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_HumanState] PRIMARY KEY ([HumanStateId]),
    CONSTRAINT [FK_HumanState_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE CASCADE,
    CONSTRAINT [FK_HumanState_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[JobDescription] (
    [JobDescriptionId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [TenantId] uniqueidentifier NOT NULL,
    [PersonId] uniqueidentifier NOT NULL,
    [SourceStoragePath] nvarchar(1000) NOT NULL,
    [SourceFileName] nvarchar(400) NOT NULL,
    [SourceUploadedDate] datetime2 NOT NULL,
    [JobTitle] nvarchar(400) NOT NULL,
    [RolePurpose] nvarchar(2000) NULL,
    [RoleSummary] nvarchar(4000) NULL,
    [PrimaryResponsibilitiesJson] nvarchar(max) NOT NULL DEFAULT N'[]',
    [ExpectedOutcomesJson] nvarchar(max) NOT NULL DEFAULT N'[]',
    [RequiredExperience] nvarchar(2000) NULL,
    [ToolsMentionedJson] nvarchar(max) NOT NULL DEFAULT N'[]',
    [SuggestedProfessionalLevel] nvarchar(100) NULL,
    [ExtractionStatus] nvarchar(50) NOT NULL DEFAULT N'Pending',
    [ExtractionModel] nvarchar(100) NULL,
    [RawExtractionJson] nvarchar(max) NULL,
    [ExtractedDate] datetime2 NULL,
    [ConfirmedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_JobDescription] PRIMARY KEY ([JobDescriptionId]),
    CONSTRAINT [FK_JobDescription_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE CASCADE
);
GO


CREATE TABLE [LearningSessions] (
    [LearningSessionId] uniqueidentifier NOT NULL,
    [PersonId] uniqueidentifier NOT NULL,
    [CapabilityId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_LearningSessions] PRIMARY KEY ([LearningSessionId]),
    CONSTRAINT [FK_LearningSessions_Capability_CapabilityId] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LearningSessions_Person_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[PersonCapability] (
    [PersonCapabilityId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [CapabilityId] uniqueidentifier NOT NULL,
    [CurrentLevel] int NOT NULL DEFAULT 0,
    [TargetLevel] int NOT NULL DEFAULT 5,
    [ProgressPercentage] decimal(5,2) NOT NULL DEFAULT 0.0,
    [MasteryScore] decimal(5,2) NOT NULL DEFAULT 0.0,
    [Status] nvarchar(50) NOT NULL DEFAULT N'NotStarted',
    [IndependenceLevel] int NOT NULL DEFAULT 0,
    [RetentionScore] decimal(5,2) NULL,
    [ConfidenceScore] decimal(5,2) NULL,
    [KnowledgeScore] int NOT NULL DEFAULT 0,
    [RecallScore] int NOT NULL DEFAULT 0,
    [ApplicationScore] int NOT NULL DEFAULT 0,
    [StartedDate] datetime2 NULL,
    [LastActivityDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_PersonCapability] PRIMARY KEY ([PersonCapabilityId]),
    CONSTRAINT [CK_PersonCapability_ConfidenceScore] CHECK ([ConfidenceScore] IS NULL
OR [ConfidenceScore] BETWEEN 0 AND 100),
    CONSTRAINT [CK_PersonCapability_CurrentLevel] CHECK ([CurrentLevel] BETWEEN 0 AND 5),
    CONSTRAINT [CK_PersonCapability_IndependenceLevel] CHECK ([IndependenceLevel] BETWEEN 0 AND 5),
    CONSTRAINT [CK_PersonCapability_MasteryScore] CHECK ([MasteryScore] BETWEEN 0 AND 100),
    CONSTRAINT [CK_PersonCapability_ProgressPercentage] CHECK ([ProgressPercentage] BETWEEN 0 AND 100),
    CONSTRAINT [CK_PersonCapability_RetentionScore] CHECK ([RetentionScore] IS NULL
OR [RetentionScore] BETWEEN 0 AND 100),
    CONSTRAINT [CK_PersonCapability_Status] CHECK ([Status] IN
('NotStarted', 'InProgress', 'Paused', 'Completed')),
    CONSTRAINT [CK_PersonCapability_TargetLevel] CHECK ([TargetLevel] BETWEEN 0 AND 5),
    CONSTRAINT [FK_PersonCapability_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PersonCapability_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[PersonGoal] (
    [PersonGoalId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [GoalId] uniqueidentifier NOT NULL,
    [Status] nvarchar(100) NOT NULL DEFAULT N'Active',
    [ProgressPercentage] decimal(5,2) NOT NULL DEFAULT 0.0,
    [TargetDate] date NULL,
    [StartedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CompletedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_PersonGoal] PRIMARY KEY ([PersonGoalId]),
    CONSTRAINT [CK_PersonGoal_ProgressPercentage] CHECK ([ProgressPercentage] BETWEEN 0 AND 100),
    CONSTRAINT [CK_PersonGoal_Status] CHECK ([Status] IN
(
    'Active',
    'Paused',
    'Completed',
    'Abandoned'
)),
    CONSTRAINT [FK_PersonGoal_Goal] FOREIGN KEY ([GoalId]) REFERENCES [dbo].[Goal] ([GoalId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PersonGoal_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[AgentMessage] (
    [AgentMessageId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [AgentId] uniqueidentifier NOT NULL,
    [PersonId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Reason] nvarchar(max) NULL,
    [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_AgentMessage] PRIMARY KEY ([AgentMessageId]),
    CONSTRAINT [FK_AgentMessage_Agent] FOREIGN KEY ([AgentId]) REFERENCES [dbo].[Agent] ([AgentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AgentMessage_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE CASCADE,
    CONSTRAINT [FK_AgentMessage_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[AssessmentAttempt] (
    [AssessmentAttemptId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [AssessmentId] uniqueidentifier NOT NULL,
    [PersonId] uniqueidentifier NOT NULL,
    [Score] decimal(5,2) NULL,
    [AssistanceLevel] int NOT NULL DEFAULT 0,
    [StartedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CompletedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_AssessmentAttempt] PRIMARY KEY ([AssessmentAttemptId]),
    CONSTRAINT [CK_AssessmentAttempt_AssistanceLevel] CHECK ([AssistanceLevel] BETWEEN 0 AND 5),
    CONSTRAINT [CK_AssessmentAttempt_Score] CHECK ([Score] IS NULL
OR [Score] BETWEEN 0 AND 100),
    CONSTRAINT [FK_AssessmentAttempt_Assessment] FOREIGN KEY ([AssessmentId]) REFERENCES [dbo].[Assessment] ([AssessmentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssessmentAttempt_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CapabilityGraphNodes] (
    [CapabilityGraphNodeId] uniqueidentifier NOT NULL,
    [CapabilityGraphId] uniqueidentifier NOT NULL,
    [Name] nvarchar(500) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [NodeType] int NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [AcademicDefinition] nvarchar(max) NULL,
    [Interpretation] nvarchar(max) NULL,
    [ExamplesJson] nvarchar(max) NULL,
    [ApplicationsJson] nvarchar(max) NULL,
    [ReferencesJson] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_CapabilityGraphNodes] PRIMARY KEY ([CapabilityGraphNodeId]),
    CONSTRAINT [FK_CapabilityGraphNodes_CapabilityGraphs_CapabilityGraphId] FOREIGN KEY ([CapabilityGraphId]) REFERENCES [CapabilityGraphs] ([CapabilityGraphId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityModule] (
    [CapabilityModuleId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityLevelId] uniqueidentifier NOT NULL,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(400) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [Script] nvarchar(max) NOT NULL,
    [ReflectionPrompt] nvarchar(max) NOT NULL,
    [MetricRationale] nvarchar(max) NOT NULL,
    [RecallRequirement] nvarchar(max) NOT NULL,
    [LearnerProduction] nvarchar(max) NOT NULL,
    [LearnerTask] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityModule] PRIMARY KEY ([CapabilityModuleId]),
    CONSTRAINT [FK_CapabilityModule_CapabilityLevel] FOREIGN KEY ([CapabilityLevelId]) REFERENCES [dbo].[CapabilityLevel] ([CapabilityLevelId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[PersonProject] (
    [PersonProjectId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Status] nvarchar(100) NOT NULL DEFAULT N'NotStarted',
    [ProgressPercentage] decimal(5,2) NOT NULL DEFAULT 0.0,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_PersonProject] PRIMARY KEY ([PersonProjectId]),
    CONSTRAINT [CK_PersonProject_ProgressPercentage] CHECK ([ProgressPercentage] BETWEEN 0 AND 100),
    CONSTRAINT [CK_PersonProject_Status] CHECK ([Status] IN
(
    'NotStarted',
    'InProgress',
    'Paused',
    'Completed'
)),
    CONSTRAINT [FK_PersonProject_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PersonProject_Project] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Project] ([ProjectId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[ProjectTranslation] (
    [ProjectId] uniqueidentifier NOT NULL,
    [LanguageCode] nvarchar(10) NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Description] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ProjectTranslation] PRIMARY KEY ([ProjectId], [LanguageCode]),
    CONSTRAINT [FK_ProjectTranslation_Language] FOREIGN KEY ([LanguageCode]) REFERENCES [dbo].[Language] ([LanguageCode]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProjectTranslation_Project] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Project] ([ProjectId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[PersonProfile] (
    [PersonProfileId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(200) NULL,
    [LastName] nvarchar(200) NULL,
    [DisplayName] nvarchar(400) NULL,
    [PhoneNumber] nvarchar(100) NULL,
    [PreferredLanguage] nvarchar(10) NOT NULL DEFAULT N'en',
    [CountryCode] nvarchar(20) NULL,
    [TimeZone] nvarchar(200) NULL,
    [ProfilePhotoUrl] nvarchar(1000) NULL,
    [DateOfBirth] date NULL,
    [Occupation] nvarchar(400) NULL,
    [Company] nvarchar(400) NULL,
    [Biography] nvarchar(4000) NULL,
    [CurrentJobDescriptionId] uniqueidentifier NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_PersonProfile] PRIMARY KEY ([PersonProfileId]),
    CONSTRAINT [FK_PersonProfile_CurrentJobDescription] FOREIGN KEY ([CurrentJobDescriptionId]) REFERENCES [dbo].[JobDescription] ([JobDescriptionId]) ON DELETE SET NULL,
    CONSTRAINT [FK_PersonProfile_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE CASCADE,
    CONSTRAINT [FK_PersonProfile_PreferredLanguage] FOREIGN KEY ([PreferredLanguage]) REFERENCES [dbo].[Language] ([LanguageCode]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[CapabilityPractice] (
    [CapabilityPracticeId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonCapabilityId] uniqueidentifier NOT NULL,
    [PracticeType] nvarchar(50) NOT NULL,
    [AssistanceLevel] int NOT NULL DEFAULT 0,
    [PersonReflection] nvarchar(max) NULL,
    [LanguageCode] nvarchar(10) NOT NULL DEFAULT N'en',
    [PracticedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityPractice] PRIMARY KEY ([CapabilityPracticeId]),
    CONSTRAINT [CK_CapabilityPractice_AssistanceLevel] CHECK ([AssistanceLevel] BETWEEN 0 AND 5),
    CONSTRAINT [FK_CapabilityPractice_Language] FOREIGN KEY ([LanguageCode]) REFERENCES [dbo].[Language] ([LanguageCode]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CapabilityPractice_PersonCapability] FOREIGN KEY ([PersonCapabilityId]) REFERENCES [dbo].[PersonCapability] ([PersonCapabilityId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[RecallAttempt] (
    [RecallAttemptId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonCapabilityId] uniqueidentifier NOT NULL,
    [RecallPrompt] nvarchar(max) NOT NULL,
    [PersonResponse] nvarchar(max) NULL,
    [RecallScore] decimal(5,2) NULL,
    [ConfidenceScore] decimal(5,2) NULL,
    [AssistanceLevel] int NOT NULL DEFAULT 0,
    [LanguageCode] nvarchar(10) NOT NULL DEFAULT N'en',
    [AttemptedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_RecallAttempt] PRIMARY KEY ([RecallAttemptId]),
    CONSTRAINT [CK_RecallAttempt_AssistanceLevel] CHECK ([AssistanceLevel] BETWEEN 0 AND 5),
    CONSTRAINT [CK_RecallAttempt_ConfidenceScore] CHECK ([ConfidenceScore] IS NULL
OR [ConfidenceScore] BETWEEN 0 AND 100),
    CONSTRAINT [CK_RecallAttempt_RecallScore] CHECK ([RecallScore] IS NULL
OR [RecallScore] BETWEEN 0 AND 100),
    CONSTRAINT [FK_RecallAttempt_Language] FOREIGN KEY ([LanguageCode]) REFERENCES [dbo].[Language] ([LanguageCode]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RecallAttempt_PersonCapability] FOREIGN KEY ([PersonCapabilityId]) REFERENCES [dbo].[PersonCapability] ([PersonCapabilityId]) ON DELETE CASCADE
);
GO


CREATE TABLE [CapabilityGraphEdges] (
    [CapabilityGraphEdgeId] uniqueidentifier NOT NULL,
    [CapabilityGraphId] uniqueidentifier NOT NULL,
    [SourceNodeId] uniqueidentifier NOT NULL,
    [TargetNodeId] uniqueidentifier NOT NULL,
    [RelationshipType] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_CapabilityGraphEdges] PRIMARY KEY ([CapabilityGraphEdgeId]),
    CONSTRAINT [FK_CapabilityGraphEdges_CapabilityGraphNodes_SourceNodeId] FOREIGN KEY ([SourceNodeId]) REFERENCES [CapabilityGraphNodes] ([CapabilityGraphNodeId]),
    CONSTRAINT [FK_CapabilityGraphEdges_CapabilityGraphNodes_TargetNodeId] FOREIGN KEY ([TargetNodeId]) REFERENCES [CapabilityGraphNodes] ([CapabilityGraphNodeId]),
    CONSTRAINT [FK_CapabilityGraphEdges_CapabilityGraphs_CapabilityGraphId] FOREIGN KEY ([CapabilityGraphId]) REFERENCES [CapabilityGraphs] ([CapabilityGraphId]) ON DELETE CASCADE
);
GO


CREATE TABLE [CapabilityGraphNodeIllustrations] (
    [CapabilityGraphNodeIllustrationId] uniqueidentifier NOT NULL,
    [CapabilityGraphNodeId] uniqueidentifier NOT NULL,
    [StoragePath] nvarchar(1000) NOT NULL,
    [Prompt] nvarchar(max) NOT NULL,
    [Caption] nvarchar(2000) NULL,
    [ImageModel] nvarchar(200) NOT NULL,
    [Width] int NOT NULL,
    [Height] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_CapabilityGraphNodeIllustrations] PRIMARY KEY ([CapabilityGraphNodeIllustrationId]),
    CONSTRAINT [FK_CapabilityGraphNodeIllustrations_CapabilityGraphNodes_CapabilityGraphNodeId] FOREIGN KEY ([CapabilityGraphNodeId]) REFERENCES [CapabilityGraphNodes] ([CapabilityGraphNodeId]) ON DELETE CASCADE
);
GO


CREATE TABLE [NodeExperienceBlueprints] (
    [NodeExperienceBlueprintId] uniqueidentifier NOT NULL,
    [CapabilityGraphNodeId] uniqueidentifier NOT NULL,
    [Name] nvarchar(500) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [Version] int NOT NULL DEFAULT 1,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_NodeExperienceBlueprints] PRIMARY KEY ([NodeExperienceBlueprintId]),
    CONSTRAINT [FK_NodeExperienceBlueprints_CapabilityGraphNodes_CapabilityGraphNodeId] FOREIGN KEY ([CapabilityGraphNodeId]) REFERENCES [CapabilityGraphNodes] ([CapabilityGraphNodeId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityKnowledgeChunk] (
    [CapabilityKnowledgeChunkId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityId] uniqueidentifier NOT NULL,
    [CapabilityModuleId] uniqueidentifier NULL,
    [SortOrder] int NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Embedding] vector(1536) NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityKnowledgeChunk] PRIMARY KEY ([CapabilityKnowledgeChunkId]),
    CONSTRAINT [FK_CapabilityKnowledgeChunk_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CapabilityKnowledgeChunk_CapabilityModule] FOREIGN KEY ([CapabilityModuleId]) REFERENCES [dbo].[CapabilityModule] ([CapabilityModuleId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[CapabilityModuleChapter] (
    [CapabilityModuleChapterId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityModuleId] uniqueidentifier NOT NULL,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(400) NOT NULL,
    [TeachingContent] nvarchar(max) NOT NULL,
    [IsPrimaryWeight] bit NOT NULL,
    [RecallPrompt] nvarchar(max) NOT NULL,
    [IsCumulativeRecall] bit NOT NULL,
    [PredictionPrompt] nvarchar(max) NULL,
    [MiniPracticePrompt] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityModuleChapter] PRIMARY KEY ([CapabilityModuleChapterId]),
    CONSTRAINT [FK_CapabilityModuleChapter_CapabilityModule] FOREIGN KEY ([CapabilityModuleId]) REFERENCES [dbo].[CapabilityModule] ([CapabilityModuleId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityModuleMetric] (
    [CapabilityModuleId] uniqueidentifier NOT NULL,
    [Metric] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_CapabilityModuleMetric] PRIMARY KEY ([CapabilityModuleId], [Metric]),
    CONSTRAINT [FK_CapabilityModuleMetric_CapabilityModule] FOREIGN KEY ([CapabilityModuleId]) REFERENCES [dbo].[CapabilityModule] ([CapabilityModuleId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityModuleVerification] (
    [CapabilityModuleVerificationId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityModuleId] uniqueidentifier NOT NULL,
    [TargetMetric] nvarchar(50) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [Evidence] nvarchar(max) NOT NULL,
    [EvidenceLocation] nvarchar(max) NOT NULL,
    [Explanation] nvarchar(max) NOT NULL,
    [RecallStatus] nvarchar(50) NOT NULL,
    [RecallEvidence] nvarchar(max) NOT NULL,
    [RecallEvidenceLocation] nvarchar(max) NOT NULL,
    [RecallOccursBeforeInstruction] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_CapabilityModuleVerification] PRIMARY KEY ([CapabilityModuleVerificationId]),
    CONSTRAINT [FK_CapabilityModuleVerification_CapabilityModule] FOREIGN KEY ([CapabilityModuleId]) REFERENCES [dbo].[CapabilityModule] ([CapabilityModuleId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[Evidence] (
    [EvidenceId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [CapabilityId] uniqueidentifier NOT NULL,
    [PersonProjectId] uniqueidentifier NULL,
    [Title] nvarchar(400) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [EvidenceType] nvarchar(100) NOT NULL,
    [EvidenceUrl] nvarchar(2000) NULL,
    [ValidationStatus] nvarchar(30) NOT NULL DEFAULT N'Pending',
    [AssistanceLevel] int NOT NULL DEFAULT 0,
    [ValidationFeedback] nvarchar(max) NULL,
    [ValidatedDate] datetime2 NULL,
    [SubmittedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Evidence] PRIMARY KEY ([EvidenceId]),
    CONSTRAINT [CK_Evidence_AssistanceLevel] CHECK ([AssistanceLevel] BETWEEN 0 AND 5),
    CONSTRAINT [CK_Evidence_ValidationState] CHECK ((
    [ValidationStatus] = 'Pending'
    AND [ValidatedDate] IS NULL
)
OR
(
    [ValidationStatus] IN ('Accepted', 'Rejected')
    AND [ValidatedDate] IS NOT NULL
)),
    CONSTRAINT [CK_Evidence_ValidationStatus] CHECK ([ValidationStatus] IN
(
    'Pending',
    'Accepted',
    'Rejected'
)),
    CONSTRAINT [FK_Evidence_Capability] FOREIGN KEY ([CapabilityId]) REFERENCES [dbo].[Capability] ([CapabilityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Evidence_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Evidence_PersonProject] FOREIGN KEY ([PersonProjectId]) REFERENCES [dbo].[PersonProject] ([PersonProjectId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [dbo].[GrowthAction] (
    [GrowthActionId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [PersonId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [PersonCapabilityId] uniqueidentifier NOT NULL,
    [Title] nvarchar(255) NOT NULL,
    [Description] nvarchar(max) NULL,
    [ActionType] nvarchar(50) NOT NULL,
    [IsCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [ScheduledFor] date NULL,
    [RecallAttemptId] uniqueidentifier NULL,
    [PracticeId] uniqueidentifier NULL,
    [AssessmentId] uniqueidentifier NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_GrowthAction] PRIMARY KEY ([GrowthActionId]),
    CONSTRAINT [FK_GrowthAction_Assessment] FOREIGN KEY ([AssessmentId]) REFERENCES [dbo].[Assessment] ([AssessmentId]) ON DELETE SET NULL,
    CONSTRAINT [FK_GrowthAction_CapabilityPractice] FOREIGN KEY ([PracticeId]) REFERENCES [dbo].[CapabilityPractice] ([CapabilityPracticeId]) ON DELETE SET NULL,
    CONSTRAINT [FK_GrowthAction_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE CASCADE,
    CONSTRAINT [FK_GrowthAction_PersonCapability] FOREIGN KEY ([PersonCapabilityId]) REFERENCES [dbo].[PersonCapability] ([PersonCapabilityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GrowthAction_RecallAttempt] FOREIGN KEY ([RecallAttemptId]) REFERENCES [dbo].[RecallAttempt] ([RecallAttemptId]) ON DELETE SET NULL,
    CONSTRAINT [FK_GrowthAction_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [BlueprintValidations] (
    [BlueprintValidationId] uniqueidentifier NOT NULL,
    [NodeExperienceBlueprintId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [Score] int NOT NULL,
    [InputTokens] int NOT NULL,
    [OutputTokens] int NOT NULL,
    [TotalTokens] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_BlueprintValidations] PRIMARY KEY ([BlueprintValidationId]),
    CONSTRAINT [FK_BlueprintValidations_NodeExperienceBlueprints_NodeExperienceBlueprintId] FOREIGN KEY ([NodeExperienceBlueprintId]) REFERENCES [NodeExperienceBlueprints] ([NodeExperienceBlueprintId]) ON DELETE CASCADE
);
GO


CREATE TABLE [LearningSessionNodes] (
    [LearningSessionNodeId] uniqueidentifier NOT NULL,
    [LearningSessionId] uniqueidentifier NOT NULL,
    [CapabilityGraphNodeId] uniqueidentifier NOT NULL,
    [NodeExperienceBlueprintId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_LearningSessionNodes] PRIMARY KEY ([LearningSessionNodeId]),
    CONSTRAINT [FK_LearningSessionNodes_CapabilityGraphNodes_CapabilityGraphNodeId] FOREIGN KEY ([CapabilityGraphNodeId]) REFERENCES [CapabilityGraphNodes] ([CapabilityGraphNodeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LearningSessionNodes_LearningSessions_LearningSessionId] FOREIGN KEY ([LearningSessionId]) REFERENCES [LearningSessions] ([LearningSessionId]) ON DELETE CASCADE,
    CONSTRAINT [FK_LearningSessionNodes_NodeExperienceBlueprints_NodeExperienceBlueprintId] FOREIGN KEY ([NodeExperienceBlueprintId]) REFERENCES [NodeExperienceBlueprints] ([NodeExperienceBlueprintId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [NodeExperienceBlueprintSteps] (
    [NodeExperienceBlueprintStepId] uniqueidentifier NOT NULL,
    [NodeExperienceBlueprintId] uniqueidentifier NOT NULL,
    [StepType] int NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [ReferencedIllustrationIdsJson] nvarchar(max) NULL,
    [SortOrder] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_NodeExperienceBlueprintSteps] PRIMARY KEY ([NodeExperienceBlueprintStepId]),
    CONSTRAINT [FK_NodeExperienceBlueprintSteps_NodeExperienceBlueprints_NodeExperienceBlueprintId] FOREIGN KEY ([NodeExperienceBlueprintId]) REFERENCES [NodeExperienceBlueprints] ([NodeExperienceBlueprintId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityModuleSuccessCriterionResult] (
    [CapabilityModuleSuccessCriterionResultId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
    [CapabilityModuleVerificationId] uniqueidentifier NOT NULL,
    [SortOrder] int NOT NULL,
    [Criterion] nvarchar(max) NOT NULL,
    [IsSatisfied] bit NOT NULL,
    [Evidence] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CapabilityModuleSuccessCriterionResult] PRIMARY KEY ([CapabilityModuleSuccessCriterionResultId]),
    CONSTRAINT [FK_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerification] FOREIGN KEY ([CapabilityModuleVerificationId]) REFERENCES [dbo].[CapabilityModuleVerification] ([CapabilityModuleVerificationId]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[CapabilityEvidence] (
    [CapabilityEvidenceId] uniqueidentifier NOT NULL,
    [PersonCapabilityId] uniqueidentifier NOT NULL,
    [EvidenceId] uniqueidentifier NOT NULL,
    [EvidenceType] nvarchar(50) NOT NULL,
    [ContributionWeight] decimal(5,2) NULL,
    [ValidationStatus] nvarchar(30) NOT NULL DEFAULT N'Pending',
    [ValidatedByPersonId] uniqueidentifier NULL,
    [ValidatedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [EvidenceId1] uniqueidentifier NULL,
    [PersonCapabilityId1] uniqueidentifier NULL,
    CONSTRAINT [PK_CapabilityEvidence] PRIMARY KEY ([CapabilityEvidenceId]),
    CONSTRAINT [FK_CapabilityEvidence_Evidence] FOREIGN KEY ([EvidenceId]) REFERENCES [dbo].[Evidence] ([EvidenceId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CapabilityEvidence_Evidence_EvidenceId1] FOREIGN KEY ([EvidenceId1]) REFERENCES [dbo].[Evidence] ([EvidenceId]),
    CONSTRAINT [FK_CapabilityEvidence_PersonCapability] FOREIGN KEY ([PersonCapabilityId]) REFERENCES [dbo].[PersonCapability] ([PersonCapabilityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CapabilityEvidence_PersonCapability_PersonCapabilityId1] FOREIGN KEY ([PersonCapabilityId1]) REFERENCES [dbo].[PersonCapability] ([PersonCapabilityId]),
    CONSTRAINT [FK_CapabilityEvidence_Validator] FOREIGN KEY ([ValidatedByPersonId]) REFERENCES [dbo].[Person] ([PersonId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [BlueprintValidationIssues] (
    [BlueprintValidationIssueId] uniqueidentifier NOT NULL,
    [BlueprintValidationId] uniqueidentifier NOT NULL,
    [Severity] int NOT NULL,
    [Area] int NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_BlueprintValidationIssues] PRIMARY KEY ([BlueprintValidationIssueId]),
    CONSTRAINT [FK_BlueprintValidationIssues_BlueprintValidations_BlueprintValidationId] FOREIGN KEY ([BlueprintValidationId]) REFERENCES [BlueprintValidations] ([BlueprintValidationId]) ON DELETE CASCADE
);
GO


CREATE TABLE [BlueprintValidationMetrics] (
    [BlueprintValidationMetricId] uniqueidentifier NOT NULL,
    [BlueprintValidationId] uniqueidentifier NOT NULL,
    [MetricName] nvarchar(200) NOT NULL,
    [MetricValue] int NOT NULL,
    CONSTRAINT [PK_BlueprintValidationMetrics] PRIMARY KEY ([BlueprintValidationMetricId]),
    CONSTRAINT [FK_BlueprintValidationMetrics_BlueprintValidations_BlueprintValidationId] FOREIGN KEY ([BlueprintValidationId]) REFERENCES [BlueprintValidations] ([BlueprintValidationId]) ON DELETE CASCADE
);
GO


CREATE TABLE [LearningAssessmentResults] (
    [LearningAssessmentResultId] uniqueidentifier NOT NULL,
    [LearningSessionNodeId] uniqueidentifier NOT NULL,
    [Score] int NOT NULL,
    [Passed] bit NOT NULL,
    [Feedback] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_LearningAssessmentResults] PRIMARY KEY ([LearningAssessmentResultId]),
    CONSTRAINT [FK_LearningAssessmentResults_LearningSessionNodes_LearningSessionNodeId] FOREIGN KEY ([LearningSessionNodeId]) REFERENCES [LearningSessionNodes] ([LearningSessionNodeId]) ON DELETE CASCADE
);
GO


CREATE TABLE [LearningSessionSteps] (
    [LearningSessionStepId] uniqueidentifier NOT NULL,
    [LearningSessionNodeId] uniqueidentifier NOT NULL,
    [StepType] int NOT NULL,
    [Status] int NOT NULL,
    [StartedDate] datetime2 NULL,
    [CompletedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_LearningSessionSteps] PRIMARY KEY ([LearningSessionStepId]),
    CONSTRAINT [FK_LearningSessionSteps_LearningSessionNodes_LearningSessionNodeId] FOREIGN KEY ([LearningSessionNodeId]) REFERENCES [LearningSessionNodes] ([LearningSessionNodeId]) ON DELETE CASCADE
);
GO


CREATE TABLE [LearningEvidences] (
    [LearningEvidenceId] uniqueidentifier NOT NULL,
    [LearningSessionStepId] uniqueidentifier NOT NULL,
    [StudentResponse] nvarchar(max) NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_LearningEvidences] PRIMARY KEY ([LearningEvidenceId]),
    CONSTRAINT [FK_LearningEvidences_LearningSessionSteps_LearningSessionStepId] FOREIGN KEY ([LearningSessionStepId]) REFERENCES [LearningSessionSteps] ([LearningSessionStepId]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [UX_Agent_CapabilityId] ON [dbo].[Agent] ([CapabilityId]);
GO


CREATE INDEX [IX_AgentMessage_AgentId] ON [dbo].[AgentMessage] ([AgentId]);
GO


CREATE INDEX [IX_AgentMessage_PersonId_CreatedDate] ON [dbo].[AgentMessage] ([PersonId], [CreatedDate]);
GO


CREATE INDEX [IX_AgentMessage_TenantId] ON [dbo].[AgentMessage] ([TenantId]);
GO


CREATE INDEX [IX_Assessment_CapabilityId] ON [dbo].[Assessment] ([CapabilityId]);
GO


CREATE INDEX [IX_AssessmentAttempt_AssessmentId] ON [dbo].[AssessmentAttempt] ([AssessmentId]);
GO


CREATE INDEX [IX_AssessmentAttempt_PersonId] ON [dbo].[AssessmentAttempt] ([PersonId]);
GO


CREATE INDEX [IX_BlueprintValidationIssues_BlueprintValidationId] ON [BlueprintValidationIssues] ([BlueprintValidationId]);
GO


CREATE INDEX [IX_BlueprintValidationMetrics_BlueprintValidationId] ON [BlueprintValidationMetrics] ([BlueprintValidationId]);
GO


CREATE INDEX [IX_BlueprintValidations_NodeExperienceBlueprintId] ON [BlueprintValidations] ([NodeExperienceBlueprintId]);
GO


CREATE INDEX [IX_Capability_CapabilityDomainId] ON [dbo].[Capability] ([CapabilityDomainId]);
GO


CREATE UNIQUE INDEX [UX_Capability_Code] ON [dbo].[Capability] ([Code]);
GO


CREATE UNIQUE INDEX [UX_CapabilityDomain_Code] ON [dbo].[CapabilityDomain] ([Code]);
GO


CREATE INDEX [IX_CapabilityDomainTranslation_LanguageCode] ON [dbo].[CapabilityDomainTranslation] ([LanguageCode]);
GO


CREATE INDEX [IX_CapabilityEvidence_EvidenceId] ON [dbo].[CapabilityEvidence] ([EvidenceId]);
GO


CREATE INDEX [IX_CapabilityEvidence_EvidenceId1] ON [dbo].[CapabilityEvidence] ([EvidenceId1]);
GO


CREATE INDEX [IX_CapabilityEvidence_PersonCapabilityId1] ON [dbo].[CapabilityEvidence] ([PersonCapabilityId1]);
GO


CREATE INDEX [IX_CapabilityEvidence_ValidatedByPersonId] ON [dbo].[CapabilityEvidence] ([ValidatedByPersonId]);
GO


CREATE UNIQUE INDEX [UQ_CapabilityEvidence] ON [dbo].[CapabilityEvidence] ([PersonCapabilityId], [EvidenceId]);
GO


CREATE INDEX [IX_CapabilityGraphEdges_CapabilityGraphId] ON [CapabilityGraphEdges] ([CapabilityGraphId]);
GO


CREATE INDEX [IX_CapabilityGraphEdges_SourceNodeId_TargetNodeId] ON [CapabilityGraphEdges] ([SourceNodeId], [TargetNodeId]);
GO


CREATE INDEX [IX_CapabilityGraphEdges_TargetNodeId] ON [CapabilityGraphEdges] ([TargetNodeId]);
GO


CREATE INDEX [IX_CapabilityGraphNodeIllustrations_CapabilityGraphNodeId] ON [CapabilityGraphNodeIllustrations] ([CapabilityGraphNodeId]);
GO


CREATE UNIQUE INDEX [IX_CapabilityGraphNodes_CapabilityGraphId_Name] ON [CapabilityGraphNodes] ([CapabilityGraphId], [Name]);
GO


CREATE INDEX [IX_CapabilityGraphNodes_CapabilityGraphId_SortOrder] ON [CapabilityGraphNodes] ([CapabilityGraphId], [SortOrder]);
GO


CREATE UNIQUE INDEX [IX_CapabilityGraphs_CapabilityId] ON [CapabilityGraphs] ([CapabilityId]);
GO


CREATE INDEX [IX_CapabilityKnowledgeChunk_CapabilityId] ON [dbo].[CapabilityKnowledgeChunk] ([CapabilityId]);
GO


CREATE INDEX [IX_CapabilityKnowledgeChunk_CapabilityModuleId] ON [dbo].[CapabilityKnowledgeChunk] ([CapabilityModuleId]);
GO


CREATE INDEX [IX_CapabilityLevel_CapabilityId] ON [dbo].[CapabilityLevel] ([CapabilityId]);
GO


CREATE INDEX [IX_CapabilityModule_CapabilityLevelId] ON [dbo].[CapabilityModule] ([CapabilityLevelId]);
GO


CREATE INDEX [IX_CapabilityModuleChapter_CapabilityModuleId] ON [dbo].[CapabilityModuleChapter] ([CapabilityModuleId]);
GO


CREATE INDEX [IX_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerificationId] ON [dbo].[CapabilityModuleSuccessCriterionResult] ([CapabilityModuleVerificationId]);
GO


CREATE INDEX [IX_CapabilityModuleVerification_CapabilityModuleId] ON [dbo].[CapabilityModuleVerification] ([CapabilityModuleId]);
GO


CREATE INDEX [IX_CapabilityPractice_LanguageCode] ON [dbo].[CapabilityPractice] ([LanguageCode]);
GO


CREATE INDEX [IX_CapabilityPractice_PersonCapabilityId] ON [dbo].[CapabilityPractice] ([PersonCapabilityId]);
GO


CREATE INDEX [IX_CapabilityTranslation_LanguageCode] ON [dbo].[CapabilityTranslation] ([LanguageCode]);
GO


CREATE INDEX [IX_Evidence_CapabilityId] ON [dbo].[Evidence] ([CapabilityId]);
GO


CREATE INDEX [IX_Evidence_PersonId] ON [dbo].[Evidence] ([PersonId]);
GO


CREATE INDEX [IX_Evidence_PersonProjectId] ON [dbo].[Evidence] ([PersonProjectId]);
GO


CREATE INDEX [IX_GoalCapability_CapabilityId] ON [dbo].[GoalCapability] ([CapabilityId]);
GO


CREATE INDEX [IX_GoalTranslation_LanguageCode] ON [dbo].[GoalTranslation] ([LanguageCode]);
GO


CREATE INDEX [IX_GrowthAction_AssessmentId] ON [dbo].[GrowthAction] ([AssessmentId]);
GO


CREATE INDEX [IX_GrowthAction_PersonCapabilityId] ON [dbo].[GrowthAction] ([PersonCapabilityId]);
GO


CREATE INDEX [IX_GrowthAction_PersonId_ScheduledFor] ON [dbo].[GrowthAction] ([PersonId], [ScheduledFor]);
GO


CREATE INDEX [IX_GrowthAction_PracticeId] ON [dbo].[GrowthAction] ([PracticeId]);
GO


CREATE INDEX [IX_GrowthAction_RecallAttemptId] ON [dbo].[GrowthAction] ([RecallAttemptId]);
GO


CREATE INDEX [IX_GrowthAction_TenantId] ON [dbo].[GrowthAction] ([TenantId]);
GO


CREATE UNIQUE INDEX [UX_HumanProfile_PersonId] ON [dbo].[HumanProfile] ([PersonId]);
GO


CREATE INDEX [IX_HumanState_PersonId_RecordedAt] ON [dbo].[HumanState] ([PersonId], [RecordedAt]);
GO


CREATE INDEX [IX_HumanState_TenantId] ON [dbo].[HumanState] ([TenantId]);
GO


CREATE INDEX [IX_JobDescription_PersonId] ON [dbo].[JobDescription] ([PersonId]);
GO


CREATE INDEX [IX_JobDescription_TenantId] ON [dbo].[JobDescription] ([TenantId]);
GO


CREATE INDEX [IX_LearningAssessmentResults_LearningSessionNodeId] ON [LearningAssessmentResults] ([LearningSessionNodeId]);
GO


CREATE INDEX [IX_LearningEvidences_LearningSessionStepId] ON [LearningEvidences] ([LearningSessionStepId]);
GO


CREATE INDEX [IX_LearningSessionNodes_CapabilityGraphNodeId] ON [LearningSessionNodes] ([CapabilityGraphNodeId]);
GO


CREATE INDEX [IX_LearningSessionNodes_LearningSessionId] ON [LearningSessionNodes] ([LearningSessionId]);
GO


CREATE INDEX [IX_LearningSessionNodes_NodeExperienceBlueprintId] ON [LearningSessionNodes] ([NodeExperienceBlueprintId]);
GO


CREATE INDEX [IX_LearningSessions_CapabilityId] ON [LearningSessions] ([CapabilityId]);
GO


CREATE INDEX [IX_LearningSessions_PersonId] ON [LearningSessions] ([PersonId]);
GO


CREATE INDEX [IX_LearningSessions_PersonId_CapabilityId] ON [LearningSessions] ([PersonId], [CapabilityId]);
GO


CREATE UNIQUE INDEX [IX_LearningSessionSteps_LearningSessionNodeId_StepType] ON [LearningSessionSteps] ([LearningSessionNodeId], [StepType]);
GO


CREATE INDEX [IX_NodeExperienceBlueprints_CapabilityGraphNodeId] ON [NodeExperienceBlueprints] ([CapabilityGraphNodeId]);
GO


CREATE UNIQUE INDEX [IX_NodeExperienceBlueprints_CapabilityGraphNodeId_Name_Version] ON [NodeExperienceBlueprints] ([CapabilityGraphNodeId], [Name], [Version]);
GO


CREATE INDEX [IX_NodeExperienceBlueprintSteps_NodeExperienceBlueprintId_SortOrder] ON [NodeExperienceBlueprintSteps] ([NodeExperienceBlueprintId], [SortOrder]);
GO


CREATE UNIQUE INDEX [IX_NodeExperienceBlueprintSteps_NodeExperienceBlueprintId_StepType] ON [NodeExperienceBlueprintSteps] ([NodeExperienceBlueprintId], [StepType]);
GO


CREATE INDEX [IX_Person_TenantId] ON [dbo].[Person] ([TenantId]);
GO


CREATE UNIQUE INDEX [UX_Person_AzureTid_AzureOid] ON [dbo].[Person] ([AzureTid], [AzureOid]);
GO


CREATE INDEX [IX_PersonCapability_CapabilityId] ON [dbo].[PersonCapability] ([CapabilityId]);
GO


CREATE UNIQUE INDEX [UX_PersonCapability_PersonId_CapabilityId] ON [dbo].[PersonCapability] ([PersonId], [CapabilityId]);
GO


CREATE INDEX [IX_PersonGoal_GoalId] ON [dbo].[PersonGoal] ([GoalId]);
GO


CREATE UNIQUE INDEX [UX_PersonGoal_PersonId_GoalId] ON [dbo].[PersonGoal] ([PersonId], [GoalId]);
GO


CREATE INDEX [IX_PersonProfile_CurrentJobDescriptionId] ON [dbo].[PersonProfile] ([CurrentJobDescriptionId]);
GO


CREATE INDEX [IX_PersonProfile_PreferredLanguage] ON [dbo].[PersonProfile] ([PreferredLanguage]);
GO


CREATE UNIQUE INDEX [UX_PersonProfile_PersonId] ON [dbo].[PersonProfile] ([PersonId]);
GO


CREATE INDEX [IX_PersonProject_ProjectId] ON [dbo].[PersonProject] ([ProjectId]);
GO


CREATE UNIQUE INDEX [UX_PersonProject_PersonId_ProjectId] ON [dbo].[PersonProject] ([PersonId], [ProjectId]);
GO


CREATE INDEX [IX_Project_CapabilityId] ON [dbo].[Project] ([CapabilityId]);
GO


CREATE INDEX [IX_ProjectTranslation_LanguageCode] ON [dbo].[ProjectTranslation] ([LanguageCode]);
GO


CREATE INDEX [IX_RecallAttempt_LanguageCode] ON [dbo].[RecallAttempt] ([LanguageCode]);
GO


CREATE INDEX [IX_RecallAttempt_PersonCapabilityId] ON [dbo].[RecallAttempt] ([PersonCapabilityId]);
GO


CREATE UNIQUE INDEX [UX_RuntimeWorkflowCheckpoint_SessionId_CheckpointId] ON [dbo].[RuntimeWorkflowCheckpoint] ([SessionId], [CheckpointId]);
GO


CREATE UNIQUE INDEX [UX_Tenant_AzureTenantId] ON [dbo].[Tenant] ([AzureTenantId]) WHERE [AzureTenantId] IS NOT NULL;
GO


CREATE UNIQUE INDEX [UX_Tenant_Slug] ON [dbo].[Tenant] ([Slug]);
GO


