param(
    [Parameter(Mandatory=$true)][string]$RunId
)

# Fetch the pending subject id first
$status = Invoke-RestMethod -Method Get -Uri "http://localhost:7071/api/studio/capability-creation/$RunId/status" -TimeoutSec 30
$subjectId = $status.PendingSubjectId

$gateBody = @{
    subjectId = $subjectId
    approved = $true
} | ConvertTo-Json

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/$RunId/respond" -ContentType "application/json" -Body $gateBody -TimeoutSec 300
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\spanish-retry-gate-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\spanish-retry-gate-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\spanish-retry-gate-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\spanish-retry-gate-status.txt"
    }
}
