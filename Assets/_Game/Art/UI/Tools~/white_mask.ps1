# Ink on white (or black on alpha) -> white shape with alpha = ink darkness, for tinting with GUI.color.
# -Crop trims to the shape bounds (+Pad px). -Size WxH resizes after cropping (keeps aspect inside, centered).
param([string]$In, [string]$OutPath, [switch]$Crop, [int]$Pad = 6, [int]$W = 0, [int]$H = 0, [int]$Floor = 18)
Add-Type -AssemblyName System.Drawing
$src = [Drawing.Bitmap]::FromFile($In)
$bmp = New-Object Drawing.Bitmap $src.Width, $src.Height, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [Drawing.Graphics]::FromImage($bmp); $g.DrawImage($src, 0, 0, $src.Width, $src.Height); $g.Dispose()
$rect = New-Object Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
$data = $bmp.LockBits($rect, 'ReadWrite', ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
$n = $data.Stride * $bmp.Height; $px = New-Object byte[] $n
[Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $n)
$minX = $bmp.Width; $minY = $bmp.Height; $maxX = 0; $maxY = 0
for ($y = 0; $y -lt $bmp.Height; $y++) { $row = $y * $data.Stride
  for ($x = 0; $x -lt $bmp.Width; $x++) { $i = $row + $x * 4
    $lum = (0.114 * $px[$i] + 0.587 * $px[$i+1] + 0.299 * $px[$i+2])
    $a = (255 - $lum) * $px[$i+3] / 255
    $a = [Math]::Max(0, [Math]::Min(255, ($a - $Floor) * 255 / (255 - $Floor)))
    $px[$i] = 255; $px[$i+1] = 255; $px[$i+2] = 255; $px[$i+3] = [byte]$a
    if ($a -gt 20) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } } } }
[Runtime.InteropServices.Marshal]::Copy($px, 0, $data.Scan0, $n); $bmp.UnlockBits($data)
$img = $bmp
if ($Crop) {
  $minX = [Math]::Max(0, $minX - $Pad); $minY = [Math]::Max(0, $minY - $Pad)
  $maxX = [Math]::Min($bmp.Width - 1, $maxX + $Pad); $maxY = [Math]::Min($bmp.Height - 1, $maxY + $Pad)
  $img = $bmp.Clone((New-Object Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)), $bmp.PixelFormat)
}
if ($W -gt 0 -and $H -gt 0) {
  $dst = New-Object Drawing.Bitmap $W, $H, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [Drawing.Graphics]::FromImage($dst); $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
  $g.DrawImage($img, 0, 0, $W, $H); $g.Dispose(); $img = $dst
}
$img.Save($OutPath, [Drawing.Imaging.ImageFormat]::Png)
"$OutPath $($img.Width)x$($img.Height)"
