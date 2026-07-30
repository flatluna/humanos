DECLARE @SessionId UNIQUEIDENTIFIER, @NodeId UNIQUEIDENTIFIER, @BlueprintId UNIQUEIDENTIFIER;
SELECT @SessionId = LearningSessionId, @NodeId = CapabilityGraphNodeId, @BlueprintId = NodeExperienceBlueprintId
FROM LearningSessionNodes WHERE LearningSessionNodeId = '24963BEB-96BC-42BA-A035-4FE71FCA2EAC';

DECLARE @NewNode1 UNIQUEIDENTIFIER = NEWID(), @NewNode2 UNIQUEIDENTIFIER = NEWID(), @NewNode3 UNIQUEIDENTIFIER = NEWID(), @NewNode4 UNIQUEIDENTIFIER = NEWID();

INSERT INTO LearningSessionNodes (LearningSessionNodeId, LearningSessionId, CapabilityGraphNodeId, NodeExperienceBlueprintId, Status, StartedDate, CompletedDate, CreatedDate)
VALUES
(@NewNode1, @SessionId, @NodeId, @BlueprintId, 2, DATEADD(day,-121,SYSUTCDATETIME()), DATEADD(day,-120,SYSUTCDATETIME()), DATEADD(day,-120,SYSUTCDATETIME())),
(@NewNode2, @SessionId, @NodeId, @BlueprintId, 2, DATEADD(day,-101,SYSUTCDATETIME()), DATEADD(day,-100,SYSUTCDATETIME()), DATEADD(day,-100,SYSUTCDATETIME())),
(@NewNode3, @SessionId, @NodeId, @BlueprintId, 2, DATEADD(day,-81,SYSUTCDATETIME()),  DATEADD(day,-80,SYSUTCDATETIME()),  DATEADD(day,-80,SYSUTCDATETIME())),
(@NewNode4, @SessionId, @NodeId, @BlueprintId, 2, DATEADD(day,-71,SYSUTCDATETIME()),  DATEADD(day,-70,SYSUTCDATETIME()),  DATEADD(day,-70,SYSUTCDATETIME()));

INSERT INTO LearningAssessmentResults (LearningAssessmentResultId, LearningSessionNodeId, Score, Passed, Feedback, CreatedDate)
VALUES
(NEWID(), @NewNode1, 92, 1, 'Demo review 1 (automaticity seed)', DATEADD(day,-120,SYSUTCDATETIME())),
(NEWID(), @NewNode2, 95, 1, 'Demo review 2 (automaticity seed)', DATEADD(day,-100,SYSUTCDATETIME())),
(NEWID(), @NewNode3, 88, 1, 'Demo review 3 (automaticity seed)', DATEADD(day,-80,SYSUTCDATETIME())),
(NEWID(), @NewNode4, 97, 1, 'Demo review 4 (automaticity seed)', DATEADD(day,-70,SYSUTCDATETIME()));

SELECT COUNT(*) AS TotalCompletedPassedReviews
FROM LearningSessionNodes n
JOIN LearningAssessmentResults a ON a.LearningSessionNodeId = n.LearningSessionNodeId
WHERE n.CapabilityGraphNodeId = @NodeId AND n.Status = 2 AND a.Passed = 1;
