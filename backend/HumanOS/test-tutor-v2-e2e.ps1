# TutorAgent V2 - Live E2E test.
# Reuses the same Person/Capability/CapabilityGraphNode already validated by
# test-paso4-runtime-http-api.ps1 (see CURADOR_GRAPHARCHITECT_RESULTS.txt).
# StartSession always creates a brand-new LearningSession/Node, so this is
# safe to re-run against the same node.
#
# Flow (ONE full node, exercising all 4 TutorAgentV2 modes):
#   StartSession -> Hypothesis (no Tutor, submit+advance)
#   -> Teaching (TutorAsk mode=Teaching) -> advance
#   -> Recall (TutorSubmitRecallAttempt, loop until Advanced=true, mastery gate)
#   -> Production (TutorAsk mode=Production) -> submit+advance
#   -> Assessment (submit+evaluate via AssessmentEvaluator) -> TutorAsk mode=AssessmentFeedback
#   -> CompleteNode

$ErrorActionPreference = "Stop"
$base = "http://localhost:7071/api/instructor-runtime"
$log = @()

function Log($msg) {
    Write-Host $msg
    $script:log += $msg
}

# Windows PowerShell 5.1's Invoke-RestMethod does not reliably send accented
# (non-ASCII, e.g. Spanish tildes/n-with-tilde/inverted punctuation) JSON
# string bodies as valid UTF-8 bytes on its own -- the server's strict UTF-8
# JSON parser then fails with a JsonException, surfaced as a fast (~6ms,
# no DB/LLM call) 400 "InvalidJson". Fix: always convert the JSON string to
# explicit UTF-8 bytes before passing it as -Body.
function PostJson($uri, $bodyObject) {
    $json = $bodyObject | ConvertTo-Json
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    return Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json; charset=utf-8" -Body $bytes
}

# Downloads the real image bytes for each illustration a TutorAsk response
# referenced (via the new GetIllustrationImage endpoint) and saves them
# next to this script, so the actual picture (not just its StoragePath/
# Caption metadata) can be opened and viewed, not just read as JSON.
# -UseBasicParsing is required in Windows PowerShell 5.1 or Invoke-WebRequest
# tries to parse the binary body as HTML and throws a misleading "connection
# forcibly closed" error (see /memories/powershell-gotchas.md).
function SaveIllustrations($illustrations, $filePrefix) {
    $savedPaths = @()
    $i = 0
    foreach ($illustration in $illustrations) {
        $i++
        $outPath = "$PSScriptRoot\$filePrefix-$i.png"
        $imgResp = Invoke-WebRequest -Uri "http://localhost:7071/api/illustrations/$($illustration.illustrationId)/image" -Method Get -UseBasicParsing
        [System.IO.File]::WriteAllBytes($outPath, $imgResp.Content)
        $savedPaths += $outPath
    }
    return $savedPaths
}

$personId = "22e52050-2b03-475f-93b9-880b81e50663"
$capabilityId = "523f53da-a181-4801-a6c6-1b349a5e593c"
$capabilityGraphNodeId = "ac8ed53b-8e8a-4173-aa46-643a3ee729be"

