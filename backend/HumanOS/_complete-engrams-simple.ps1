param(
    [Parameter(Mandatory = $true)][string]$DocumentId,
    [Parameter(Mandatory = $true)][string[]]$SectionIds,
    [Parameter(Mandatory = $true)][string]$PersonId,
    [string]$BaseUrl = "http://localhost:7071/api",
    [int]$MaxAssessmentAttempts = 8
)

$ErrorActionPreference = "Stop"

foreach ($SectionId in $SectionIds) {
    Write-Output "=========================================="
    Write-Output "Engram $SectionId - PersonId $PersonId"
    Write-Output "=========================================="

    $doc = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/$DocumentId" -Method GET
    $section = $doc.sections | Where-Object { $_.structuredLearningDocumentSectionId -eq $SectionId }
    if (-not $section) { Write-Warning "Section not found, skipping."; continue }
    if ($section.pedagogyStatus -ne "Completed") { Write-Warning "Pedagogy not Completed, skipping."; continue }

    # Hypothesis (Antes de iniciar) - quick, deterministic, always correct answers.
    $hypothesisStep = $section.steps | Where-Object { $_.stepType -eq "Hypothesis" }
    if ($hypothesisStep -and $hypothesisStep.stepDataJson) {
        $questions = $hypothesisStep.stepDataJson | ConvertFrom-Json
        $answers = @()
        for ($i = 0; $i -lt $questions.Count; $i++) {
            $selected = 0
            if ($null -ne $questions[$i].correctIndex) { $selected = $questions[$i].correctIndex }
            $answers += @{ questionIndex = $i; selectedOptionIndex = $selected }
        }
        Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/hypothesis/responses?personId=$PersonId" `
            -Method POST -Body (@{ answers = $answers } | ConvertTo-Json -Depth 5) -ContentType "application/json" | Out-Null
        Write-Output "[Antes de iniciar] OK"
    }

    # Production (En tu puesto) - ungated, completes on first fetch.
    $productionStep = $section.steps | Where-Object { $_.stepType -eq "Production" }
    if ($productionStep) {
        Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/workplace-application?personId=$PersonId" -Method GET | Out-Null
        Write-Output "[En tu puesto] OK"
    }

    # Assessment (Evaluacion) - MultipleChoice is brute-forceable since a Failed round auto-starts a fresh one.
    $assessmentStep = $section.steps | Where-Object { $_.stepType -eq "Assessment" }
    if ($assessmentStep) {
        $round = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/sections/$SectionId/assessment/start-round?personId=$PersonId" -Method POST
        $passed = $false
        $attempt = 0
        while ($round.currentQuestion -and $attempt -lt $MaxAssessmentAttempts) {
            $attempt++
            $q = $round.currentQuestion
            if ($q.options -and $q.options.Count -gt 0) {
                $optionIndex = ($attempt - 1) % $q.options.Count
                $answer = "$optionIndex"
            } else {
                $answer = "Based on this section's teaching content: $($q.questionText)"
            }
            $result = Invoke-RestMethod -Uri "$BaseUrl/studio/structured-learning/assessment/questions/$($q.structuredLearningAssessmentQuestionAttemptId)/answer" `
                -Method POST -Body (@{ studentAnswer = $answer } | ConvertTo-Json) -ContentType "application/json"
            if ($result.roundComplete -and $result.passed) { $passed = $true; break }
            $round = [PSCustomObject]@{ currentQuestion = $result.nextQuestion }
        }
        Write-Output "[Evaluacion] passed=$passed (attempts=$attempt)"
    }
}

Write-Output "=========================================="
Write-Output "DONE"
Write-Output "=========================================="
