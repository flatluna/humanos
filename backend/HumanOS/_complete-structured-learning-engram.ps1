param(
    [Parameter(Mandatory = $true)][string]$DocumentId,
    [Parameter(Mandatory = $true)][string]$SectionId,
    [string]$PersonId = [guid]::NewGuid().ToString(),
    [string]$BaseUrl = "http://localhost:7071/api",
    [int]$MaxAttemptsPerGatedStep = 6
)

$ErrorActionPreference = "Stop"
Write-Output "=========================================="
Write-Output "Completing engram $SectionId (doc $DocumentId)"
Write-Output "PersonId: $PersonId"
Write-Output "=========================================="

function Get-Section {
    $doc = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/$DocumentId" -Method GET
    return $doc.sections | Where-Object { $_.structuredLearningDocumentSectionId -eq $SectionId }
}

$section = Get-Section
if (-not $section) { throw "Section $SectionId not found in document $DocumentId" }
if ($section.pedagogyStatus -ne "Completed") {
    throw "Section pedagogyStatus is '$($section.pedagogyStatus)'. Run /studio/structured-learning/pedagogy/start first."
}

# ---- 1. Hypothesis (Antes de iniciar) ----
$hypothesisStep = $section.steps | Where-Object { $_.stepType -eq "Hypothesis" }
if ($hypothesisStep -and $hypothesisStep.stepDataJson) {
    $questions = $hypothesisStep.stepDataJson | ConvertFrom-Json
    $answers = @()
    for ($i = 0; $i -lt $questions.Count; $i++) {
        $correct = $questions[$i].correctIndex
        $selected = 0
        if ($null -ne $correct) { $selected = $correct }
        $answers += @{ questionIndex = $i; selectedOptionIndex = $selected }
    }
    Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/hypothesis/responses?personId=$PersonId" `
        -Method POST -Body (@{ answers = $answers } | ConvertTo-Json -Depth 5) -ContentType "application/json" | Out-Null
    Write-Output "[Antes de iniciar] Submitted $($answers.Count) answers."
} else {
    Write-Output "[Antes de iniciar] No Hypothesis step/questions. Skipped."
}

# ---- 2. Teaching (Ensenanza) - ungated, nothing to submit ----
Write-Output "[Ensenanza] No submission required (content-only step)."

# ---- 3. DeepDive (Profundizacion) ----
$deepDiveStep = $section.steps | Where-Object { $_.stepType -eq "DeepDive" }
if ($deepDiveStep -and $deepDiveStep.stepDataJson) {
    $cards = $deepDiveStep.stepDataJson | ConvertFrom-Json
    $complete = $false
    $lastPrompt = $null
    for ($attempt = 1; $attempt -le $MaxAttemptsPerGatedStep -and -not $complete; $attempt++) {
        $state = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/deepdive/state" -Method GET
        $cardIdx = [Math]::Min($state.cardIndex, $cards.Count - 1)
        $card = $cards[$cardIdx]
        $response = "$($card.Title): $($card.Text)"
        $body = @{ studentResponse = $response; tutorPromptShown = $lastPrompt } | ConvertTo-Json -Depth 5
        $result = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/deepdive/attempt" -Method POST -Body $body -ContentType "application/json"
        $lastPrompt = $result.message
        $complete = $result.complete
        $cardNum = $result.cardIndex + 1
        Write-Output "[Profundizacion] Attempt $attempt : card $cardNum of $($result.totalCards) mastered=$($result.mastered) complete=$complete"
    }
    if (-not $complete) { Write-Warning "[Profundizacion] Did not reach Complete after $MaxAttemptsPerGatedStep attempts." }
} else {
    Write-Output "[Profundizacion] No DeepDive step/cards. Skipped."
}

# ---- 4. Recall (Evocacion) ----
$recallStep = $section.steps | Where-Object { $_.stepType -eq "Recall" }
if ($recallStep) {
    $teachingStep = $section.steps | Where-Object { $_.stepType -eq "Teaching" }
    $teachingContent = if ($teachingStep) { $teachingStep.content } else { $section.text }
    $complete = $false
    $studentResponse = $null
    for ($attempt = 1; $attempt -le $MaxAttemptsPerGatedStep -and -not $complete; $attempt++) {
        $body = @{ studentResponse = $studentResponse } | ConvertTo-Json -Depth 5
        $result = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/recall/attempt?personId=$PersonId" -Method POST -Body $body -ContentType "application/json"
        Write-Output "[Evocacion] Attempt $attempt : mastered=$($result.masteredThisAttempt) itemsMastered=$($result.itemsMastered)/$($result.itemsRequired) complete=$($result.complete)"
        $complete = $result.complete
        # Answer the question just asked (result.nextQuestion) using the section's own Teaching content as source of truth.
        $studentResponse = $teachingContent
    }
    if (-not $complete) { Write-Warning "[Evocacion] Did not reach Complete after $MaxAttemptsPerGatedStep attempts." }
} else {
    Write-Output "[Evocacion] No Recall step. Skipped."
}

# ---- 5. Production (En tu puesto) - ungated, completes on first fetch ----
$productionStep = $section.steps | Where-Object { $_.stepType -eq "Production" }
if ($productionStep) {
    Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/workplace-application?personId=$PersonId" -Method GET | Out-Null
    Write-Output "[En tu puesto] Workplace application generated/fetched. Step marked complete."
} else {
    Write-Output "[En tu puesto] No Production step. Skipped."
}

# ---- 6. Assessment (Evaluacion) ----
$assessmentStep = $section.steps | Where-Object { $_.stepType -eq "Assessment" }
if ($assessmentStep) {
    $round = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/assessment/start-round?personId=$PersonId" -Method POST
    $passed = $false
    $attempt = 0
    while ($round.currentQuestion -and $attempt -lt $MaxAttemptsPerGatedStep) {
        $attempt++
        $q = $round.currentQuestion
        if ($q.options -and $q.options.Count -gt 0) {
            # MultipleChoice - correct index isn't exposed to the client; try each option in turn.
            $optionIndex = ($attempt - 1) % $q.options.Count
            $answer = "$optionIndex"
        } else {
            $answer = "Based on this section's teaching content: $($q.questionText)"
        }
        $result = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/assessment/questions/$($q.structuredLearningAssessmentQuestionAttemptId)/answer" `
            -Method POST -Body (@{ studentAnswer = $answer } | ConvertTo-Json) -ContentType "application/json"
        Write-Output "[Evaluacion] Attempt $attempt : correctness=$($result.grade.correctness) roundComplete=$($result.roundComplete) passed=$($result.passed)"
        if ($result.roundComplete) {
            $passed = $result.passed
            if ($result.nextQuestion) {
                $round = [PSCustomObject]@{ currentQuestion = $result.nextQuestion }
            } else {
                break
            }
        } else {
            $round = [PSCustomObject]@{ currentQuestion = $result.nextQuestion }
        }
    }
    Write-Output "[Evaluacion] Final passed=$passed"
} else {
    Write-Output "[Evaluacion] No Assessment step. Skipped."
}

Write-Output "=========================================="
Write-Output "DONE - engram $SectionId completed for PersonId $PersonId"
Write-Output "=========================================="
