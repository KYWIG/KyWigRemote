<#
.SYNOPSIS
    Convertit une image PNG (avec transparence) en icone .ico multi-tailles au format classique DIB.

.DESCRIPTION
    Produit un .ico avec des entrees BMP/DIB 32 bits (et non PNG embarque), compatibles avec les
    validateurs anciens (dont ClickOnce). Le masque AND est derive du canal alpha pour respecter
    la transparence. Redimensionne proprement la source (bicubique) a chaque taille.

.PARAMETER Source
    Chemin de l'image source (PNG de preference, carree).

.PARAMETER Output
    Chemin du .ico a produire.

.PARAMETER Sizes
    Tailles a inclure. Defaut : 16,24,32,48,64,128,256.

.EXAMPLE
    .\tools\png-to-ico.ps1 -Source assets\brand\server-manager.png -Output assets\brand\server-manager.ico
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Source,
    [Parameter(Mandatory)] [string]$Output,
    [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256)
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Source)) { throw "Source introuvable : $Source" }
$src = [System.Drawing.Image]::FromFile((Resolve-Path $Source))

function Resize-To([System.Drawing.Image]$img, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($img, 0, 0, $size, $size)
    $g.Dispose()
    return $bmp
}

# DIB d'icone : BITMAPINFOHEADER + bitmap XOR (BGRA, bas->haut) + masque AND derive de l'alpha.
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
    $bw.Write([uint32]40); $bw.Write([int32]$w); $bw.Write([int32]($h * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($w * $h * 4))
    $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
    for ($y = $h - 1; $y -ge 0; $y--) { $bw.Write($pixels, $y * $stride, $w * 4) }

    # Masque AND : 1 bit par pixel (1 = transparent), lignes alignees sur 4 octets, de bas en haut.
    $maskStride = [int]([Math]::Floor(($w + 31) / 32)) * 4
    for ($y = $h - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $maskStride
        for ($x = 0; $x -lt $w; $x++) {
            $alpha = $pixels[$y * $stride + $x * 4 + 3]
            if ($alpha -lt 128) {
                $byteIndex = [int][Math]::Floor($x / 8)
                $row[$byteIndex] = $row[$byteIndex] -bor (0x80 -shr ($x % 8))
            }
        }
        $bw.Write($row, 0, $row.Length)
    }
    $bw.Flush()
    return ,$ms.ToArray()
}

$dibs = New-Object System.Collections.ArrayList
foreach ($s in $Sizes) {
    $b = Resize-To $src $s
    [void]$dibs.Add((Get-IconDib $b))
    $b.Dispose()
}
$src.Dispose()

$fs = [System.IO.File]::Open((Join-Path (Get-Location) $Output), [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$Sizes.Count)
    $offset = 6 + (16 * $Sizes.Count)
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $dim = if ($Sizes[$i] -ge 256) { 0 } else { $Sizes[$i] }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$dibs[$i].Length); $bw.Write([uint32]$offset)
        $offset += $dibs[$i].Length
    }
    foreach ($d in $dibs) { $bw.Write($d, 0, $d.Length) }
}
finally { $bw.Dispose(); $fs.Dispose() }

Write-Host "Ecrit : $Output ($($Sizes.Count) tailles)"
