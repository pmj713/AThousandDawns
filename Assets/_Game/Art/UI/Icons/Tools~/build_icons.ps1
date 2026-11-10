# 아이콘 만들기: <Set>_sources.tsv (VARCO 이미지 → 프롬프트)와 <Set>_prompts.tsv (id → 프롬프트)를 맞춰
# 원본을 내려받고 투명 바탕 256px로 바꿔 Resources/ 아래에 넣어요. 이미 있는 아이콘은 건너뛰어요 (-Force면 다시).
#   -Set item   : 기물 → Resources/ItemIcons/<기물 id>.png
#   -Set emblem : 키워드·부품·혈자리 → Resources/<id>.png (id에 폴더 포함, 예: KeywordIcons/Yeolyang)
# 원본(장당 약 2MB)은 저장소에 넣지 않고 -RawDir에 캐시해요.
param([ValidateSet("item", "emblem")][string]$Set = "item", [string]$RawDir = "$env:TEMP\AThousandDawns_icon_raw", [switch]$Force)

$tools = $PSScriptRoot
$outRoot = Join-Path $tools "..\Resources"
$prefix = if ($Set -eq "item") { "ItemIcons/" } else { "" }
New-Item -ItemType Directory -Force $RawDir | Out-Null

$idBySubject = @{}
Import-Csv (Join-Path $tools "$($Set)_prompts.tsv") -Delimiter "`t" -Encoding UTF8 | ForEach-Object { $idBySubject[$_.subject] = $prefix + $_.id }

$made = 0; $missing = @(); $done = @{}
$sources = Join-Path $tools "$($Set)_sources.tsv"
if (Test-Path $sources) {
    foreach ($line in Get-Content $sources -Encoding UTF8) {
        if (-not $line.Trim()) { continue }
        $file, $subject = $line -split "`t", 2
        $id = $idBySubject[$subject]
        if (-not $id) { $missing += $subject; continue }
        $done[$id] = $true
        $dst = Join-Path $outRoot "$id.png"
        if ((Test-Path $dst) -and -not $Force) { continue }
        New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
        $raw = Join-Path $RawDir $file
        if (-not (Test-Path $raw)) { curl.exe -s -f -o $raw "https://3d.varco.ai/api/objects/$file" }
        & (Join-Path $tools "icon_alpha.ps1") $raw $dst
        $made++
    }
}
"만듦: $made"
if ($missing) { "프롬프트를 못 찾음:"; $missing }
$lack = $idBySubject.Values | Where-Object { -not $done[$_] }
"원본이 없는 아이콘 ($($lack.Count)): $($lack -join ', ')"
