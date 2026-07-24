$body = '{"tenantId":"81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b","capabilityDomainId":"26bd8589-acbc-4c67-8ada-728e90e05f94","capabilityName":"Sumar y restar","description":"Quiero que un nino aprenda a sumar y restar"}'
try {
    $resp = Invoke-RestMethod -Uri "http://localhost:7071/api/studio/capability-graph/create-from-description" -Method Post -ContentType "application/json" -Body $body
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path start-resp2.json
    "SUCCESS" | Set-Content -Path start-status2.txt
} catch {
    $_.Exception.Message | Set-Content -Path start-status2.txt
    if ($_.ErrorDetails) { $_.ErrorDetails.Message | Add-Content -Path start-status2.txt }
}
