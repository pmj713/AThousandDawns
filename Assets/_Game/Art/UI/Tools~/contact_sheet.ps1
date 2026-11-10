# 아이콘 확인용 한 장 (어두운 UI 바탕 위, 파일 이름 표시)
param([string]$Dir, [string]$Out, [int]$Cell = 128, [int]$Cols = 10)
Add-Type -AssemblyName System.Drawing
$files = Get-ChildItem $Dir -Filter *.png | Sort-Object Name
$rows = [math]::Ceiling($files.Count / $Cols)
$bmp = New-Object Drawing.Bitmap ($Cols * $Cell), ($rows * ($Cell + 14))
$g = [Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(38, 36, 34))
$font = New-Object Drawing.Font "Consolas", 7
for ($i = 0; $i -lt $files.Count; $i++) {
    $x = ($i % $Cols) * $Cell; $y = [math]::Floor($i / $Cols) * ($Cell + 14)
    $img = [Drawing.Image]::FromFile($files[$i].FullName)
    $g.DrawImage($img, $x, $y, $Cell, $Cell); $img.Dispose()
    $g.DrawString($files[$i].BaseName, $font, [Drawing.Brushes]::Gainsboro, $x + 2, $y + $Cell)
}
$bmp.Save($Out); $g.Dispose(); $bmp.Dispose()
