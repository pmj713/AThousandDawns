# 투명 PNG의 빈 여백을 잘라 내요 (Pad px만 남김). 제목 붓글씨·밑줄처럼 자리를 맞춰 놓을 그림용
param([string]$Path, [int]$Pad = 4)
Add-Type -AssemblyName System.Drawing
$src = [Drawing.Bitmap]::FromFile((Resolve-Path $Path).Path)
$minX = $src.Width; $minY = $src.Height; $maxX = -1; $maxY = -1
for ($y = 0; $y -lt $src.Height; $y++) {
    for ($x = 0; $x -lt $src.Width; $x++) {
        if ($src.GetPixel($x, $y).A -gt 8) {
            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
$minX = [math]::Max(0, $minX - $Pad); $minY = [math]::Max(0, $minY - $Pad)
$maxX = [math]::Min($src.Width - 1, $maxX + $Pad); $maxY = [math]::Min($src.Height - 1, $maxY + $Pad)
$rect = New-Object Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
$out = $src.Clone($rect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$src.Dispose()
$out.Save((Resolve-Path $Path).Path, [Drawing.Imaging.ImageFormat]::Png)
"$Path -> $($out.Width)x$($out.Height)"
$out.Dispose()
