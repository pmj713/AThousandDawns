# 그림을 가로 Width px로 줄여 PNG로 저장 (비율 유지)
param([string]$Src, [string]$Dst, [int]$Width = 960)
Add-Type -AssemblyName System.Drawing
$img = [Drawing.Image]::FromFile((Resolve-Path $Src).Path)
$h = [int][math]::Round($img.Height * $Width / $img.Width)
$bmp = New-Object Drawing.Bitmap $Width, $h
$g = [Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.DrawImage($img, 0, 0, $Width, $h)
$bmp.Save([IO.Path]::GetFullPath($Dst), [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose(); $img.Dispose()