try {
    # 1. START SESSION
    $session = PostJson "$base/sessions/start" @{ personId = $personId; capabilityId = $capabilityId; capabilityGraphNodeId = $capabilityGraphNodeId }
    Log "1. START SESSION -> LearningSessionNodeId=$($session.learningSessionNodeId)"

    $learningSessionNodeId = $session.learningSessionNodeId
    $currentStepId = $session.currentStep.learningSessionStepId

    # 2. HYPOTHESIS - no Tutor involvement by design
    PostJson "$base/steps/respond" @{ learningSessionStepId = $currentStepId; response = "Creo que el grupo B tiene mas objetos porque parece mas denso." } | Out-Null
    $advanced = PostJson "$base/steps/advance" @{ learningSessionNodeId = $learningSessionNodeId }
    Log "2. HYPOTHESIS submitted, advanced -> $($advanced.stepType)"
    $currentStepId = $advanced.learningSessionStepId

    # 3. TEACHING - TutorAsk mode=Teaching (on-demand help, no persistence)
    $tutorTeaching = PostJson "$base/tutor/ask" @{ learningSessionStepId = $currentStepId; mode = "Teaching"; studentMessage = "No entiendo bien que significa 'cantidad', me puedes explicar de otra forma?" }
    Log "3. TUTOR ASK (Teaching) -> $($tutorTeaching.message)"
    if ($tutorTeaching.illustrations.Count -gt 0) {
        $savedPaths = SaveIllustrations $tutorTeaching.illustrations "tutor-v2-e2e-illustration-teaching"
        Log "   ILLUSTRATION(S) SAVED -> $($savedPaths -join ', ')"
    }
    $advanced = PostJson "$base/steps/advance" @{ learningSessionNodeId = $learningSessionNodeId }
    Log "   advanced -> $($advanced.stepType)"
    $currentStepId = $advanced.learningSessionStepId

    # 4. RECALL - TutorSubmitRecallAttempt loop until Advanced=true (mastery gate, deterministic in RecallLoopGate)
    $tutorPromptShown = $null
    $recallAttempts = 0
    do {
        $recallAttempts++
        $recallOutcome = PostJson "$base/tutor/recall-attempts" @{ learningSessionStepId = $currentStepId; studentResponse = "La cantidad es cuantos objetos hay en un grupo, sirve para comparar y sumar grupos."; tutorPromptShown = $tutorPromptShown }
        Log "4. RECALL attempt $recallAttempts -> RecallScore=$($recallOutcome.tutorTurn.recallScore) AttemptsUsed=$($recallOutcome.attemptsUsed) Mastered=$($recallOutcome.mastered) Advanced=$($recallOutcome.advanced)"
        $tutorPromptShown = $recallOutcome.tutorTurn.message
    } while (-not $recallOutcome.advanced -and $recallAttempts -lt 6)

    if (-not $recallOutcome.advanced) { throw "Recall never advanced after $recallAttempts attempts (gate should force advance at 5)." }
    if ($recallOutcome.nextStep.stepType -ne "Production") { throw "Expected Production after Recall advance, got $($recallOutcome.nextStep.stepType)" }
    $currentStepId = $recallOutcome.nextStep.learningSessionStepId
    Log "   -> Recall done, now on $($recallOutcome.nextStep.stepType)"

    # 5. PRODUCTION - TutorAsk mode=Production (Socratic on-demand help), then submit+advance
    $tutorProduction = PostJson "$base/tutor/ask" @{ learningSessionStepId = $currentStepId; mode = "Production"; studentMessage = "Como aplico la idea de cantidad para comparar dos grupos distintos?" }
    Log "5. TUTOR ASK (Production) -> $($tutorProduction.message)"

    PostJson "$base/steps/respond" @{ learningSessionStepId = $currentStepId; response = "Cuento cuantos objetos tiene cada grupo y comparo los dos numeros; el grupo con el numero mas alto tiene mas cantidad." } | Out-Null
    $advanced = PostJson "$base/steps/advance" @{ learningSessionNodeId = $learningSessionNodeId }
    Log "   submitted, advanced -> $($advanced.stepType)"
    $currentStepId = $advanced.learningSessionStepId

    # 6. ASSESSMENT - submit response, evaluate (real AssessmentEvaluator), then TutorAsk mode=AssessmentFeedback
    PostJson "$base/steps/respond" @{ learningSessionStepId = $currentStepId; response = "La cantidad me indica cuantos elementos hay en un conjunto y me sirve para comparar o sumar grupos entre si." } | Out-Null

    $evalResult = PostJson "$base/nodes/evaluate" @{ learningSessionNodeId = $learningSessionNodeId }
    Log "6. EVALUATE ASSESSMENT -> Score=$($evalResult.score) Passed=$($evalResult.passed) Feedback=$($evalResult.feedback)"

    $tutorFeedback = PostJson "$base/tutor/ask" @{ learningSessionStepId = $currentStepId; mode = "AssessmentFeedback"; studentMessage = "No entiendo bien el feedback que me dieron, me lo puedes explicar?"; rawAssessmentFeedback = $evalResult.feedback }
    Log "   TUTOR ASK (AssessmentFeedback) -> $($tutorFeedback.message)"

    # 7. COMPLETE NODE
    $completeResult = PostJson "$base/nodes/complete" @{ learningSessionNodeId = $learningSessionNodeId }
    Log "7. COMPLETE NODE -> success=$($completeResult.success)"

    Log "`nSUCCESS - TutorAgent V2 E2E: los 4 modos (Teaching/Recall+gate/Production/AssessmentFeedback) funcionaron correctamente sobre un nodo completo."
    $log -join "`n" | Set-Content -Path "$PSScriptRoot\tutor-v2-e2e-results.txt" -Encoding utf8
} catch {
    Log "`nFAILURE - TutorAgent V2 E2E: $($_.Exception.Message)"
    if ($_.ErrorDetails) { Log $_.ErrorDetails.Message }
    $log -join "`n" | Set-Content -Path "$PSScriptRoot\tutor-v2-e2e-results.txt" -Encoding utf8
    throw
}
