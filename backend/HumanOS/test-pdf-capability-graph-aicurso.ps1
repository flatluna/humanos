$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:7071/api"
$pdfPath = "C:\EducationAI\Documents\AICurso.pdf"

$tenantId = "81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b"
$capabilityDomainId = "9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"
$capabilityName = "Matematicas para IA (v2 - imagen+HTML+web)"

Write-Output "Reading PDF: $pdfPath"
$bytes = [System.IO.File]::ReadAllBytes($pdfPath)
Write-Output "PDF size: $($bytes.Length) bytes"
$contentBase64 = [Convert]::ToBase64String($bytes)

$startBody = @{
    TenantId = $tenantId
    CapabilityDomainId = $capabilityDomainId
    CapabilityName = $capabilityName
    FileName = "AICurso.pdf"
    ContentBase64 = $contentBase64
    EnableWebEnrichment = $true
} | ConvertTo-Json

$startBytes = [System.Text.Encoding]::UTF8.GetBytes($startBody)

Write-Output "Starting PDF capability graph pipeline..."
$startResp = Invoke-RestMethod -Method Post -Uri "$baseUrl/studio/capability-graph/create-from-pdf" -ContentType "application/json; charset=utf-8" -Body $startBytes
$startResp | ConvertTo-Json -Depth 5
$runId = $startResp.RunId
Write-Output "RunId: $runId"

$maxPolls = 120
$pollIntervalSeconds = 5
for ($i = 0; $i -lt $maxPolls; $i++) {
    Start-Sleep -Seconds $pollIntervalSeconds
    $status = Invoke-RestMethod -Method Get -Uri "$baseUrl/studio/capability-graph/$runId/status"
    Write-Output "[$i] Stage=$($status.Stage) Step=$($status.CurrentStepDescription)"
    if ($status.Stage -eq "Completed") {
        Write-Output "COMPLETED"
        $status | ConvertTo-Json -Depth 10
        break
    }
    if ($status.Stage -eq "Failed") {
        Write-Output "FAILED: $($status.ErrorMessage)"
        break
    }
}
