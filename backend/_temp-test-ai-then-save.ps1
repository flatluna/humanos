$roleId = "263d5230-6e0f-48f7-9730-13d9c2905dfd"
$tenantId = "81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b"

$role = Invoke-RestMethod -Uri "http://localhost:7071/api/job-roles/$roleId" -TimeoutSec 30
Write-Host "--- CURRENT ROLE (departmentId=$($role.departmentId)) ---"

# 1) Build the GenerateJobRoleDescription request exactly like the frontend does
$genBody = [ordered]@{
  jobRoleId              = $roleId
  departmentId           = $role.departmentId
  title                  = $role.title
  levelLabel             = $role.levelLabel
  purpose                = $role.purpose
  summary                = $role.summary
  responsibilities       = $role.responsibilities
  requiredExperience     = $role.requiredExperience
  requiredEducation      = $role.requiredEducation
  requiredCertifications = $role.requiredCertifications
  requiredLanguages      = $role.requiredLanguages
  technicalSkills        = $role.technicalSkills
  softSkills             = $role.softSkills
  tools                  = $role.tools
  dailyActivities        = $role.dailyActivities
  performanceIndicators  = $role.performanceIndicators
  expectedOutcomes       = $role.expectedOutcomes
  workSchedule           = $role.workSchedule
  workModality           = $role.workModality
  requiresTravel         = $role.requiresTravel
  workLocation           = $role.workLocation
  availabilityNotes      = $role.availabilityNotes
  employmentType         = $role.employmentType
  salaryRangeMin         = $role.salaryRangeMin
  salaryRangeMax         = $role.salaryRangeMax
  salaryCurrency         = $role.salaryCurrency
  benefits               = $role.benefits
} | ConvertTo-Json -Depth 5

Write-Host "--- CALLING generate-description ---"
$proposal = Invoke-RestMethod -Uri "http://localhost:7071/api/tenants/$tenantId/job-roles/generate-description" -Method Post -Body $genBody -ContentType "application/json" -TimeoutSec 60
Write-Host "--- PROPOSAL ---"
$proposal | ConvertTo-Json -Depth 5 | Write-Host

# 2) Apply EVERY proposed field onto the current role (simulating "accept all" in the review panel)
$saveBody = [ordered]@{
  departmentId           = $role.departmentId
  title                  = $role.title
  levelLabel             = $proposal.levelLabel
  purpose                = $proposal.purpose
  summary                = $proposal.summary
  responsibilities       = $proposal.responsibilities
  dailyActivities        = $proposal.dailyActivities
  requiredExperience     = $proposal.requiredExperience
  requiredEducation      = $proposal.requiredEducation
  technicalSkills        = $proposal.technicalSkills
  requiredCertifications = $proposal.requiredCertifications
  requiredLanguages      = $proposal.requiredLanguages
  softSkills             = $proposal.softSkills
  departmentProcesses    = $role.departmentProcesses
  hrProcesses            = $role.hrProcesses
  tools                  = $proposal.tools
  reportsToJobRoleId     = $role.reportsToJobRoleId
  performanceIndicators  = $proposal.performanceIndicators
  workSchedule           = $proposal.workSchedule
  workModality           = $proposal.workModality
  requiresTravel         = $proposal.requiresTravel
  workLocation           = $proposal.workLocation
  availabilityNotes      = $proposal.availabilityNotes
  employmentType         = $proposal.employmentType
  salaryRangeMin         = $proposal.salaryRangeMin
  salaryRangeMax         = $proposal.salaryRangeMax
  salaryCurrency         = $proposal.salaryCurrency
  benefits               = $proposal.benefits
  expectedOutcomes       = $proposal.expectedOutcomes
} | ConvertTo-Json -Depth 5

Write-Host "--- SAVE BODY ---"
Write-Host $saveBody
Write-Host "--- CALLING PUT job-roles ---"

$bytes = [System.Text.Encoding]::UTF8.GetBytes($saveBody)
Write-Host "--- BODY BYTE COUNT: $($bytes.Length) vs CHAR COUNT: $($saveBody.Length) ---"

try {
  $r = Invoke-WebRequest -Uri "http://localhost:7071/api/job-roles/$roleId" -Method Put -Body $bytes -ContentType "application/json; charset=utf-8" -TimeoutSec 30 -UseBasicParsing
  Write-Host "SUCCESS (UTF8 bytes): $($r.StatusCode)"
} catch {
  Write-Host "FAILED (UTF8 bytes)"
  $resp = $_.Exception.Response
  if ($resp) {
    $stream = $resp.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "Body: $($reader.ReadToEnd())"
  } else {
    Write-Host $_.Exception.Message
  }
}
