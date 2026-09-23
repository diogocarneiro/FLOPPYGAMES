#Requires -Version 7.0
<#
.SYNOPSIS
    Publica o Agent e o Label Studio (self-contained, win-x64) e compila o instalador Inno Setup.
.PARAMETER Version
    Opcional (ex. "0.2.0"). Sobrepõe a versão de Directory.Build.props e de FloppyGames.iss — é
    assim que o pipeline de release usa a versão da tag. Sem ele, usam-se esses valores.
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version
)

$ErrorActionPreference = "Stop"

if ($Version -and $Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Versão inválida '$Version' — esperado X.Y.Z (ex. 0.2.0)."
}
# [string[]] é obrigatório: atribuído a partir de um if, um array de um só elemento viraria uma
# string, e o splatting de uma string passa-a letra a letra ao comando.
[string[]]$versionArgs = if ($Version) { "-p:Version=$Version" } else { @() }
[string[]]$isccVersionArgs = if ($Version) { "/DMyAppVersion=$Version" } else { @() }

$installerDir = $PSScriptRoot
$srcDir = Split-Path -Parent $installerDir
$repoRoot = Split-Path -Parent $srcDir
$publishDir = Join-Path $installerDir "publish"

Write-Host "A limpar publicações anteriores..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue

Write-Host "A publicar FloppyGames.Agent ($Configuration, $Runtime, self-contained)..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot "src\FloppyGames.Agent\FloppyGames.Agent.csproj") `
    -c $Configuration -r $Runtime --self-contained true `
    -o (Join-Path $publishDir "Agent") @versionArgs
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o Agent." }

Write-Host "A publicar FloppyGames.LabelStudio ($Configuration, $Runtime, self-contained)..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot "src\FloppyGames.LabelStudio\FloppyGames.LabelStudio.csproj") `
    -c $Configuration -r $Runtime --self-contained true `
    -o (Join-Path $publishDir "LabelStudio") @versionArgs
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
& $iscc @isccVersionArgs (Join-Path $installerDir "FloppyGames.iss")
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador." }

Write-Host "Pronto: $installerDir\Output\FloppyGamesSetup.exe" -ForegroundColor Green
