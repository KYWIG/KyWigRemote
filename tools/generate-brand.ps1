<#
.SYNOPSIS
    Genere les fichiers de marque (icone .ico multi-tailles + PNG) a partir du monogramme.

.DESCRIPTION
    Dessine le monogramme KyWigRemote (fond accent #007ACC, lettres blanches "KW")
    via GDI+ puis assemble un .ico contenant plusieurs resolutions (PNG embarque, gere
    par Windows Vista et ulterieur). Produit aussi un PNG 256 pour la page web.

    Sortie : assets\brand\kywig.ico et assets\brand\kywig-256.png
    A relancer uniquement si la marque change. Necessite Windows PowerShell (System.Drawing).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot "assets\brand"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$accent = [System.Drawing.ColorTranslator]::FromHtml("#007ACC")
$frame  = [System.Drawing.ColorTranslator]::FromHtml("#005A99")
$white  = [System.Drawing.Color]::White

function New-MonogramPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $g.Clear($accent)

    # Cadre interieur discret (proportionnel a la taille).
    $penWidth = [Math]::Max(1, [int]($size / 32))
    $pen = New-Object System.Drawing.Pen($frame, $penWidth)
    $half = [int]($penWidth / 2)
    $g.DrawRectangle($pen, $half, $half, $size - $penWidth, $size - $penWidth)

    # Monogramme "KW" centre. On dimensionne la police pour remplir la tuile.
    $fontSize = [float]($size * 0.46)
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $brush = New-Object System.Drawing.SolidBrush($white)
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString("KW", $font, $brush, $rect, $fmt)

    $g.Dispose(); $pen.Dispose(); $font.Dispose(); $brush.Dispose(); $fmt.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

# --- PNG 256 pour la page web ---
$png256 = New-MonogramPng 256
[System.IO.File]::WriteAllBytes((Join-Path $outDir "kywig-256.png"), $png256)
Write-Host "Ecrit : kywig-256.png ($($png256.Length) octets)"

# --- ICO multi-tailles (chaque entree est un PNG) ---
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) { ,(New-MonogramPng $s) }

$icoPath = Join-Path $outDir "kywig.ico"
$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    # ICONDIR : reserve=0, type=1 (icone), count
    $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)

    # Les donnees image commencent apres l'entete + toutes les entrees (16 octets chacune).
    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $dim = if ($s -ge 256) { 0 } else { $s }   # 0 signifie 256 dans le format ICO
        $bw.Write([Byte]$dim)                       # largeur
        $bw.Write([Byte]$dim)                       # hauteur
        $bw.Write([Byte]0)                          # nb couleurs (0 = truecolor)
        $bw.Write([Byte]0)                          # reserve
        $bw.Write([UInt16]1)                        # plans
        $bw.Write([UInt16]32)                       # bits par pixel
        $bw.Write([UInt32]$images[$i].Length)       # taille des donnees
        $bw.Write([UInt32]$offset)                  # position des donnees
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $bw.Write($img) }
}
finally { $bw.Dispose(); $fs.Dispose() }

Write-Host "Ecrit : kywig.ico ($((Get-Item $icoPath).Length) octets, $($sizes.Count) tailles)"
Write-Host "Termine."
