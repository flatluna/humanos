$body = @{
    capabilityDomainId = "9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"
    capabilityGoal = "Aprender a facilitar reuniones de trabajo efectivas, siendo capaz de crear y ejecutar agendas accionables por mi cuenta"
    rawMaterials = @(
        @{
            type = "UserNote"
            label = "Que hace efectiva una reunion"
            content = "Una reunion efectiva siempre tiene un proposito claro que termina en una decision o entrega concreta, no solo 'discutir'. Cada tema de la agenda debe tener un tiempo asignado, un responsable, y un resultado esperado. El facilitador debe anticipar posibles fricciones (desacuerdos, falta de datos, participantes ausentes) y tener una respuesta preparada. Las reuniones sin agenda accionable tienden a alargarse sin producir decisiones."
        },
        @{
            type = "UserNote"
            label = "Errores comunes al crear agendas"
            content = "Los errores mas comunes son: temas sin tiempo asignado, agendas que son solo una lista de topicos sin objetivo, falta de un responsable claro por item, y no dejar espacio para resolver fricciones previsibles. Una agenda accionable ordena los temas de forma que cada uno conduce al resultado final esperado de la reunion."
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/start" -ContentType "application/json" -Body $body -TimeoutSec 170
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\paso2-blueprint-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\paso2-blueprint-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\paso2-blueprint-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\paso2-blueprint-status.txt"
    }
}
