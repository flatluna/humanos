param(
    [string]$DocId = "982a046a-c7ff-4f25-aee6-45181c99591c",
    [int]$ThrottleLimit = 3
)

$ErrorActionPreference = "Continue"
Write-Output "Fetching document $DocId..."
$doc = Invoke-RestMethod -Uri "http://localhost:7071/api/studio/structured-learning/$DocId" -Method GET
Write-Output "Document: $($doc.documentName)"
Write-Output "Total sections: $($doc.sections.Count)"

$sections = $doc.sections
$total = $sections.Count

$results = $sections | ForEach-Object -Parallel {
    $s = $_
    $secId = $s.structuredLearningDocumentSectionId
    $title = $s.subchapterTitle
    if ($title.Length -gt 40) { $title = $title.Substring(0, 40) + "..." }
    
    # 1. Generate connections
    $connsCreated = 0
    try {
        $cRes = Invoke-RestMethod -Uri "http://localhost:7071/api/studio/structured-learning/sections/$secId/knowledge-graph/generate" -Method POST -TimeoutSec 120
        $connsCreated = $cRes.connectionsCreated
    } catch {
        $err = $_.Exception.Message
        Write-Warning "[$secId] Connection error: $err"
    }

    # 2. Generate knowledge facts
    $factsCreated = 0
    try {
        $fRes = Invoke-RestMethod -Uri "http://localhost:7071/api/studio/structured-learning/sections/$secId/knowledge-facts/generate" -Method POST -TimeoutSec 120
        $factsCreated = $fRes.knowledgeFactsCreated
    } catch {
        $err = $_.Exception.Message
        Write-Warning "[$secId] Fact error: $err"
    }

    Write-Output "Done: $title | Conns: $connsCreated | Facts: $factsCreated"

    [PSCustomObject]@{
        SectionId = $secId
        Title = $title
        Connections = $connsCreated
        Facts = $factsCreated
    }
} -ThrottleLimit $ThrottleLimit

$totalConns = ($results | Measure-Object -Property Connections -Sum).Sum
$totalFacts = ($results | Measure-Object -Property Facts -Sum).Sum
$sectionsWithFacts = ($results | Where-Object { $_.Facts -gt 0 }).Count

Write-Output "========================================"
Write-Output "FINISHED $DocId"
Write-Output "Sections with facts: $sectionsWithFacts / $total"
Write-Output "Total Connections Created: $totalConns"
Write-Output "Total Knowledge Facts Created: $totalFacts"
Write-Output "========================================"
