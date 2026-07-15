$body = @{
    capabilityDomainId = "9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"
    capabilityGoal = "Repasar rapido las reglas basicas de acentuacion en espanol para que se me queden en la memoria"
    rawMaterials = @(
        @{
            type = "UserNote"
            label = "Reglas de acentuacion"
            content = "Las palabras agudas llevan tilde cuando terminan en n, s o vocal (cancion, autobus, sofa). Las palabras llanas o graves llevan tilde cuando NO terminan en n, s o vocal (arbol, facil, azucar). Las palabras esdrujulas y sobresdrujulas siempre llevan tilde (musica, telefono, comuniqueselo)."
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/start" -ContentType "application/json" -Body $body -TimeoutSec 170
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\scope-test-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\scope-test-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\scope-test-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\scope-test-status.txt"
    }
}
