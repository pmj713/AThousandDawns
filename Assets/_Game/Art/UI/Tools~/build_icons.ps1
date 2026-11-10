# UI 그림 만들기: <Set>_sources.tsv (VARCO 이미지 → 프롬프트)와 <Set>_prompts.tsv (id → 프롬프트)를 맞춰
# 원본을 내려받아 Resources/ 아래에 넣어요. 이미 있는 그림은 건너뛰어요 (-Force면 다시).
#   -Set item   : 기물 → Icons/Resources/ItemIcons/<기물 id>.png (투명 바탕 256px)
#   -Set emblem : 키워드·부품·혈자리·키 → Icons/Resources/<id>.png (id에 폴더 포함, 예: KeywordIcons/Yeolyang)
#   -Set event  : 이벤트 그림 → Events/Resources/EventArt/<이벤트 id>.png (한지 바탕 그대로, 가로 960px)
#   -Set title  : 시작 화면 배경·프롤로그 → Title/Resources/TitleArt/<id>.png (가로 1536px)
# 원본(장당 약 2MB)은 저장소에 넣지 않고 -RawDir에 캐시해요.
param([ValidateSet("item", "emblem", "event", "title")][string]$Set = "item", [string]$RawDir = "$env:TEMP\AThousandDawns_icon_raw", [switch]$Force)

$tools = $PSScriptRoot
$outRoot = switch ($Set) { "event" { Join-Path $tools "..\Events\Resources" } "title" { Join-Path $tools "..\Title\Resources" } default { Join-Path $tools "..\Icons\Resources" } }
$prefix = switch ($Set) { "item" { "ItemIcons/" } "event" { "EventArt/" } "title" { "TitleArt/" } default { "" } }
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
        if ($Set -eq "event") { & (Join-Path $tools "resize.ps1") $raw $dst 960 }
        elseif ($Set -eq "title") { & (Join-Path $tools "resize.ps1") $raw $dst 1536 }
        else { & (Join-Path $tools "icon_alpha.ps1") $raw $dst }
        $made++
    }
}
"만듦: $made"
if ($missing) { "프롬프트를 못 찾음:"; $missing }
$lack = $idBySubject.Values | Where-Object { -not $done[$_] }
"원본이 없는 그림 ($($lack.Count)): $($lack -join ', ')"
