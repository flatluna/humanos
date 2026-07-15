$body = @{
    capabilityDomainId = "9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"
    capabilityGoal = "Quiero aprender español básico para poder mantener una conversación cotidiana simple sin depender de traductores"
    rawMaterials = @(
        @{
            type = "UserNote"
            label = "Contexto del objetivo"
            content = "Quiero poder saludar, presentarme, pedir cosas en una tienda o cafeteria, preguntar direcciones y responder preguntas simples en español, sin depender de un traductor."
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/start" -ContentType "application/json" -Body $body -TimeoutSec 170
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\spanish-retry2-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\spanish-retry2-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\spanish-retry2-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\spanish-retry2-status.txt"
    }
}
