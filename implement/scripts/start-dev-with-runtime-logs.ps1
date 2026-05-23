[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$implementRoot = Split-Path -Parent $scriptDir
$frontendDir = Join-Path $implementRoot 'src\frontend'
$backendProject = Join-Path $implementRoot 'src\backend\CobolAnalyzer.API\CobolAnalyzer.API.csproj'
$logDir = Join-Path $implementRoot 'log\runtime'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'

if (-not (Test-Path $frontendDir)) {
    throw "Frontend directory not found: $frontendDir"
}

if (-not (Test-Path $backendProject)) {
    throw "Backend project not found: $backendProject"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet command was not found.'
}

if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    throw 'npm.cmd command was not found.'
}

New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$backendOut = Join-Path $logDir "backend-live-$timestamp.out.log"
$backendErr = Join-Path $logDir "backend-live-$timestamp.err.log"
$frontendOut = Join-Path $logDir "frontend-live-$timestamp.out.log"
$frontendErr = Join-Path $logDir "frontend-live-$timestamp.err.log"

$backend = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $backendProject) `
    -WorkingDirectory $implementRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $backendOut `
    -RedirectStandardError $backendErr `
    -PassThru

$frontend = Start-Process `
    -FilePath 'npm.cmd' `
    -ArgumentList @('run', 'dev') `
    -WorkingDirectory $frontendDir `
    -WindowStyle Hidden `
    -RedirectStandardOutput $frontendOut `
    -RedirectStandardError $frontendErr `
    -PassThru

Write-Host "Started backend  PID=$($backend.Id)"
Write-Host "  stdout: $backendOut"
Write-Host "  stderr: $backendErr"
Write-Host "Started frontend PID=$($frontend.Id)"
Write-Host "  stdout: $frontendOut"
Write-Host "  stderr: $frontendErr"
Write-Host ''
Write-Host 'Default URLs'
Write-Host '  Backend : http://localhost:5000'
Write-Host '  Frontend: http://127.0.0.1:5173/'
Write-Host ''
Write-Host "Stop with: Stop-Process -Id $($backend.Id),$($frontend.Id)"
