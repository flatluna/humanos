# Script para aplicar schema a NGOAutomate usando PowerShell y Azure SQL
param(
    [string]$Server = "flatsqlserver.database.windows.net",
    [string]$Database = "NGOAutomate",
    [string]$ResourceGroup = "FlatBItDevelopment"
)

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Aplicando Schema a NGOAutomate"
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Servidor: $Server"
Write-Host "Base de datos: $Database"
Write-Host ""

# SQL Script para crear tablas
$sqlScript = @"
-- Tabla: Organizations (ONGs)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Organizations')
BEGIN
    CREATE TABLE Organizations (
        OrgId INT PRIMARY KEY IDENTITY(1,1),
        OrgName NVARCHAR(255) NOT NULL,
        Country NVARCHAR(100),
        CreatedAt DATETIME DEFAULT GETUTCDATE(),
        IsActive BIT DEFAULT 1
    )
    PRINT 'Tabla Organizations creada'
END
GO

-- Tabla: Agents (Agentes AI)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Agents')
BEGIN
    CREATE TABLE Agents (
        AgentId INT PRIMARY KEY IDENTITY(1,1),
        OrgId INT NOT NULL,
        AgentName NVARCHAR(255) NOT NULL,
        AgentType NVARCHAR(50),
        Instructions NVARCHAR(MAX),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETUTCDATE(),
        FOREIGN KEY (OrgId) REFERENCES Organizations(OrgId)
    )
    PRINT 'Tabla Agents creada'
END
GO

-- Tabla: Tasks (Tareas automatizadas)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tasks')
BEGIN
    CREATE TABLE Tasks (
        TaskId INT PRIMARY KEY IDENTITY(1,1),
        AgentId INT NOT NULL,
        TaskName NVARCHAR(255) NOT NULL,
        TaskStatus NVARCHAR(50),
        InputData NVARCHAR(MAX),
        OutputData NVARCHAR(MAX),
        ExecutedAt DATETIME,
        CompletedAt DATETIME,
        FOREIGN KEY (AgentId) REFERENCES Agents(AgentId)
    )
    PRINT 'Tabla Tasks creada'
END
GO

-- Tabla: Logs (Auditoría)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Logs')
BEGIN
    CREATE TABLE Logs (
        LogId BIGINT PRIMARY KEY IDENTITY(1,1),
        OrgId INT,
        AgentId INT,
        TaskId INT,
        LogLevel NVARCHAR(20),
        Message NVARCHAR(MAX),
        CreatedAt DATETIME DEFAULT GETUTCDATE(),
        FOREIGN KEY (OrgId) REFERENCES Organizations(OrgId),
        FOREIGN KEY (AgentId) REFERENCES Agents(AgentId),
        FOREIGN KEY (TaskId) REFERENCES Tasks(TaskId)
    )
    PRINT 'Tabla Logs creada'
END
GO

-- Verificar tablas creadas
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME
"@

try {
    # Ejecutar script
    Write-Host "Ejecutando script SQL..." -ForegroundColor Yellow
    
    # Usar az sql db query (si está disponible en tu version de az cli)
    $tempFile = "$env:TEMP\ngoautomate-schema-$([DateTime]::Now.Ticks).sql"
    $sqlScript | Out-File $tempFile -Encoding UTF8
    
    Write-Host "Script guardado en: $tempFile"
    Write-Host ""
    Write-Host "Para aplicar el schema, ejecuta uno de estos comandos:"
    Write-Host ""
    Write-Host "Option 1: Usando Azure CLI"
    Write-Host "  az sql db query --resource-group $ResourceGroup --server $Server --database $Database --query-text `"@$tempFile`""
    Write-Host ""
    Write-Host "Option 2: Usando SQL Server Management Studio"
    Write-Host "  Abre la BD $Database en SSMS y ejecuta el archivo SQL"
    Write-Host ""
    Write-Host "Option 3: Usando sqlcmd (si tienes credenciales admin)"
    Write-Host "  sqlcmd -S $Server -d $Database -i `"$tempFile`" -U <username> -P <password>"
    Write-Host ""
    
    # Intentar ejecutar directamente
    Write-Host "Intentando aplicar schema automáticamente..."
    sqlcmd -S $Server -d $Database -i $tempFile -G -N 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✓ Schema aplicado exitosamente" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "⚠ Hubo un error al aplicar el schema." -ForegroundColor Yellow
        Write-Host "Por favor, aplica manualmente el archivo SQL:" -ForegroundColor Yellow
        Write-Host "  $tempFile" -ForegroundColor White
    }
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Script SQL guardado en: C:\EducationAI\HumanOS\backend\create-ngoautomate.sql" -ForegroundColor Yellow
}
