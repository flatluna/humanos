$body = @{
    capabilityDomainId = "5a75e51d-f803-460d-ba08-6ebba78ef004"
    capabilityGoal = "Aprender a preparar cafe en prensa francesa correctamente por mi cuenta"
    rawMaterials = @(
        @{
            type = "UserNote"
            label = "Notas sobre prensa francesa"
            content = "Para preparar cafe en prensa francesa: usa cafe molido grueso (proporcion 1:15, ej. 30g de cafe por 450ml de agua), agua caliente a 93-96 grados (no hirviendo), vierte el agua sobre el cafe, remueve suavemente, deja reposar 4 minutos tapado sin presionar el embolo, luego presiona el embolo lentamente y de forma constante, sirve inmediatamente para evitar sobre-extraccion. Errores comunes: moler el cafe muy fino (queda amargo y con sedimento), usar agua hirviendo (quema el cafe), dejar reposar demasiado tiempo antes de servir (sobre-extraccion), presionar el embolo con fuerza o de golpe (agita los sedimentos)."
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/start" -ContentType "application/json" -Body $body -TimeoutSec 170
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\final-e2e-start.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\final-e2e-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\final-e2e-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\final-e2e-status.txt"
    }
}
