Add-Type -AssemblyName System.Drawing

function New-IconBitmap {
    param([int]$size)

    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $left = $size * 0.18
    $right = $size * 0.82
    $top = $size * 0.10
    $bottom = $size * 0.90
    $fold = $size * 0.22

    $page = New-Object System.Drawing.Drawing2D.GraphicsPath
    $page.AddLine($left, $top, $right - $fold, $top)
    $page.AddLine($right - $fold, $top, $right, $top + $fold)
    $page.AddLine($right, $top + $fold, $right, $bottom)
    $page.AddLine($right, $bottom, $left, $bottom)
    $page.CloseFigure()

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillPath($white, $page)
    $borderColor = [System.Drawing.Color]::FromArgb(120, 120, 120)
    $borderPen = New-Object System.Drawing.Pen($borderColor, [single]($size * 0.012))
    $g.DrawPath($borderPen, $page)

    $foldPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $foldPath.AddLine($right - $fold, $top, $right - $fold, $top + $fold)
    $foldPath.AddLine($right - $fold, $top + $fold, $right, $top + $fold)
    $foldPath.CloseFigure()
    $foldFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 210, 210))
    $g.FillPath($foldFill, $foldPath)
    $g.DrawPath($borderPen, $foldPath)

    $bandHeight = $size * 0.26
    $bandTop = $size * 0.55
    $bandBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(197, 40, 40))
    $g.FillRectangle($bandBrush, [single]$left, [single]$bandTop, [single]($right - $left), [single]$bandHeight)

    $font = New-Object System.Drawing.Font("Arial", [single]($size * 0.16), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF([single]$left, [single]$bandTop, [single]($right - $left), [single]$bandHeight)
    $g.DrawString("PDF", $font, $textBrush, $rect, $format)

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap -size $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,@{ Size = $s; Bytes = $ms.ToArray() }
    $bmp.Dispose()
    $ms.Dispose()
}

$outPath = Join-Path $PSScriptRoot "appicon.ico"
$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0)            # reserved
$bw.Write([uint16]1)            # type = icon
$bw.Write([uint16]$pngs.Count)  # count

$offset = 6 + (16 * $pngs.Count)
foreach ($p in $pngs) {
    $dim = if ($p.Size -ge 256) { 0 } else { $p.Size }
    $bw.Write([byte]$dim)        # width
    $bw.Write([byte]$dim)        # height
    $bw.Write([byte]0)           # color count
    $bw.Write([byte]0)           # reserved
    $bw.Write([uint16]1)         # planes
    $bw.Write([uint16]32)        # bit count
    $bw.Write([uint32]$p.Bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $p.Bytes.Length
}

foreach ($p in $pngs) {
    $bw.Write($p.Bytes)
}

$bw.Flush()
$bw.Dispose()
$fs.Dispose()
Write-Output "Wrote $outPath ($((Get-Item $outPath).Length) bytes)"
