-- Deletes ONLY rows (no tables/schema touched) for these 6 Structured Learning
-- documents shown on http://localhost:3010/ and everything that references
-- them, in FK-safe order. Does not touch Azure Data Lake blob images.
SET NOCOUNT ON;

DECLARE @DocIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @DocIds (Id) VALUES
    ('a60ca9af-4c50-4610-ab55-bee96b148702'), -- AlgebraNumerosImaginarios.pdf
    ('c4812040-b9e6-4803-a937-0146c2e8bbf6'), -- CursoAlgebra.pdf
    ('c22e47e2-9db5-4495-99ec-d01eda16a4f6'), -- Heaven Volunteer Orientation Policy demo Small.pdf
    ('f67ab5f8-fa7c-48c5-87f4-ff4065980732'), -- Heaven Volunteer Orientation Policy demo Small.pdf
    ('a141180b-e350-44e6-863f-b14b06a5e1ea'), -- Heaven Volunteer Orientation Policy demo Small.pdf
    ('72f35082-9c21-49ee-8c95-015665da2467'); -- Heaven Volunteer Orientation Policy demo

DECLARE @Sections TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Sections (Id)
SELECT StructuredLearningDocumentSectionId
FROM dbo.StructuredLearningDocumentSection
WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

DECLARE @Rounds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Rounds (Id)
SELECT StructuredLearningAssessmentRoundId
FROM dbo.StructuredLearningAssessmentRound
WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @Sections);

DELETE FROM dbo.StructuredLearningAssessmentQuestionAttempt WHERE StructuredLearningAssessmentRoundId IN (SELECT Id FROM @Rounds);
DELETE FROM dbo.StructuredLearningAssessmentRound WHERE Id IN (SELECT Id FROM @Rounds);
DELETE FROM dbo.StructuredLearningRecallAttempt WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @Sections);
DELETE FROM dbo.StructuredLearningPersonSectionState WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @Sections);
DELETE FROM dbo.StructuredLearningIllustration WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @Sections);
DELETE FROM dbo.StructuredLearningSectionStep WHERE StructuredLearningDocumentSectionId IN (SELECT Id FROM @Sections);
DELETE FROM dbo.StructuredLearningConnection WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);
DELETE FROM dbo.StructuredLearningEntity WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);
DELETE FROM dbo.StructuredLearningDocumentProgramMembership WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);
DELETE FROM dbo.StructuredLearningDocumentEnrollment WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);
DELETE FROM dbo.StructuredLearningDocumentSection WHERE Id IN (SELECT Id FROM @Sections);
DELETE FROM dbo.StructuredLearningDocument WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);

SELECT 'Remaining matching documents' AS Check1, COUNT(*) AS Cnt FROM dbo.StructuredLearningDocument WHERE StructuredLearningDocumentId IN (SELECT Id FROM @DocIds);
