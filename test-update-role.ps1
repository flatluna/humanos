$body = @{
  departmentId = "a8d509fa-4032-4c41-a56e-838c281708cf"
  title = "Marketing"
  levelLabel = "Coordinador"
  purpose = "Coordinar e implementar iniciativas de marketing."
  summary = "El puesto de Marketing es responsable de coordinar campanas."
  responsibilities = @("Coordinar la ejecucion de campanas de marketing digital.")
  dailyActivities = @("Programar y revisar publicaciones en canales digitales.")
  requiredExperience = "2 a 4 anos de experiencia en marketing."
  requiredEducation = "Licenciatura en Marketing."
  technicalSkills = @("Marketing digital","Gestion de campanas")
  requiredCertifications = @()
  requiredLanguages = @("Espanol fluido","Ingles intermedio o avanzado")
  softSkills = @("Sexual Harassment Prevention (compliance training)","Comunicacion efectiva")
  departmentProcesses = @()
  hrProcesses = @()
  tools = @("Google Analytics","Meta Business Suite")
  reportsToJobRoleId = $null
  performanceIndicators = @()
  workSchedule = $null
  workModality = $null
  requiresTravel = $false
  workLocation = $null
  availabilityNotes = $null
  employmentType = $null
  salaryRangeMin = $null
  salaryRangeMax = $null
  salaryCurrency = $null
  benefits = @()
  expectedOutcomes = @()
} | ConvertTo-Json -Depth 5

try {
  $r = Invoke-RestMethod -Uri "http://localhost:7071/api/job-roles/263d5230-6e0f-48f7-9730-13d9c2905dfd" -Method Put -Body $body -ContentType "application/json" -TimeoutSec 15
  Write-Host "SUCCESS"
  $r | ConvertTo-Json -Depth 3
} catch {
  Write-Host "FAILED"
  $resp = $_.Exception.Response
  if ($resp) {
    $stream = $resp.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host $reader.ReadToEnd()
  } else {
    Write-Host $_.Exception.Message
  }
}
