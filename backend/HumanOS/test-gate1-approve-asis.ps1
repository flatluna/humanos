param(
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$SubjectId
)

$body = @{
    subjectId = $SubjectId
    approved = $true
    comments = "Approved as-is (small scope-selected course, no reduction needed)"
} | ConvertTo-Json -Depth 5

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/$RunId/respond" -ContentType "application/json" -Body $body -TimeoutSec 300
    $resp | ConvertTo-Json -Depth 12 | Set-Content -Path "$PSScriptRoot\gate1-asis-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\gate1-asis-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\gate1-asis-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\gate1-asis-status.txt"
    }
}
