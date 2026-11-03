$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSCommandPath
$out = Join-Path $root 'build\app.ico'

function New-GlyphBitmap([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap($size, $size)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)

  $bg = [System.Drawing.Color]::FromArgb(45, 110, 220)
  $bgBrush = New-Object System.Drawing.SolidBrush($bg)
  $pad = [int]($size * 0.06)
  $d = $size - 2 * $pad
  $g.FillEllipse($bgBrush, $pad, $pad, $d, $d)

  $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [float]($size * 0.10))
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $inner = [int]($size * 0.30)
  $g.DrawEllipse($pen, $inner, $inner, ($size - 2 * $inner), ($size - 2 * $inner))
  # 关断竖线（表示断电/离线开关）
  $x1 = [int]($size * 0.5); $y1 = [int]($size * 0.17)
  $x2 = [int]($size * 0.5); $y2 = [int]($size * 0.42)
  $g.DrawLine($pen, $x1, $y1, $x2, $y2)

  $pen.Dispose(); $bgBrush.Dispose(); $g.Dispose()
  return $bmp
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = @()
foreach ($s in $sizes) {
  $b = New-GlyphBitmap $s
  $ms = New-Object System.IO.MemoryStream
  $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $images += ,@($s, $ms.ToArray())
  $b.Dispose()
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type icon
$bw.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
  $s = [int]$img[0]; $data = [byte[]]$img[1]
  $bw.Write([Byte]($(if ($s -ge 256) {0} else {$s})))   # width
  $bw.Write([Byte]($(if ($s -ge 256) {0} else {$s})))   # height
  $bw.Write([Byte]0)      # colors
  $bw.Write([Byte]0)      # reserved
  $bw.Write([UInt16]1)    # planes
  $bw.Write([UInt16]32)   # bitcount
  $bw.Write([UInt32]$data.Length)
  $bw.Write([UInt32]$offset)
  $offset += $data.Length
}
foreach ($img in $images) { $bw.Write([byte[]]$img[1]) }
$bw.Flush(); $bw.Close(); $fs.Close()
Write-Host "已生成图标: $out ($([int]((Get-Item $out).Length)) bytes)"
