param(
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$SubjectId
)

$gate2Body = @{
    subjectId = $SubjectId
    approved = $true
    comments = "Reduced smoke test approved - publish"
} | ConvertTo-Json

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/$RunId/respond" -ContentType "application/json" -Body $gate2Body -TimeoutSec 120
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\gate2-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\gate2-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\gate2-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\gate2-status.txt"
    }
}
