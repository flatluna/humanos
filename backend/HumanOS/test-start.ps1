$startBody = @{
    capabilityDomainId = "9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"
    capabilityGoal = "Conseguir mi primer trabajo"
    rawMaterials = @(
        @{
            type = "UserNote"
            label = "Que buscan los reclutadores"
            content = "Los reclutadores dedican en promedio menos de 30 segundos a la primera revision de un resume. Buscan palabras clave que coincidan con la descripcion del puesto, logros cuantificables (no solo tareas) y senales de progresion de carrera. El proceso tipico tiene 4 etapas: filtro automatico (ATS), entrevista telefonica con RRHH, entrevista tecnica o con el gerente, y entrevista final. La mayoria de los candidatos son descartados en el filtro ATS por no adaptar su resume a cada oferta."
        },
        @{
            type = "UserNote"
            label = "Construccion del resume"
            content = "Un buen resume para un primer empleo debe priorizar proyectos personales, practicas profesionales y logros academicos relevantes por encima de experiencia laboral formal, que suele ser escasa. Cada linea debe seguir el formato: verbo de accion + que hiciste + resultado medible. LinkedIn debe reflejar exactamente la misma narrativa que el resume, con una foto profesional y un resumen que explique claramente que tipo de rol se busca."
        },
        @{
            type = "UserNote"
            label = "Preparacion de entrevistas"
            content = "La tecnica STAR (Situacion, Tarea, Accion, Resultado) es la mas efectiva para responder preguntas conductuales. Practicar en voz alta y grabarse ayuda a detectar muletillas y falta de estructura. Antes de la entrevista final conviene investigar a la empresa, preparar 3 preguntas propias, y practicar la negociacion salarial: nunca dar el primer numero si se puede evitar, e investigar rangos salariales de mercado con antelacion."
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $start = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/start" -ContentType "application/json" -Body $startBody -TimeoutSec 170
    $start | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\start-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\start-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\start-status.txt"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.ReadToEnd() | Add-Content -Path "$PSScriptRoot\start-status.txt"
    }
}
