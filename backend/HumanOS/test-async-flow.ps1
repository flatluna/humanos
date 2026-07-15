$base = "http://localhost:7071/api/studio/capability-creation"
$log = "$PSScriptRoot\async-flow-test-log.txt"
"" | Set-Content -Path $log

function Log($msg) {
    $line = "$(Get-Date -Format 'HH:mm:ss') $msg"
    Write-Host $line
    Add-Content -Path $log -Value $line
}

function Poll-Status($runId) {
    while ($true) {
        Start-Sleep -Seconds 2
        try {
            $status = Invoke-RestMethod -Method Get -Uri "$base/$runId/status" -TimeoutSec 30
        } catch {
            Log "POLL ERROR: $($_.Exception.Message)"
            continue
        }
        $progress = ""
        if ($status.progress) {
            $progress = " | totalModules=$($status.progress.totalModules) completedModules=$($status.progress.completedModules) currentModule=$($status.progress.currentModuleTitle) publishTasks=$($status.progress.publishTasks | ConvertTo-Json -Compress)"
        }
        Log "STATUS stage=$($status.stage)$progress"
        if ($status.stage -ne "Running") {
            return $status
        }
    }
}

$startBody = @{
    capabilityDomainId = "9cc8511e-14f8-4dbf-bc89-3a014b61cf9e"
    capabilityGoal = "Conseguir mi primer trabajo"
    rawMaterials = @(
        @{
            type = "UserNote"
            label = "Que buscan los reclutadores"
            content = "Los reclutadores dedican en promedio menos de 30 segundos a la primera revision de un resume. Buscan palabras clave que coincidan con la descripcion del puesto y logros cuantificables."
        }
    )
} | ConvertTo-Json -Depth 5

Log "Calling /start ..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$start = Invoke-RestMethod -Method Post -Uri "$base/start" -ContentType "application/json" -Body $startBody -TimeoutSec 30
$sw.Stop()
Log "/start returned in $($sw.ElapsedMilliseconds) ms with stage=$($start.stage) (should be Running, and FAST)"

$runId = $start.runId
Log "runId = $runId"

Log "Polling until Gate 1 pending..."
$gate1 = Poll-Status $runId
Log "Gate1 payload keys: $($gate1.payload.PSObject.Properties.Name -join ', ')"

$gate1Body = @{
    subjectId = $gate1.pendingSubjectId
    approved = $true
    comments = "Reduced to 1 module for async-flow smoke test"
    revisedBlueprint = @{
        capabilityName = "Conseguir mi primer trabajo"
        goal = "Conseguir mi primer trabajo"
        levels = @(
            @{
                layer = "Frontier"
                title = "Gestionar tu busqueda y empezar a guiar a otros"
                humanTransformation = "El aprendiz deja de trabajar su busqueda de forma aislada."
                modules = @(
                    @{
                        title = "Coaching entre pares: revisar resumes y dar feedback"
                        description = "Sesiones de mentoria entre pares estructuradas."
                        type = "Mentoria"
                        targetMetric = "Fluency"
                    }
                )
            }
        )
    }
} | ConvertTo-Json -Depth 8

Log "Calling /respond for Gate 1 ..."
$sw.Restart()
$respond1 = Invoke-RestMethod -Method Post -Uri "$base/$runId/respond" -ContentType "application/json" -Body $gate1Body -TimeoutSec 30
$sw.Stop()
Log "/respond (gate1) returned in $($sw.ElapsedMilliseconds) ms with stage=$($respond1.stage) (should be Running, and FAST)"

Log "Polling through module generation until Gate 2 pending..."
$gate2 = Poll-Status $runId
Log "Gate2 payload keys: $($gate2.payload.PSObject.Properties.Name -join ', ')"

$gate2Body = @{
    subjectId = $gate2.pendingSubjectId
    approved = $true
    comments = "Approve for async-flow smoke test"
} | ConvertTo-Json -Depth 5

Log "Calling /respond for Gate 2 ..."
$sw.Restart()
$respond2 = Invoke-RestMethod -Method Post -Uri "$base/$runId/respond" -ContentType "application/json" -Body $gate2Body -TimeoutSec 30
$sw.Stop()
Log "/respond (gate2) returned in $($sw.ElapsedMilliseconds) ms with stage=$($respond2.stage) (should be Running, and FAST)"

Log "Polling through Publish until Completed..."
$final = Poll-Status $runId
Log "FINAL stage=$($final.stage)"
$final | ConvertTo-Json -Depth 10 | Set-Content -Path "$PSScriptRoot\async-flow-final-response.json" -Encoding utf8
Log "Done. Full final response written to async-flow-final-response.json"
