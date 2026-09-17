$doc = Invoke-RestMethod -Uri "http://localhost:7071/api/studio/structured-learning/64d033e9-06d2-4497-9d34-d3ee385bd3c7" -Method GET
$sections = $doc.sections | Sort-Object sortOrder
$total = $sections.Count
$idx = 0

Write-Host "Total sections to process: $total"

foreach ($s in $sections) {
    $idx++
    $secId = $s.structuredLearningDocumentSectionId
    $teaching = $s.steps | Where-Object { $_.stepType -eq 'Teaching' }
    if (-not $teaching -or [string]::IsNullOrWhiteSpace($teaching.content)) {
        Write-Host "[$idx/$total] SKIPPED: $($s.subchapterTitle) (no teaching content)"
        continue
    }

    if ($teaching.stepDataJson -like '*SpeechStoragePath*') {
        Write-Host "[$idx/$total] ALREADY HAS AUDIO: $($s.subchapterTitle)"
        continue
    }

    Write-Host "[$idx/$total] GENERATING AUDIO: $($s.subchapterTitle) ..."
    try {
        $body = @{ Content = $teaching.content; Confirmed = $true } | ConvertTo-Json
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $res = Invoke-RestMethod -Uri "http://localhost:7071/api/studio/structured-learning/sections/$secId/steps/Teaching/content" -Method POST -Body $bytes -ContentType "application/json; charset=utf-8"
        Write-Host "    -> OK ($secId)"
    } catch {
        $errMsg = $_.Exception.Message
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errMsg = $reader.ReadToEnd()
        } catch { }
        Write-Host "    -> ERROR: $errMsg"
    }
}
Write-Host "ALL SECTIONS PROCESSED!"
