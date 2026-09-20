#Requires -Version 7.0
<#
.SYNOPSIS
    Publica o Agent e o Label Studio (self-contained, win-x64) e compila o instalador Inno Setup.
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$srcDir = Split-Path -Parent $installerDir
$repoRoot = Split-Path -Parent $srcDir
$publishDir = Join-Path $installerDir "publish"

Write-Host "A limpar publicações anteriores..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue

Write-Host "A publicar FloppyGames.Agent ($Configuration, $Runtime, self-contained)..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot "src\FloppyGames.Agent\FloppyGames.Agent.csproj") `
    -c $Configuration -r $Runtime --self-contained true `
    -o (Join-Path $publishDir "Agent")
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o Agent." }

Write-Host "A publicar FloppyGames.LabelStudio ($Configuration, $Runtime, self-contained)..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot "src\FloppyGames.LabelStudio\FloppyGames.LabelStudio.csproj") `
    -c $Configuration -r $Runtime --self-contained true `
    -o (Join-Path $publishDir "LabelStudio")
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o Label Studio." }

$candidatePaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$iscc = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup não encontrado. Instala com: winget install JRSoftware.InnoSetup"
}

Write-Host "A compilar o instalador com $iscc..." -ForegroundColor Cyan
& $iscc (Join-Path $installerDir "FloppyGames.iss")
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador." }

Write-Host "Pronto: $installerDir\Output\FloppyGamesSetup.exe" -ForegroundColor Green
