-- Deletes ONLY the SQL rows for the specified Structured Learning documents
-- (created via /new-structured-learning). Does NOT drop any table and does
-- NOT touch blob storage (illustration images stay in Data Lake as orphans
-- unless separately cleaned up).
SET NOCOUNT ON;

DECLARE @DocIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @DocIds (Id) VALUES
    ('a60ca9af-4c50-4610-ab55-bee96b148702'), -- AlgebraNumerosImaginarios.pdf
    ('c4812040-b9e6-4803-a937-0146c2e8bbf6'), -- CursoAlgebra.pdf
    ('c22e47e2-9db5-4495-99ec-d01eda16a4f6'), -- Heaven Volunteer Orientation Policy demo Small.pdf (1:16 AM)
    ('f67ab5f8-fa7c-48c5-87f4-ff4065980732'), -- Heaven Volunteer Orientation Policy demo Small.pdf (12:57 AM)
    ('a141180b-e350-44e6-863f-b14b06a5e1ea'), -- Heaven Volunteer Orientation Policy demo Small.pdf (12:39 AM)
    ('72f35082-9c21-49ee-8c95-015665da2467'); -- Heaven Volunteer Orientation Policy demo

DECLARE @SectionIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @SectionIds (Id)
SELECT StructuredLearningDocumentSectionId
FROM dbo.StructuredLearningDocumentSection
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

-- No FK declared from Illustration -> Section, so it must be deleted explicitly.
DELETE FROM dbo.StructuredLearningIllustration
WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @SectionIds);

-- Restrict delete behavior on Document -> Enrollment; must go first.
DELETE FROM dbo.StructuredLearningDocumentEnrollment
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

-- Everything below cascades from StructuredLearningDocument, but deleted
-- explicitly here for a clear, auditable script (and in case cascade paths
-- change later): Sections -> Steps/AssessmentRounds(+Questions)/RecallAttempts/
-- PersonSectionStates, Entities, Connections, ProgramMemberships.
DELETE FROM dbo.StructuredLearningAssessmentQuestionAttempt
WHERE StructuredLearningAssessmentRoundId IN (
    SELECT StructuredLearningAssessmentRoundId FROM dbo.StructuredLearningAssessmentRound
    WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @SectionIds));

DELETE FROM dbo.StructuredLearningAssessmentRound
WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @SectionIds);

DELETE FROM dbo.StructuredLearningRecallAttempt
WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @SectionIds);

DELETE FROM dbo.StructuredLearningPersonSectionState
WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @SectionIds);

DELETE FROM dbo.StructuredLearningSectionStep
WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @SectionIds);

DELETE FROM dbo.StructuredLearningConnection
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

DELETE FROM dbo.StructuredLearningEntity
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

DELETE FROM dbo.StructuredLearningDocumentProgramMembership
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

DELETE FROM dbo.StructuredLearningDocumentSection
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

DELETE FROM dbo.StructuredLearningDocument
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

SELECT 'Deleted structured learning documents' AS Result, COUNT(*) AS Requested FROM @DocIds;
