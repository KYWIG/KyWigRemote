<#
.SYNOPSIS
    Monte une instance de developpement complete de KyWigRemote en une commande :
    (re)demarre le serveur, cree le compte administrateur si besoin, puis lance le client
    et la console d'administration.

.DESCRIPTION
    Evite la mise en route manuelle (demarrage serveur, amorcage admin, lancement des UIs).
    Destine au poste de DEVELOPPEMENT uniquement - base SQLite locale, jamais la production.

    Aucun secret n'est stocke dans ce script : le mot de passe admin est OBLIGATOIREMENT fourni
    en parametre par l'appelant (regle 5/6 de CLAUDE.md).

.PARAMETER AdminPassword
    Mot de passe du compte admin applicatif (fixture de dev). Facultatif : s'il est omis, un mot
    de passe aleatoire est genere et affiche une fois. Aucun secret n'est stocke dans ce script.

.PARAMETER Port
    Port d'ecoute HTTP du serveur (defaut 5080).

.PARAMETER Reset
    Supprime la base de dev avant de demarrer (repart d'un schema neuf + donnees de demo).

.PARAMETER NoUi
    Ne lance pas le client ni la console d'administration (serveur seul).

.EXAMPLE
    .\tools\dev-run.ps1 -Reset
    (mot de passe admin genere automatiquement et affiche)

.EXAMPLE
    .\tools\dev-run.ps1 -AdminPassword 'MonMotDePasseDeDev!' -Reset
#>
[CmdletBinding()]
param(
    [string] $AdminPassword,
    [int]    $Port = 5080,
    [switch] $Reset,
    [switch] $NoUi
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$base = "http://localhost:$Port"

# Mot de passe admin : fourni par l'appelant, ou genere aleatoirement (fixture de dev, jamais en
# dur dans le script - regles 5/6 de CLAUDE.md). Genere par le RNG cryptographique de .NET.
$generatedPassword = $false
if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
    $alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%*-_".ToCharArray()
    $AdminPassword = -join (1..20 | ForEach-Object { $alphabet[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($alphabet.Length)] })
    $generatedPassword = $true
}

# Runtime .NET : rend le host 'dotnet' resoluble pour les .exe lances (apphosts). Evite d'avoir
# a positionner DOTNET_ROOT/PATH a la main quand le SDK est installe hors du PATH systeme.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"),
        "$env:ProgramFiles\dotnet",
        "${env:ProgramFiles(x86)}\dotnet"
    )
    $dotnetDir = $candidates | Where-Object { $_ -and (Test-Path (Join-Path $_ "dotnet.exe")) } | Select-Object -First 1
    if ($dotnetDir) {
        $env:DOTNET_ROOT = $dotnetDir
        $env:PATH = "$dotnetDir;$env:PATH"
        Write-Host "Runtime .NET detecte : $dotnetDir"
    } else {
        Write-Warning "dotnet introuvable sur le PATH. Si les .exe ne demarrent pas, installe le runtime .NET 8 (Desktop + ASP.NET Core)."
    }
}

function Exe([string] $name) {
    Join-Path $root "src\KyWigRemote.$name\bin\Debug\net8.0-windows\KyWigRemote.$name.exe"
}

$serverExe = Exe "Server"
if (-not (Test-Path $serverExe)) {
    throw "Serveur non compile. Lance d'abord : dotnet build"
}

# 1. Base neuve si demande (le serveur doit etre arrete pour liberer le fichier).
if ($Reset) {
    Get-Process KyWigRemote.Server -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    $db = Join-Path $env:LOCALAPPDATA "KyWigRemote\kywigremote.db"
    Remove-Item "$db*" -Force -ErrorAction SilentlyContinue
    Write-Host "Base de dev supprimee (repart a neuf)."
}

# 2. Demarrage du serveur (si pas deja en cours) sur le port demande.
if (-not (Get-Process KyWigRemote.Server -ErrorAction SilentlyContinue)) {
    $env:KyWigRemote__Server__Urls = $base
    Start-Process $serverExe
    Write-Host "Serveur lance sur $base - attente de disponibilite..."
}

$ready = $false
foreach ($i in 1..20) {
    Start-Sleep -Milliseconds 700
    try { Invoke-RestMethod "$base/health" -TimeoutSec 3 | Out-Null; $ready = $true; break } catch { }
}
if (-not $ready) { throw "Le serveur n'a pas repondu sur $base." }
Write-Host "Serveur disponible."

# 3. Amorcage de l'administrateur (ignore s'il existe deja : 409).
try {
    $body = @{ username = "admin"; password = $AdminPassword; displayName = "Admin dev" } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$base/api/auth/bootstrap-admin" -ContentType "application/json" -Body $body | Out-Null
    Write-Host "Compte admin cree."
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    if ($code -eq 409) { Write-Host "Compte admin deja present - inchange." }
    else { throw }
}

# 4. Lancement des interfaces.
if (-not $NoUi) {
    Start-Process (Exe "Client")
    Start-Process (Exe "Admin")
    Write-Host "Client et console d'administration lances."
}

Write-Host ""
if ($generatedPassword) {
    Write-Host "Pret. Connecte-toi avec :" -ForegroundColor Green
    Write-Host "   serveur      = $base"
    Write-Host "   utilisateur  = admin"
    Write-Host "   mot de passe = $AdminPassword" -ForegroundColor Yellow
    Write-Host "(mot de passe genere pour cette session - note-le, il n'est pas stocke en clair.)"
} else {
    Write-Host "Pret. Connecte-toi avec : serveur=$base, utilisateur=admin, mot de passe=(celui fourni)."
}
