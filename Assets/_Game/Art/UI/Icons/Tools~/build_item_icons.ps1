# 기물 아이콘 만들기: item_sources.tsv (VARCO 이미지 → 프롬프트)와 item_prompts.tsv (기물 id → 프롬프트)를 맞춰
# 원본을 내려받고 투명 바탕 256px로 바꿔 Resources/ItemIcons/<id>.png에 넣어요. 이미 있는 아이콘은 건너뛰어요 (-Force면 다시).
# 원본(장당 약 2MB)은 저장소에 넣지 않고 -RawDir에 캐시해요.
param([string]$RawDir = "$env:TEMP\AThousandDawns_icon_raw", [switch]$Force)

$tools = $PSScriptRoot
$outDir = Join-Path $tools "..\Resources\ItemIcons"
New-Item -ItemType Directory -Force $outDir, $RawDir | Out-Null

$idBySubject = @{}
Import-Csv (Join-Path $tools "item_prompts.tsv") -Delimiter "`t" | ForEach-Object { $idBySubject[$_.subject] = $_.id }

$made = 0; $missing = @()
foreach ($line in Get-Content (Join-Path $tools "item_sources.tsv") -Encoding UTF8) {
    if (-not $line.Trim()) { continue }
    $file, $subject = $line -split "`t", 2
    $id = $idBySubject[$subject]
    if (-not $id) { $missing += $subject; continue }
    $dst = Join-Path $outDir "$id.png"
    if ((Test-Path $dst) -and -not $Force) { continue }
    $raw = Join-Path $RawDir $file
    if (-not (Test-Path $raw)) { curl.exe -s -f -o $raw "https://3d.varco.ai/api/objects/$file" }
    & (Join-Path $tools "icon_alpha.ps1") $raw $dst
    $made++
}
"만듦: $made"
if ($missing) { "프롬프트를 못 찾음:"; $missing }
$have = (Get-ChildItem $outDir -Filter *.png).BaseName
$lack = $idBySubject.Values | Where-Object { $have -notcontains $_ }
"아이콘 없는 기물 ($($lack.Count)): $($lack -join ', ')"
