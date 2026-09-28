<#
.SYNOPSIS
    Sauvegarde la base SQLite de KyWigRemote (copie horodatée + rotation).

.DESCRIPTION
    Copie le fichier de base et, s'ils existent, ses journaux WAL (-wal) et index partagé (-shm)
    dans un sous-dossier horodaté. Inclure le WAL garantit une restauration cohérente (SQLite
    rejoue le journal à l'ouverture). Conserve les N dernières sauvegardes.

    Pour une cohérence maximale sur une base très active, arrêter le service le temps de la copie
    (console « Gestion du serveur » -> Arrêter), puis relancer. Pour un usage courant (faible
    charge), la copie à chaud avec le WAL est suffisante.

.PARAMETER DbPath
    Chemin du fichier .db. Par défaut, on cherche les emplacements usuels.

.PARAMETER BackupDir
    Dossier des sauvegardes. Par défaut, un sous-dossier « backups » à côté de la base.

.PARAMETER Keep
    Nombre de sauvegardes à conserver (rotation). 14 par défaut.

.EXAMPLE
    .\tools\backup-db.ps1 -DbPath 'C:\ProgramData\KyWigRemote\kywigremote.db'
#>
[CmdletBinding()]
param(
    [string]$DbPath,
    [string]$BackupDir,
    [int]$Keep = 14
)

$ErrorActionPreference = "Stop"

if (-not $DbPath) {
    $candidates = @(
        "C:\ProgramData\KyWigRemote\kywigremote.db",
        (Join-Path $env:LOCALAPPDATA "KyWigRemote\kywigremote.db"),
        "C:\Windows\System32\config\systemprofile\AppData\Local\KyWigRemote\kywigremote.db"
    )
    $DbPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $DbPath) {
        throw "Base introuvable. Precisez -DbPath (emplacements testes : $($candidates -join ', '))."
    }
}
if (-not (Test-Path $DbPath)) { throw "Base introuvable : $DbPath" }

if (-not $BackupDir) { $BackupDir = Join-Path (Split-Path -Parent $DbPath) "backups" }
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$target = Join-Path $BackupDir $stamp
New-Item -ItemType Directory -Path $target -Force | Out-Null

# Copie la base et ses journaux (WAL/SHM) s'ils sont presents.
$base = $DbPath
foreach ($suffix in @("", "-wal", "-shm")) {
    $src = $base + $suffix
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $target ([System.IO.Path]::GetFileName($src))) -Force
    }
}

$sizeMb = [Math]::Round(((Get-ChildItem $target -File | Measure-Object Length -Sum).Sum) / 1MB, 2)
Write-Host "Sauvegarde ecrite : $target ($sizeMb Mo)"

# Rotation : conserve les $Keep sous-dossiers les plus recents.
$all = Get-ChildItem $BackupDir -Directory | Sort-Object Name -Descending
if ($all.Count -gt $Keep) {
    $all | Select-Object -Skip $Keep | ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force
        Write-Host "Ancienne sauvegarde supprimee : $($_.Name)"
    }
}
