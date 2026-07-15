$json = Get-Content -Raw -Path "$PSScriptRoot\gate1-asis-response.json" | ConvertFrom-Json

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("CURSO: $($json.Payload.CapabilityName)")
[void]$sb.AppendLine("=" * 80)
[void]$sb.AppendLine()
[void]$sb.AppendLine("TUTOR KNOWLEDGE BASE (resumen consolidado):")
[void]$sb.AppendLine("-" * 80)
[void]$sb.AppendLine($json.Payload.TutorKnowledgeBase)
[void]$sb.AppendLine()
[void]$sb.AppendLine("=" * 80)

$moduleIndex = 1
foreach ($m in $json.Payload.Modules) {
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("MODULO $moduleIndex : $($m.Module.Title)")
    [void]$sb.AppendLine("Tipo: $($m.Module.Type)  |  TargetMetric (asignado por Arquitecto): $($m.Module.TargetMetric)")
    [void]$sb.AppendLine("Descripcion: $($m.Module.Description)")
    [void]$sb.AppendLine("-" * 80)
    [void]$sb.AppendLine("GUION (Instructor):")
    [void]$sb.AppendLine($m.Script.Script)
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("METRICAS VERIFICADAS (Metrico): $($m.Metrics.Metrics -join ', ')")
    [void]$sb.AppendLine("RATIONALE: $($m.Metrics.Rationale)")
    [void]$sb.AppendLine("=" * 80)
    $moduleIndex++
}

$sb.ToString() | Set-Content -Path "$PSScriptRoot\curso-completo-legible.txt" -Encoding utf8
"SUCCESS"
