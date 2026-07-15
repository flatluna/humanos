param(
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$SubjectId
)

$gate1Body = @{
    subjectId = $SubjectId
    approved = $true
    comments = "Reduced to 2 modules for a cheap end-to-end smoke test"
    revisedBlueprint = @{
        capabilityName = "Conseguir mi primer trabajo"
        goal = "Conseguir mi primer trabajo"
        levels = @(
            @{
                layer = "Frontier"
                title = "Gestionar tu busqueda y empezar a guiar a otros"
                humanTransformation = "El aprendiz deja de trabajar su busqueda de forma aislada y empieza a orientar a companeros dando feedback constructivo en resumes y entrevistas."
                modules = @(
                    @{
                        title = "Coaching entre pares: revisar resumes y dar feedback"
                        description = "Sesiones de mentoria entre pares estructuradas para revisar y devolver retroalimentacion accionable sobre resumes y perfiles LinkedIn."
                        type = "Mentoria"
                        targetMetric = "Fluency"
                    },
                    @{
                        title = "Panel de entrevistas: simulador de rounds multiples"
                        description = "Simulador IA que reproduce la secuencia ATS -> RRHH -> tecnico -> final para practicar continuidad de narrativa y preparacion por ronda."
                        type = "SimuladorIA"
                        targetMetric = "Confidence"
                    }
                )
            }
        )
    }
} | ConvertTo-Json -Depth 8

try {
    $resp = Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/studio/capability-creation/$RunId/respond" -ContentType "application/json" -Body $gate1Body -TimeoutSec 300
    $resp | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\gate1-response.json" -Encoding utf8
    "SUCCESS" | Set-Content -Path "$PSScriptRoot\gate1-status.txt"
} catch {
    $_.Exception.Message | Set-Content -Path "$PSScriptRoot\gate1-status.txt"
    if ($_.ErrorDetails) {
        $_.ErrorDetails.Message | Add-Content -Path "$PSScriptRoot\gate1-status.txt"
    }
}
