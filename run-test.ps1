#!/usr/bin/env pwsh
# Script: Ejecutar prueba Curador → GraphArchitect
# Guarda resultados en un archivo de texto

$projectPath = "c:\EducationAI\HumanOS\backend\HumanOS"
$outputFile = "$projectPath\CURADOR_GRAPHARCHITECT_RESULTS.txt"

Write-Host "`n╔═══════════════════════════════════════════════════════════════╗"
Write-Host "║ EJECUTANDO PRUEBA: CURADOR → GRAPHARCHITECT                ║"
Write-Host "╚═══════════════════════════════════════════════════════════════╝`n"

Push-Location $projectPath

try {
    # Compilar
    Write-Host "📦 Compilando proyecto..." -ForegroundColor Cyan
    $buildResult = & dotnet build 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Error en compilación:" -ForegroundColor Red
        Write-Host $buildResult
        exit 1
    }
    Write-Host "✅ Compilación exitosa`n" -ForegroundColor Green

    # Crear instancia de prueba y ejecutar
    Write-Host "🚀 Ejecutando prueba..." -ForegroundColor Cyan
    
    # Crear un pequeño programa que ejecute la prueba
    $csharpCode = @"
using HumanOS.Tests;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var test = new TestCuradorGraphArchitectFlow(config);
await test.RunAsync();
"@

    # Ejecutar con dotnet run
    dotnet run --no-build -- 2>&1 | Tee-Object -Variable testOutput | Out-Host
    
    # Leer el archivo de resultados si existe
    if (Test-Path $outputFile) {
        Write-Host "`n`n"
        Write-Host "╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
        Write-Host "║ RESULTADOS DE LA PRUEBA                                       ║" -ForegroundColor Green
        Write-Host "╚═══════════════════════════════════════════════════════════════╝`n" -ForegroundColor Green
        
        $results = Get-Content $outputFile -Raw
        Write-Host $results
        
        Write-Host "`n📄 Archivo completo guardado en:" -ForegroundColor Green
        Write-Host "   $outputFile" -ForegroundColor Yellow
    }
    else {
        Write-Host "`n⚠️  Archivo de resultados no encontrado en: $outputFile" -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
