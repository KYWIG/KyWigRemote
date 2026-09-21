<#
.SYNOPSIS
    Publie le client KyWigRemote en ClickOnce, en ligne de commande, sans Visual Studio.

.DESCRIPTION
    Reproduit exactement la commande "Publier" de l'IDE a partir du profil
    src\KyWigRemote.Client\Properties\PublishProfiles\ClickOnce.pubxml.

    Deux conditions sont indispensables et ce script s'en charge :
      1. le host dotnet doit etre resoluble par MSBuild (SDK .NET) ;
      2. la publication doit tourner DANS l'environnement developpeur Visual Studio
         (VsDevCmd.bat) sinon la resolution des ressources satellites echoue
         (des centaines d'erreurs MSB3113 "fichier introuvable").

    Aucun composant a installer : Build Tools 2022 avec les charges
    "ClickOnce", "NetCore.Component.SDK" et "ManagedDesktop" suffit.

.PARAMETER Configuration
    Configuration de compilation. Release par defaut.

.PARAMETER Clean
    Vide le dossier de sortie avant publication.

.EXAMPLE
    .\tools\publish-clickonce.ps1 -Clean
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\KyWigRemote.Client\KyWigRemote.Client.csproj"
$outDir = Join-Path $repoRoot "src\KyWigRemote.Client\dist\ClientClickOnce"

function Find-Dotnet {
    $candidates = @(
        (Join-Path $env:ProgramFiles "dotnet\dotnet.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "dotnet\dotnet.exe")
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    $cmd = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "dotnet introuvable. Installe le SDK .NET (aka.ms/dotnet/download)."
}

function Find-VsDevCmd {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere introuvable : Visual Studio (ou Build Tools) 2022 n'est pas installe."
    }
    $installPath = & $vswhere -all -products * -requires Microsoft.Component.MSBuild -property installationPath | Select-Object -First 1
    if (-not $installPath) { throw "Aucune installation Visual Studio/Build Tools avec MSBuild trouvee." }
    $devcmd = Join-Path $installPath "Common7\Tools\VsDevCmd.bat"
    if (-not (Test-Path $devcmd)) { throw "VsDevCmd.bat introuvable sous $installPath." }
    return $devcmd
}

$dotnet = Find-Dotnet
$dotnetDir = Split-Path -Parent $dotnet
$vsDevCmd = Find-VsDevCmd

Write-Host "dotnet   : $dotnet"
Write-Host "VsDevCmd : $vsDevCmd"
Write-Host "Projet   : $project"
Write-Host "Sortie   : $outDir"

if ($Clean -and (Test-Path $outDir)) {
    Write-Host "Nettoyage du dossier de sortie..."
    Remove-Item $outDir -Recurse -Force
}

# La publication ClickOnce autonome exige une restauration avec le RID cible.
Write-Host "Restauration (win-x64)..."
& $dotnet restore $project -r win-x64 --nologo
if ($LASTEXITCODE -ne 0) { throw "La restauration a echoue (code $LASTEXITCODE)." }

# MSBuild plein-framework, DANS l'environnement developpeur, avec dotnet sur le PATH.
# On genere un .cmd temporaire : dans un fichier multi-lignes, %PATH% s'evalue a
# l'execution (apres VsDevCmd), contrairement a un "cmd /c a && b" ou il serait pre-substitue
# et ecraserait le PATH etabli par VsDevCmd (msbuild deviendrait introuvable).
$msbuildArgs = "/t:Publish /p:PublishProfile=ClickOnce /p:Configuration=$Configuration /v:minimal /nologo"
$batch = @"
@echo off
call "$vsDevCmd" -arch=x64 -host_arch=x64 -no_logo
set "PATH=$dotnetDir;%PATH%"
msbuild "$project" $msbuildArgs
exit /b %errorlevel%
"@
$batchPath = Join-Path $env:TEMP "kywig-publish-clickonce.cmd"
Set-Content -Path $batchPath -Value $batch -Encoding Ascii

Write-Host "Publication ClickOnce..."
& cmd.exe /c $batchPath
$publishExit = $LASTEXITCODE
Remove-Item $batchPath -ErrorAction SilentlyContinue

$manifest = Join-Path $outDir "KyWigRemote.Client.application"
if ($publishExit -eq 0 -and (Test-Path $manifest)) {
    Write-Host ""
    Write-Host "Publication ClickOnce reussie." -ForegroundColor Green
    Write-Host "Manifeste de deploiement : $manifest"
    Write-Host "Deploie tout le contenu de : $outDir"
    exit 0
}

Write-Error "La publication ClickOnce a echoue (code $publishExit). Manifeste absent : $manifest"
exit 1
