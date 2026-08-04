$roleId = "263d5230-6e0f-48f7-9730-13d9c2905dfd"
$role = Invoke-RestMethod -Uri "http://localhost:7071/api/job-roles/$roleId" -TimeoutSec 15

$body = [ordered]@{
  departmentId = $role.departmentId
  title = $role.title
  levelLabel = $role.levelLabel
  purpose = $role.purpose
  summary = $role.summary
  responsibilities = $role.responsibilities
  dailyActivities = $role.dailyActivities
  requiredExperience = $role.requiredExperience
  requiredEducation = $role.requiredEducation
  technicalSkills = $role.technicalSkills
  requiredCertifications = $role.requiredCertifications
  requiredLanguages = $role.requiredLanguages
  softSkills = $role.softSkills
  departmentProcesses = $role.departmentProcesses
  hrProcesses = $role.hrProcesses
  tools = $role.tools
  reportsToJobRoleId = $role.reportsToJobRoleId
  performanceIndicators = $role.performanceIndicators
  workSchedule = $role.workSchedule
  workModality = $role.workModality
  requiresTravel = $role.requiresTravel
  workLocation = $role.workLocation
  availabilityNotes = $role.availabilityNotes
  employmentType = $role.employmentType
  salaryRangeMin = $role.salaryRangeMin
  salaryRangeMax = $role.salaryRangeMax
  salaryCurrency = $role.salaryCurrency
  benefits = $role.benefits
  expectedOutcomes = $role.expectedOutcomes
} | ConvertTo-Json -Depth 5

Write-Host "---BODY---"
Write-Host $body
Write-Host "---SENDING---"

try {
  $r = Invoke-WebRequest -Uri "http://localhost:7071/api/job-roles/$roleId" -Method Put -Body $body -ContentType "application/json" -TimeoutSec 15
  Write-Host "STATUS: $($r.StatusCode)"
  Write-Host $r.Content
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
