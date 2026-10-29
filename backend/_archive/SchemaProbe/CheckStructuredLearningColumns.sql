SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME IN ('StructuredLearningDocument','StructuredLearningDocumentSection','StructuredLearningSectionStep','StructuredLearningIllustration')
  AND COLUMN_NAME IN ('PromptVersion','ProcessVersion','ModelName','PromptHash','ConfigSnapshotJson')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
