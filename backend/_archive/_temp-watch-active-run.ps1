$uri = "http://localhost:7071/api/studio/capability-graph/active"
while ($true) {
    $status = Invoke-RestMethod -Uri $uri -Method Get
    Write-Host "$(Get-Date -Format 'HH:mm:ss') stage=$($status.stage) step='$($status.currentStepDescription)'"
    if ($status.stage -ne 'Running') {
        Write-Host "----- FINAL STATUS -----"
        $status | ConvertTo-Json -Depth 10
        break
    }
    Start-Sleep -Seconds 5
}
