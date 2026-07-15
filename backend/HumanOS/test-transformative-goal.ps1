$body = @{
    capabilityDomainId = "9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"
    capabilityGoal = "Formar a alguien desde cero hasta convertirse en un arquitecto de software experto, capaz de disenar sistemas complejos, liderar equipos tecnicos, tomar decisiones de arquitectura bajo ambiguedad, y mentorar a otros arquitectos junior en la organizacion."
    rawMaterials = @(
        @{
            type = "UserNote"
            label = "Fundamentos de arquitectura de software"
            content = "La arquitectura de software define la estructura de alto nivel de un sistema: sus componentes, las relaciones entre ellos, y los principios que guian su diseno y evolucion. Los conceptos fundamentales incluyen: separacion de responsabilidades (SRP), acoplamiento y cohesion, patrones arquitectonicos (monolito, microservicios, arquitectura hexagonal, event-driven), y trade-offs entre consistencia, disponibilidad y tolerancia a particiones (teorema CAP)."
        },
        @{
            type = "UserNote"
            label = "Diseno de sistemas distribuidos"
            content = "Los sistemas distribuidos requieren decisiones sobre particionamiento de datos (sharding), replicacion, consenso distribuido (Raft, Paxos), colas de mensajes para desacoplar servicios, y estrategias de resiliencia (circuit breakers, retries con backoff exponencial, bulkheads). Un arquitecto experto sabe evaluar cuando la complejidad de un sistema distribuido se justifica versus un monolito bien disenado."
        },
        @{
            type = "UserNote"
            label = "Liderazgo tecnico y toma de decisiones"
            content = "Un arquitecto senior no solo disena sistemas: lidera decisiones tecnicas bajo ambiguedad e incertidumbre, comunica trade-offs a stakeholders no tecnicos, escribe Architecture Decision Records (ADRs) para documentar y justificar decisiones, y mentora a ingenieros junior y de nivel medio para que crezcan en su propio juicio arquitectonico en vez de depender siempre de una autoridad central."
        },
        @{
            type = "UserNote"
            label = "Evolucion y deuda tecnica"
            content = "Los sistemas reales evolucionan: los arquitectos expertos disenan para el cambio (evolutionary architecture), gestionan la deuda tecnica de forma deliberada (no accidental), y usan fitness functions para verificar continuamente que la arquitectura sigue cumpliendo sus atributos de calidad (performance, seguridad, escalabilidad) a medida que el sistema crece."
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/start" -ContentType "application/json" -Body $body -TimeoutSec 170
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\transform-test-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\transform-test-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\transform-test-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\transform-test-status.txt"
    }
}
