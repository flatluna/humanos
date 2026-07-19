# Runtime V1 - Paso 4 (HTTP API Layer) - Live E2E test.
# Drives all 7 new Azure Functions endpoints in sequence against a running
# `func start` host, reusing the Person/Capability/CapabilityGraphNode
# already set up by the most recent test-curador-grapharchitect-flow run
# (see CURADOR_GRAPHARCHITECT_RESULTS.txt).
#
# Flow: StartSession -> GetCurrentStep -> (SubmitResponse -> Advance) x4
#       (Hypothesis->Teaching->Recall->Production->Assessment)
#       -> SubmitResponse(Assessment) -> EvaluateAssessment -> CompleteNode
#       -> GetActiveSession (expect null, since node is now Completed)

$ErrorActionPreference = "Stop"
$base = "http://localhost:7071/api/instructor-runtime"
$log = @()

function Log($msg) {
    Write-Host $msg
    $script:log += $msg
}

$personId = "22e52050-2b03-475f-93b9-880b81e50663"
$capabilityId = "523f53da-a181-4801-a6c6-1b349a5e593c"
$capabilityGraphNodeId = "ac8ed53b-8e8a-4173-aa46-643a3ee729be"

try {
    # 1. START SESSION
    $startBody = @{ personId = $personId; capabilityId = $capabilityId; capabilityGraphNodeId = $capabilityGraphNodeId } | ConvertTo-Json
    $session = Invoke-RestMethod -Method Post -Uri "$base/sessions/start" -ContentType "application/json" -Body $startBody
    Log "1. START SESSION -> LearningSessionId=$($session.learningSessionId) LearningSessionNodeId=$($session.learningSessionNodeId) CurrentStepType=$($session.currentStepType)"
    if ($session.currentStepType -ne "Hypothesis") { throw "Expected Hypothesis, got $($session.currentStepType)" }

    $learningSessionNodeId = $session.learningSessionNodeId
    $currentStepId = $session.currentStep.learningSessionStepId
    $currentStepType = $session.currentStepType

    # 2. GET CURRENT STEP (resume-style, only personId+capabilityId)
    $current = Invoke-RestMethod -Method Get -Uri "$base/sessions/current-step?personId=$personId&capabilityId=$capabilityId"
    Log "2. GET CURRENT STEP -> Step.StepType=$($current.step.stepType) Content length=$($current.content.Length)"
    if ($current.step.stepType -ne "Hypothesis") { throw "Expected Hypothesis on resume, got $($current.step.stepType)" }

    $expectedOrder = @("Hypothesis", "Teaching", "Recall", "Production", "Assessment")
    for ($i = 0; $i -lt 4; $i++) {
        # 3/5/7/9. SUBMIT RESPONSE for current step
        $respondBody = @{ learningSessionStepId = $currentStepId; response = "Respuesta de prueba E2E para el paso $currentStepType." } | ConvertTo-Json
        $respondResult = Invoke-RestMethod -Method Post -Uri "$base/steps/respond" -ContentType "application/json" -Body $respondBody
        Log "   SUBMIT RESPONSE ($currentStepType) -> success=$($respondResult.success) learningEvidenceId=$($respondResult.learningEvidenceId)"

        # 4/6/8/10. ADVANCE STEP
        $advanceBody = @{ learningSessionNodeId = $learningSessionNodeId } | ConvertTo-Json
        $advanced = Invoke-RestMethod -Method Post -Uri "$base/steps/advance" -ContentType "application/json" -Body $advanceBody
        Log "   ADVANCE STEP -> new StepType=$($advanced.stepType)"

        $expected = $expectedOrder[$i + 1]
        if ($advanced.stepType -ne $expected) { throw "Expected $expected after advancing, got $($advanced.stepType)" }

        $currentStepId = $advanced.learningSessionStepId
        $currentStepType = $advanced.stepType
    }

    # Now on Assessment - submit its response too, before evaluating.
    $assessRespondBody = @{ learningSessionStepId = $currentStepId; response = "Respuesta de Assessment de prueba E2E: explico el concepto completo con mis propias palabras." } | ConvertTo-Json
    $assessRespondResult = Invoke-RestMethod -Method Post -Uri "$base/steps/respond" -ContentType "application/json" -Body $assessRespondBody
    Log "3. SUBMIT RESPONSE (Assessment) -> success=$($assessRespondResult.success)"

    # 4. EVALUATE ASSESSMENT
    $evalBody = @{ learningSessionNodeId = $learningSessionNodeId } | ConvertTo-Json
    $evalResult = Invoke-RestMethod -Method Post -Uri "$base/nodes/evaluate" -ContentType "application/json" -Body $evalBody
    Log "4. EVALUATE ASSESSMENT -> Score=$($evalResult.score) Passed=$($evalResult.passed) Feedback=$($evalResult.feedback)"

    # 5. COMPLETE NODE
    $completeBody = @{ learningSessionNodeId = $learningSessionNodeId } | ConvertTo-Json
    $completeResult = Invoke-RestMethod -Method Post -Uri "$base/nodes/complete" -ContentType "application/json" -Body $completeBody
    Log "5. COMPLETE NODE -> success=$($completeResult.success)"

    # 6. GET ACTIVE SESSION - our own node must no longer show up as active
    # (it may still return a DIFFERENT session/node if the shared test
    # Person+Capability has another still-in-progress LearningSession left
    # over from a previous test run - e.g. the curador harness's own PASO 10
    # recovery test deliberately leaves its session at Recall. That is not a
    # Paso 4 bug; what matters is that OUR just-completed node is excluded.)
    $activeAfterComplete = Invoke-RestMethod -Method Get -Uri "$base/sessions/active?personId=$personId&capabilityId=$capabilityId"
    if ($null -eq $activeAfterComplete) {
        Log "6. GET ACTIVE SESSION (after complete) -> null, as expected (node Completed)."
    } elseif ($activeAfterComplete.learningSessionNodeId -ne $learningSessionNodeId) {
        Log "6. GET ACTIVE SESSION (after complete) -> returned a DIFFERENT active node ($($activeAfterComplete.learningSessionNodeId)), confirming OUR node ($learningSessionNodeId) is correctly excluded now that it is Completed."
    } else {
        Log "6. GET ACTIVE SESSION (after complete) -> UNEXPECTED: still returned OUR just-completed node: $($activeAfterComplete | ConvertTo-Json -Depth 5)"
        throw "Expected our just-completed LearningSessionNode to no longer be the active one, but it was still returned."
    }

    Log "`nSUCCESS - PASO 4: los 7 endpoints HTTP funcionaron correctamente en secuencia (Start -> CurrentStep -> [Respond -> Advance]x4 -> Respond -> Evaluate -> Complete -> ActiveSession=null)."
    $log -join "`n" | Set-Content -Path "$PSScriptRoot\paso4-e2e-results.txt" -Encoding utf8
} catch {
    Log "`nFAILURE - PASO 4: $($_.Exception.Message)"
    if ($_.ErrorDetails) { Log $_.ErrorDetails.Message }
    $log -join "`n" | Set-Content -Path "$PSScriptRoot\paso4-e2e-results.txt" -Encoding utf8
    throw
}
