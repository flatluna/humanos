$ErrorActionPreference = "Stop"
Import-Module SqlServer -ErrorAction SilentlyContinue
$token = az account get-access-token --resource https://database.windows.net --query accessToken -o tsv
Invoke-Sqlcmd -ServerInstance "flatsqlserver.database.windows.net" -Database "HumanOSDev" -AccessToken $token -InputFile "c:\EducationAI\HumanOS\backend\_temp-grant-mi.sql"
Write-Output "SQL_GRANT_DONE"
