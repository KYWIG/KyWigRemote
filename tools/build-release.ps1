<#
.SYNOPSIS
    Construit l'installeur MSI final de KyWigRemote (serveur + gestionnaire + administration
    + distribution web ClickOnce du client).

.DESCRIPTION
    1. Publie le serveur et le gestionnaire (autonomes, cote a cote).
    2. Publie la console d'administration (autonome).
    3. Publie le client en ClickOnce et prepare la page web d'installation.
    4. Assemble le tout dans dist\release\staging puis construit dist\release\KyWigRemote-Setup.msi
       avec WiX (outil dotnet).

    Prerequis : SDK .NET, Build Tools 2022 (pour ClickOnce) et l'outil WiX
    (« dotnet tool install --global wix »). Aucun droit administrateur requis pour construire.

.PARAMETER Version
    Version du produit (x.x.x.x), inscrite dans le MSII. Defaut 1.0.0.0.

.PARAMETER SkipMsi
    S'arrete apres l'assemblage (utile pour inspecter dist\release\staging).

.EXAMPLE
    .\tools\build-release.ps1 -Version 1.0.0.0
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = "1.0.0.0",
    # URL publique de la page d'installation sur LE serveur cible (ex. http://kers014:5080/install).
    # Elle est gravee dans le manifeste ClickOnce : indispensable pour que le client s'installe
    # apres telechargement par le navigateur. A adapter au nom reel du serveur de deploiement.
    [string]$BaseUrl = "http://localhost:5080/install",
    [switch]$SkipMsi
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Find-Dotnet {
    $c = @((Join-Path $env:ProgramFiles "dotnet\dotnet.exe"), (Join-Path ${env:ProgramFiles(x86)} "dotnet\dotnet.exe"))
    foreach ($p in $c) { if (Test-Path $p) { return $p } }
    $cmd = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "dotnet introuvable. Installe le SDK .NET (aka.ms/dotnet/download)."
}

function Find-Wix {
    $tool = Join-Path $env:USERPROFILE ".dotnet\tools\wix.exe"
    if (Test-Path $tool) { return $tool }
    $cmd = Get-Command wix.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "WiX introuvable. Installe-le : dotnet tool install --global wix"
}

$dotnet = Find-Dotnet
$dist = Join-Path $repoRoot "dist\release"
$staging = Join-Path $dist "staging"
$serverStage = Join-Path $staging "server"
$adminStage = Join-Path $staging "admin"
$webStage = Join-Path $staging "web"
$msiPath = Join-Path $dist "KyWigRemote-Setup.msi"

Write-Host "== KyWigRemote : construction de l'installeur ($Version) ==" -ForegroundColor Cyan
Write-Host "dotnet  : $dotnet"
Write-Host "BaseUrl : $BaseUrl"
if ($BaseUrl -match "localhost|127\.0\.0\.1") {
    Write-Warning "BaseUrl pointe sur localhost : le client ne s'installera QUE depuis le serveur lui-meme. " +
        "Pour un deploiement reseau, relancer avec -BaseUrl http://<nom-du-serveur>:5080/install"
}

# --- Nettoyage ---
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
if (Test-Path $msiPath) { Remove-Item $msiPath -Force }
New-Item -ItemType Directory -Path $serverStage, $adminStage, $webStage -Force | Out-Null

$publishArgs = @("-c", $Configuration, "-r", "win-x64", "--self-contained", "true",
    "-p:PublishSingleFile=false", "-p:DebugType=none", "--nologo")

# --- 1. Serveur (autonome) ---
Write-Host "[1/5] Publication du serveur..." -ForegroundColor Green
& $dotnet publish (Join-Path $repoRoot "src\KyWigRemote.Server\KyWigRemote.Server.csproj") @publishArgs -o $serverStage
if ($LASTEXITCODE -ne 0) { throw "Echec de publication du serveur." }

# --- 2. Gestionnaire de service (a cote du serveur : le locator le trouve) ---
Write-Host "[2/5] Publication du gestionnaire de service..." -ForegroundColor Green
& $dotnet publish (Join-Path $repoRoot "src\KyWigRemote.ServerManager\KyWigRemote.ServerManager.csproj") @publishArgs -o $serverStage
if ($LASTEXITCODE -ne 0) { throw "Echec de publication du gestionnaire." }

# --- 3. Administration (autonome) ---
Write-Host "[3/5] Publication de la console d'administration..." -ForegroundColor Green
& $dotnet publish (Join-Path $repoRoot "src\KyWigRemote.Admin\KyWigRemote.Admin.csproj") @publishArgs -o $adminStage
if ($LASTEXITCODE -ne 0) { throw "Echec de publication de l'administration." }

# --- 3b. Active la distribution web dans la config du serveur publie (web a cote du serveur) ---
$appsettings = Join-Path $serverStage "appsettings.json"
$json = Get-Content $appsettings -Raw | ConvertFrom-Json
if (-not $json.KyWigRemote.Distribution) {
    $json.KyWigRemote | Add-Member -NotePropertyName Distribution -NotePropertyValue ([pscustomobject]@{}) -Force
}
$json.KyWigRemote.Distribution | Add-Member -NotePropertyName WebRoot -NotePropertyValue "..\web" -Force
$json.KyWigRemote.Distribution | Add-Member -NotePropertyName RequestPath -NotePropertyValue "/install" -Force
# Ecoute sur le reseau (et plus seulement localhost) pour que les postes clients atteignent la page.
$json.KyWigRemote.Server.Urls = "http://0.0.0.0:5080"
$json | ConvertTo-Json -Depth 12 | Set-Content $appsettings -Encoding UTF8
Write-Host "      Distribution web activee (WebRoot=..\web), ecoute sur 0.0.0.0:5080."

# --- 4. Client ClickOnce + page web ---
Write-Host "[4/5] Publication ClickOnce du client..." -ForegroundColor Green
& (Join-Path $PSScriptRoot "publish-clickonce.ps1") -Configuration $Configuration -InstallUrl $BaseUrl -Clean
if ($LASTEXITCODE -ne 0) { throw "Echec de publication ClickOnce." }
$clickOnceDir = Join-Path $repoRoot "src\KyWigRemote.Client\dist\ClientClickOnce"
Copy-Item (Join-Path $clickOnceDir "*") $webStage -Recurse -Force
Copy-Item (Join-Path $repoRoot "installer\web\index.html") $webStage -Force
Copy-Item (Join-Path $repoRoot "assets\brand\client.png") $webStage -Force
Write-Host "      Page web prete : $(Split-Path $webStage -Leaf)\index.html"

if ($SkipMsi) {
    Write-Host "Assemblage termine (--SkipMsi). Contenu : $staging" -ForegroundColor Yellow
    exit 0
}

# --- 5. Construction du MSI avec WiX ---
Write-Host "[5/5] Construction du MSI (WiX)..." -ForegroundColor Green
$wix = Find-Wix
$wxs = Join-Path $repoRoot "installer\Product.wxs"
$ico = Join-Path $repoRoot "assets\brand\kywig.ico"
& $wix build $wxs -arch x64 -o $msiPath `
    -d "StagingDir=$staging" `
    -d "ProductVersion=$Version" `
    -d "IconFile=$ico"
if ($LASTEXITCODE -ne 0) { throw "Echec de la construction WiX." }

$sizeMb = [Math]::Round((Get-Item $msiPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Installeur construit : $msiPath ($sizeMb Mo)" -ForegroundColor Green
