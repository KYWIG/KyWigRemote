<#
.SYNOPSIS
    Genere les fichiers de marque (icone .ico multi-tailles + PNG) a partir du monogramme.

.DESCRIPTION
    Dessine le monogramme KyWigRemote (fond accent #007ACC, lettres blanches "KW") via GDI+.
    L'.ico est assemble au format CLASSIQUE (entrees DIB/BMP 32 bits), et NON en PNG embarque :
    le validateur ClickOnce (System.Deployment, .NET 4.8) rejette les icones PNG-compressees
    (« le fichier d'icone specifie dans le manifeste n'est pas valide »). On se limite aux tailles
    16..64 pour rester compatible avec ce validateur ancien.

    Produit aussi un PNG 256 pour la page web (favicon / en-tete).

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

# Dessine le monogramme et retourne un Bitmap 32 bits ARGB de la taille demandee.
function New-MonogramBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $g.Clear($accent)

    $penWidth = [Math]::Max(1, [int]($size / 32))
    $pen = New-Object System.Drawing.Pen($frame, $penWidth)
    $half = [int]($penWidth / 2)
    $g.DrawRectangle($pen, $half, $half, $size - $penWidth, $size - $penWidth)

    $fontSize = [float]($size * 0.46)
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $brush = New-Object System.Drawing.SolidBrush($white)
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString("KW", $font, $brush, $rect, $fmt)

    $g.Dispose(); $pen.Dispose(); $font.Dispose(); $brush.Dispose(); $fmt.Dispose()
    return $bmp
}

# Convertit un Bitmap en DIB d'icone (BITMAPINFOHEADER + bitmap XOR 32 bits + masque AND).
function Get-IconDib([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $pixels = New-Object byte[] ($stride * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    # BITMAPINFOHEADER (40 octets). biHeight = 2*h (bitmap XOR + masque AND).
    $bw.Write([uint32]40)
    $bw.Write([int32]$w)
    $bw.Write([int32]($h * 2))
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]0)              # BI_RGB
    $bw.Write([uint32]($w * $h * 4))
    $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
    # Bitmap XOR : lignes BGRA de bas en haut.
    for ($y = $h - 1; $y -ge 0; $y--) {
        $bw.Write($pixels, $y * $stride, $w * 4)
    }
    # Masque AND : 1 bit/pixel, lignes alignees sur 4 octets, tout a zero (opacite via alpha).
    $maskStride = [int]([Math]::Floor(($w + 31) / 32)) * 4
    $mask = New-Object byte[] ($maskStride * $h)
    $bw.Write($mask, 0, $mask.Length)

    $bw.Flush()
    return ,$ms.ToArray()
}

# --- PNG 256 pour la page web ---
$bmp256 = New-MonogramBitmap 256
$ms = New-Object System.IO.MemoryStream
$bmp256.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
[System.IO.File]::WriteAllBytes((Join-Path $outDir "kywig-256.png"), $ms.ToArray())
$bmp256.Dispose()
Write-Host "Ecrit : kywig-256.png"

# --- ICO classique (entrees DIB), tailles compatibles ClickOnce ---
$sizes = 16, 24, 32, 48, 64
$dibs = foreach ($s in $sizes) {
    $b = New-MonogramBitmap $s
    $d = Get-IconDib $b
    $b.Dispose()
    ,$d
}

$icoPath = Join-Path $outDir "kywig.ico"
$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)  # ICONDIR
    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $bw.Write([byte]$s)                     # largeur
        $bw.Write([byte]$s)                     # hauteur
        $bw.Write([byte]0)                      # nb couleurs
        $bw.Write([byte]0)                      # reserve
        $bw.Write([uint16]1)                    # plans
        $bw.Write([uint16]32)                   # bits/pixel
        $bw.Write([uint32]$dibs[$i].Length)     # taille des donnees
        $bw.Write([uint32]$offset)              # position
        $offset += $dibs[$i].Length
    }
    foreach ($d in $dibs) { $bw.Write($d, 0, $d.Length) }
}
finally { $bw.Dispose(); $fs.Dispose() }

Write-Host "Ecrit : kywig.ico ($((Get-Item $icoPath).Length) octets, $($sizes.Count) tailles DIB)"
Write-Host "Note : le monogramme sert desormais uniquement de favicon web (kywig-256.png)."
Write-Host "Termine."
